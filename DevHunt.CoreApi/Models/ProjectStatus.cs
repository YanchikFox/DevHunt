namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing project status.
/// </summary>
public sealed record ProjectStatus
{
    public string Value { get; }

    private ProjectStatus(string value) => Value = value;

    public static readonly ProjectStatus Draft = new("draft");
    public static readonly ProjectStatus Recruiting = new("recruiting");
    public static readonly ProjectStatus Active = new("active");
    public static readonly ProjectStatus Completed = new("completed");
    public static readonly ProjectStatus Archived = new("archived");
    public static readonly ProjectStatus Cancelled = new("cancelled");

    public static ProjectStatus? FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "draft" => Draft,
            "recruiting" => Recruiting,
            "active" => Active,
            "completed" => Completed,
            "archived" => Archived,
            "cancelled" => Cancelled,
            _ => null
        };
    }

    public static implicit operator string(ProjectStatus status) => status.Value;

    public override string ToString() => Value;
}
