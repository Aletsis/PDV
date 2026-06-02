using PDV.Domain.Enums;

namespace PDV.Application.Features.FolioSequences.Dtos;

public class FolioSequenceDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public InvoiceType SeriesType { get; set; }
    public string Series { get; set; } = string.Empty;
    public int LastFolio { get; set; }
    public int FolioDigits { get; set; } = 6;
    public string? ConceptCode { get; set; }
}
