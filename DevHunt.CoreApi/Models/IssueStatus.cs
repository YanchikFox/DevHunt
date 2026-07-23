namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing project issue status.
/// </summary>
public sealed record IssueStatus
{
    public string Value { get; }

    private IssueStatus(string value) => Value = value;

    public static readonly IssueStatus Open = new("open");
    public static readonly IssueStatus Investigating = new("investigating");
    public static readonly IssueStatus Resolved = new("resolved");
    public static readonly IssueStatus Escalated = new("escalated");

    public static implicit operator string(IssueStatus status) => status.Value;

    public override string ToString() => Value;
}
