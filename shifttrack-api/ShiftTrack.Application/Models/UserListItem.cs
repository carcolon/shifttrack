namespace ShiftTrack.Application.Models;

public record UserListItem(
    Guid Id,
    string DisplayName,
    string Email,
    int Role,
    string Location,
    string Company,
    string Operation,
    string ShiftTime);
