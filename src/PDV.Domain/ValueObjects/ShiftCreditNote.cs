namespace PDV.Domain.ValueObjects;

public record ShiftCreditNote(string CreditNoteId, decimal Amount, string Reason, DateTime Date);
