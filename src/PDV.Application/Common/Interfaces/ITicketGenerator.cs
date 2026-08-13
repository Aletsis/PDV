namespace PDV.Application.Common.Interfaces;

public interface ITicketGenerator
{
    Task<string> GenerateSaleTicketAsync(Guid saleId, CancellationToken cancellationToken = default, int? widthCharacters = null);
    Task<string> GenerateInvoiceTicketAsync(Guid invoiceId, CancellationToken cancellationToken = default, int? widthCharacters = null);
    Task<string> GenerateReturnTicketAsync(Guid returnId, CancellationToken cancellationToken = default, int? widthCharacters = null);
    Task<string> GenerateCashCollectionTicketAsync(Guid collectionId, CancellationToken cancellationToken = default, int? widthCharacters = null);
    Task<string> GenerateCashCutTicketAsync(Guid cutId, CancellationToken cancellationToken = default, int? widthCharacters = null);
    Task<string> GenerateOrderTicketAsync(Guid orderId, CancellationToken cancellationToken = default, int? widthCharacters = null);
    Task<string> GenerateRouteManifestTicketAsync(Guid routeId, CancellationToken cancellationToken = default, int? widthCharacters = null);
}
