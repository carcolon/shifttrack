namespace ShiftTrack.Domain.Entities;

public class RequestExportJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? RequestedByUserId { get; init; }
    public string RequestedByEmail { get; init; } = string.Empty;
    public string RequestedByName { get; init; } = string.Empty;
    public int RequestedByRole { get; init; }
    public bool RequestedByIsSystemHidden { get; init; }
    public string ScopeCompaniesJson { get; init; } = "[]";
    public string Status { get; init; } = "pending";
    public string? HangfireJobId { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public byte[]? FileContent { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
}
