namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing team member status.
/// </summary>
public sealed record TeamMemberStatus
{
    public string Value { get; }

    private TeamMemberStatus(string value) => Value = value;

    public static readonly TeamMemberStatus Active = new("active");
    public static readonly TeamMemberStatus Inactive = new("inactive");
    public static readonly TeamMemberStatus Left = new("left");
    public static readonly TeamMemberStatus Removed = new("removed");
    public static readonly TeamMemberStatus Suspended = new("suspended");

    private static readonly Dictionary<string, TeamMemberStatus> _lookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["active"] = Active,
        ["inactive"] = Inactive,
        ["left"] = Left,
        ["removed"] = Removed,
        ["suspended"] = Suspended,
    };

    public static TeamMemberStatus? FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return _lookup.TryGetValue(value.Trim(), out var status) ? status : null;
    }

    public static implicit operator string(TeamMemberStatus status) => status.Value;

    public override string ToString() => Value;
}
