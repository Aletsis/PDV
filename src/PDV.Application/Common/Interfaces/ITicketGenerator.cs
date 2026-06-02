namespace PDV.Application.Common.Interfaces;

public interface ITicketGenerator
{
    Task<string> GenerateSaleTicketAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<string> GenerateInvoiceTicketAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<string> GenerateReturnTicketAsync(Guid returnId, CancellationToken cancellationToken = default);
    Task<string> GenerateCashCollectionTicketAsync(Guid collectionId, CancellationToken cancellationToken = default);
    Task<string> GenerateCashCutTicketAsync(Guid cutId, CancellationToken cancellationToken = default);
}
