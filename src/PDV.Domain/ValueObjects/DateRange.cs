namespace PDV.Domain.ValueObjects;

public record DateRange
{
    public DateTime Start { get; init; }
    public DateTime End { get; init; }

    private DateRange() { } // For EF Core

    private DateRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    public static DateRange Create(DateTime start, DateTime end)
    {
        if (start > end)
            throw new ArgumentException("Start date cannot be after end date.");

        return new DateRange(start, end);
    }

    public bool Includes(DateTime date)
    {
        return date >= Start && date <= End;
    }
}
