using PDV.Domain.Enums;

namespace PDV.Application.Features.TicketSequences.Dtos;

public class TicketSequenceDto
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public TicketSequenceType SequenceType { get; set; }
    public string? Series { get; set; }
    public int LastTicketNumber { get; set; }
    public bool ResetOnNewShift { get; set; }
}
