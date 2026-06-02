using MediatR;
using PDV.Application.Common.Interfaces;
using PDV.Application.Features.Sales.Dtos;
using PDV.Domain.Entities;
using PDV.Domain.Repositories;
using PDV.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace PDV.Application.Features.Sales.Commands.CreateSale;

using PDV.Domain.Enums;

public record CreateSaleCommand : IRequest<Guid>
{
    public List<CartItemDto> Items { get; set; } = new();
    public string PaymentMethod { get; set; } = "Cash";
    public string UserId { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public Guid? CashRegisterId { get; set; }
    public bool IsPaid { get; set; }
    public bool RequiresInvoice { get; set; } = false;
}

public class CreateSaleCommandHandler : IRequestHandler<CreateSaleCommand, Guid>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITicketSequenceRepository _ticketSequenceRepository;
    private readonly IApplicationDbContext _context;

    public CreateSaleCommandHandler(
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        ITicketSequenceRepository ticketSequenceRepository,
        IApplicationDbContext context)
    {
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _ticketSequenceRepository = ticketSequenceRepository;
        _context = context;
    }

    public async Task<Guid> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        if (_context is DbContext dbContext)
        {
            dbContext.ChangeTracker.Clear();
        }

        const int maxRetries = 3;
        int attempt = 0;

        while (true)
        {
            attempt++;

            await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                if (!request.CashRegisterId.HasValue)
                {
                    throw new InvalidOperationException("La caja registradora es requerida para registrar una venta.");
                }

                // Buscar turno activo con caja registradora asociada para obtener BranchId
                var activeShift = await _context.Shifts
                    .Include(s => s.CashRegister)
                    .FirstOrDefaultAsync(s => s.CashRegisterId == request.CashRegisterId.Value && s.Status == ShiftStatus.Open, cancellationToken);
                
                if (activeShift == null)
                {
                    throw new InvalidOperationException("No se puede registrar una venta si no hay un turno de caja activo.");
                }

                // Crear el agregado Sale usando el constructor de dominio
                var saleNumber = SaleNumber.Generate();
                var paymentMethod = Enum.TryParse<PaymentMethodType>(request.PaymentMethod, true, out var pm) ? pm : PaymentMethodType.Cash;
                
                // Obtener o inicializar la secuencia de tickets para la venta en esta caja
                var sequence = await _ticketSequenceRepository.GetWithLockAsync(request.CashRegisterId.Value, TicketSequenceType.Sale, cancellationToken);
                if (sequence == null)
                {
                    sequence = new TicketSequence(request.CashRegisterId.Value, TicketSequenceType.Sale, "V");
                    await _ticketSequenceRepository.AddAsync(sequence, cancellationToken);
                }

                int nextFolio = sequence.GetNextTicketNumber();
                string series = sequence.Series ?? "V";

                var sale = new Sale(
                    saleNumber: saleNumber,
                    paymentMethod: paymentMethod,
                    userId: request.UserId,
                    shiftId: activeShift.Id,
                    series: series,
                    folio: nextFolio,
                    clientId: request.ClientId,
                    cashRegisterId: request.CashRegisterId);

                if (activeShift.CashRegister == null)
                {
                    throw new InvalidOperationException("La caja registradora asociada al turno no pudo ser cargada.");
                }
                sale.SetBranch(activeShift.CashRegister.BranchId);

                await _ticketSequenceRepository.UpdateAsync(sequence, cancellationToken);

                // Validar que si se requiere factura, debe haber un cliente
                if (request.RequiresInvoice && !request.ClientId.HasValue)
                {
                    throw new InvalidOperationException("Para facturar se requiere seleccionar un cliente con datos fiscales completos.");
                }

                // Procesar cada item
                foreach (var item in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(item.Product.Id, cancellationToken);
                    if (product == null)
                        throw new InvalidOperationException($"Product {item.Product.Id} not found");

                    if (!product.IsActive)
                        throw new InvalidOperationException($"Product {product.Id} is not active");

                    // Determinar cantidad según tipo de venta
                    decimal quantity;
                    if (product.SaleType == SaleType.Bulk)
                    {
                        quantity = item.Weight;
                    }
                    else
                    {
                        quantity = item.Quantity;
                    }

                    // Verificar stock usando el método de dominio
                    if (!product.HasStock(quantity))
                    {
                        throw new InvalidOperationException(
                            $"Stock insuficiente para el producto {product.Name}. Disponible: {product.Stock}, Requerido: {quantity}");
                    }

                    // Determinar decimal de taxRate e isTaxExempt a partir de product.TaxRate
                    decimal taxRatePercent = 0m;
                    bool isExempt = false;

                    switch (product.TaxRate)
                    {
                        case TaxRateType.Exempt:
                            isExempt = true;
                            break;
                        case TaxRateType.ZeroRate:
                            taxRatePercent = 0m;
                            break;
                        case TaxRateType.Rate8:
                            taxRatePercent = 8m;
                            break;
                        case TaxRateType.Rate16:
                            taxRatePercent = 16m;
                            break;
                    }

                    // Crear el item de venta usando el constructor de dominio
                    var saleItem = new SaleItem(
                        product: product,
                        quantity: quantity,
                        taxRate: taxRatePercent,
                        isTaxExempt: isExempt,
                        priceOverride: item.PriceOverride);

                    // Agregar item al agregado (esto actualiza el total automáticamente)
                    sale.AddItem(saleItem);

                    // Registrar movimiento de inventario transaccional (Kardex)
                    product.ApplyMovement(-quantity, InventoryMovementType.Sale, sale.Id);
                }

                // Marcar como pagada si es necesario
                if (request.IsPaid)
                {
                    sale.MarkAsPaid();
                }

                // Agregar la venta al repositorio
                await _saleRepository.AddAsync(sale, cancellationToken);

                // Guardar todos los cambios
                await _context.SaveChangesAsync(cancellationToken);
                await _context.CommitTransactionAsync(cancellationToken);

                return sale.Id;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                await _context.RollbackTransactionAsync(cancellationToken);
                await Task.Delay(50 * attempt, cancellationToken);
                continue;
            }
            catch
            {
                await _context.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
