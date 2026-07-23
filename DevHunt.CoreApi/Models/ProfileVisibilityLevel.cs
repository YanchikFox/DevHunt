namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing profile visibility level.
/// </summary>
public sealed record ProfileVisibilityLevel
{
    public string Value { get; }

    private ProfileVisibilityLevel(string value) => Value = value;

    public static readonly ProfileVisibilityLevel Public = new("public");
    public static readonly ProfileVisibilityLevel Private = new("private");
    public static readonly ProfileVisibilityLevel FriendsOnly = new("friends_only");

    public static ProfileVisibilityLevel? FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "public" => Public,
            "private" => Private,
            "friends_only" => FriendsOnly,
            _ => null
        };
    }

    public static implicit operator string(ProfileVisibilityLevel level) => level.Value;

    public override string ToString() => Value;
}
