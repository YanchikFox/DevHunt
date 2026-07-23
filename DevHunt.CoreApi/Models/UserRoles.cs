namespace DevHunt.CoreApi.Models;

/// <summary>
/// String constants for user roles.
/// Use these instead of inline string literals in EF queries to avoid B-09 violations.
/// </summary>
public static class UserRoles
{
    public const string Participant = "participant";
    public const string Curator    = "curator";
    public const string Admin      = "admin";
    public const string SuperAdmin = "superadmin";
}
