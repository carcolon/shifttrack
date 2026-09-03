namespace ShiftTrack.Domain.Entities;

public class Holiday
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "CO";
    public bool IsActive { get; set; } = true;
    public bool IsManual { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
