namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing project visibility level.
/// </summary>
public sealed record ProjectVisibility
{
    public string Value { get; }

    private ProjectVisibility(string value) => Value = value;

    public static readonly ProjectVisibility Public = new("public");
    public static readonly ProjectVisibility Private = new("private");
    public static readonly ProjectVisibility Unlisted = new("unlisted");
    public static readonly ProjectVisibility Members = new("members");
    public static readonly ProjectVisibility Subscribers = new("subscribers");

    public static ProjectVisibility? FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "public" => Public,
            "private" => Private,
            "unlisted" => Unlisted,
            "members" => Members,
            "subscribers" => Subscribers,
            _ => null
        };
    }

    public bool IsRestricted => Value == "private" || Value == "unlisted";

    public static implicit operator string(ProjectVisibility visibility) => visibility.Value;

    public override string ToString() => Value;
}
