namespace ShiftTrack.Application.Models;

public record AuthResult(
    bool Success,
    string? Message,
    string? Email = null,
    string? DisplayName = null,
    int? Role = null,
    bool RequirePasswordChange = false);
