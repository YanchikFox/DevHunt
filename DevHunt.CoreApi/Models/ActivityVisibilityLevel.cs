namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing activity visibility level.
/// </summary>
public sealed record ActivityVisibilityLevel
{
    public string Value { get; }

    private ActivityVisibilityLevel(string value) => Value = value;

    public static readonly ActivityVisibilityLevel Public = new("public");
    public static readonly ActivityVisibilityLevel Followers = new("followers");
    public static readonly ActivityVisibilityLevel Private = new("private");

    public static ActivityVisibilityLevel? FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "public" => Public,
            "followers" => Followers,
            "private" => Private,
            _ => null
        };
    }

    public static implicit operator string(ActivityVisibilityLevel level) => level.Value;

    public override string ToString() => Value;
}
