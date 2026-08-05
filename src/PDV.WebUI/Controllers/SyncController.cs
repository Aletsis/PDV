using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PDV.Application.Common.Interfaces;
using PDV.Domain.Entities;
using PDV.Application.Features.Sync.Commands;
using PDV.Application.Features.Sync.Dtos;
using PDV.Application.Features.Clients.Queries.GetClientsDelta;
using PDV.Application.Features.Shifts.Queries.GetActiveShiftByUserId;
using PDV.Application.Features.Printers.Queries.GetPrintersDelta;
using PDV.Application.Features.TicketSequences.Queries.GetTicketSequencesDelta;
using PDV.Application.Features.FolioSequences.Queries.GetFolioSequencesDelta;
using PDV.WebUI.Middleware;

namespace PDV.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiKey]
public class SyncController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISender mediator, IApplicationDbContext context, ILogger<SyncController> logger)
    {
        _mediator = mediator;
        _context = context;
        _logger = logger;
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok("pong");
    }

    [HttpPost("receive")]
    public async Task<IActionResult> Receive([FromBody] OutboxSyncDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await _context.InboxMessages.AnyAsync(m => m.MessageId == dto.MessageId, cancellationToken);
            if (exists)
            {
                return Ok(new { Success = true, Message = "Already queued/processed" });
            }

            var inboxMessage = new InboxMessage(dto.MessageId, dto.EventType, dto.Payload);
            _context.InboxMessages.Add(inboxMessage);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new { Success = true, Message = "Enqueued in Inbox" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing sync message {Id}", dto.MessageId);
            return Problem(ex.Message);
        }
    }

    [HttpPost("receive-batch")]
    public async Task<IActionResult> ReceiveBatch([FromBody] IEnumerable<OutboxSyncDto> dtos, CancellationToken cancellationToken)
    {
        var results = new List<MessageSyncResult>();
        var addedIds = new HashSet<Guid>();
        var toAdd = new List<InboxMessage>();

        foreach (var dto in dtos)
        {
            if (addedIds.Contains(dto.MessageId))
            {
                results.Add(new MessageSyncResult(dto.MessageId, true, "Duplicate in batch"));
                continue;
            }

            var exists = await _context.InboxMessages.AnyAsync(m => m.MessageId == dto.MessageId, cancellationToken);
            if (exists)
            {
                results.Add(new MessageSyncResult(dto.MessageId, true, "Already queued/processed"));
                continue;
            }

            try
            {
                var inboxMessage = new InboxMessage(dto.MessageId, dto.EventType, dto.Payload);
                toAdd.Add(inboxMessage);
                addedIds.Add(dto.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inbox message for {Id}", dto.MessageId);
                results.Add(new MessageSyncResult(dto.MessageId, false, ex.Message));
            }
        }

        if (toAdd.Any())
        {
            try
            {
                _context.InboxMessages.AddRange(toAdd);
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var msg in toAdd)
                {
                    results.Add(new MessageSyncResult(msg.MessageId, true, null));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving batch to InboxMessages");
                foreach (var msg in toAdd)
                {
                    results.Add(new MessageSyncResult(msg.MessageId, false, "Batch save failed: " + ex.Message));
                }
            }
        }

        return Ok(new BatchSyncResult(results));
    }

    [HttpGet("clients-delta")]
    public async Task<IActionResult> GetClientsDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new GetClientsDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting clients delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("products-delta")]
    public async Task<IActionResult> GetProductsDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new PDV.Application.Features.Products.Queries.GetProductsDelta.GetProductsDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("product-branch-stocks-delta")]
    public async Task<IActionResult> GetProductBranchStocksDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new PDV.Application.Features.Products.Queries.GetProductBranchStocksDelta.GetProductBranchStocksDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product branch stocks delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("branches-delta")]
    public async Task<IActionResult> GetBranchesDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new PDV.Application.Features.Branches.Queries.GetBranchesDelta.GetBranchesDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branches delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("unidades-medida")]
    public async Task<IActionResult> GetUnidadesMedida([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime();
            var result = await _mediator.Send(new PDV.Application.Features.UnidadesMedida.GetUnidadesMedidaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting units of measure since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("cash-registers-delta")]
    public async Task<IActionResult> GetCashRegistersDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new PDV.Application.Features.CashRegisters.Queries.GetCashRegistersDelta.GetCashRegistersDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash registers delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("printers-delta")]
    public async Task<IActionResult> GetPrintersDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new GetPrintersDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting printers delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("ticket-sequences-delta")]
    public async Task<IActionResult> GetTicketSequencesDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new GetTicketSequencesDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ticket sequences delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("folio-sequences-delta")]
    public async Task<IActionResult> GetFolioSequencesDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new GetFolioSequencesDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting folio sequences delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("users-delta")]
    public async Task<IActionResult> GetUsersDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new PDV.Application.Features.Sync.Queries.GetUsersDelta.GetUsersDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("active-shift-by-user/{userId}")]
    public async Task<IActionResult> GetActiveShiftByUserId([FromRoute] string userId)
    {
        try
        {
            var result = await _mediator.Send(new GetActiveShiftByUserIdQuery(userId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking active shift for user {UserId}", userId);
            return Problem(ex.Message);
        }
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSales(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] bool? isPaid,
        [FromQuery] bool? isCancelled,
        [FromQuery] Guid? cashRegisterId)
    {
        try
        {
            var query = new PDV.Application.Features.Sales.Queries.ListSales.ListSalesQuery(
                StartDate: startDate,
                EndDate: endDate,
                IsPaid: isPaid,
                IsCancelled: isCancelled,
                CashRegisterId: cashRegisterId);
                
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing sales from sync controller");
            return Problem(ex.Message);
        }
    }

    [HttpGet("sales/by-ticket")]
    public async Task<IActionResult> GetSaleByTicket(
        [FromQuery] string series,
        [FromQuery] int folio)
    {
        try
        {
            var query = new PDV.Application.Features.Sales.Queries.GetSaleByTicket.GetSaleByTicketQuery(series, folio);
            var result = await _mediator.Send(query);
            if (result == null)
            {
                return NotFound("Venta no encontrada con la serie y folio especificados.");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sale by ticket: {Series}-{Folio}", series, folio);
            return Problem(ex.Message);
        }
    }

    [HttpGet("sales/{id}")]
    public async Task<IActionResult> GetSaleById(Guid id)
    {
        try
        {
            var result = await _mediator.Send(new PDV.Application.Features.Sales.Queries.GetSale.GetSaleQuery(id));
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sale by id: {Id}", id);
            return Problem(ex.Message);
        }
    }
}

