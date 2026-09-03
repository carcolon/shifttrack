namespace ShiftTrack.Domain.Entities;

public class ScheduleEvent
{
    public Guid Id { get; set; }
    public Guid? EmployeeId { get; set; }
    public string EmployeeEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid? UpdatedByUserId { get; set; }
    public string UpdatedByEmail { get; set; } = string.Empty;
    public string UpdatedByName { get; set; } = string.Empty;
    public int UpdatedByRole { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}

