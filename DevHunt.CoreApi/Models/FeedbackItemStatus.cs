namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing feedback item status.
/// </summary>
public sealed record FeedbackItemStatus
{
    public string Value { get; }

    private FeedbackItemStatus(string value) => Value = value;

    public static readonly FeedbackItemStatus Open = new("open");
    public static readonly FeedbackItemStatus Planned = new("planned");
    public static readonly FeedbackItemStatus Completed = new("completed");

    public static implicit operator string(FeedbackItemStatus status) => status.Value;

    public override string ToString() => Value;
}
