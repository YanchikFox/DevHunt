namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing an issue resolution action (warn, suspend, remove, dismiss).
/// </summary>
public sealed record IssueActionType
{
    public string Value { get; }

    private IssueActionType(string value) => Value = value;

    public static readonly IssueActionType Warn = new("warn");
    public static readonly IssueActionType Suspend = new("suspend");
    public static readonly IssueActionType Remove = new("remove");
    public static readonly IssueActionType Dismiss = new("dismiss");

    public static implicit operator string(IssueActionType action) => action.Value;

    public override string ToString() => Value;
}
