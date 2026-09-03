namespace ShiftTrack.Application.Models;

public class UpdateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Role { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string ShiftTime { get; set; } = string.Empty;

    public string? GetMissingField()
    {
        if (string.IsNullOrWhiteSpace(FirstName)) return "First Name";
        if (string.IsNullOrWhiteSpace(LastName)) return "Last Name";
        if (Role < 0) return "Role";
        if (string.IsNullOrWhiteSpace(Location)) return "Location";
        if (string.IsNullOrWhiteSpace(Company)) return "Company";
        if (string.IsNullOrWhiteSpace(Operation)) return "Operation";
        if (string.IsNullOrWhiteSpace(ShiftTime)) return "Shift Time";
        return null;
    }
}
