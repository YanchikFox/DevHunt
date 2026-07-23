namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing project issue priority.
/// </summary>
public sealed record IssuePriority
{
    public string Value { get; }

    private IssuePriority(string value) => Value = value;

    public static readonly IssuePriority Normal = new("normal");
    public static readonly IssuePriority High = new("high");
    public static readonly IssuePriority Urgent = new("urgent");

    public static implicit operator string(IssuePriority priority) => priority.Value;

    public override string ToString() => Value;
}
