namespace ShiftTrack.Application;

public static class RoleHelpers
{
    public const int Employee = 0;
    public const int Manager = 1;
    public const int Admin = 2;
    public const int TeamLeader = 3;

    public static bool IsKnownRole(int role) =>
        role is Employee or Manager or Admin or TeamLeader;

    public static bool IsEmployee(int role) => role == Employee;

    public static bool IsManager(int role) => role == Manager;

    public static bool IsAdmin(int role) => role == Admin;

    public static bool IsTeamLeader(int role) => role == TeamLeader;

    public static bool IsEmployeeLike(int role) => role is Employee or TeamLeader;

    public static bool CanManageUsers(int role) => role is Manager or Admin;

    public static bool CanReviewPto(int role) => role is Manager or Admin;

    public static bool CanViewCoverage(int role) => role is Manager or Admin or TeamLeader;

    public static bool CanManagerManageRole(int targetRole) => IsKnownRole(targetRole) && !IsAdmin(targetRole);
}
