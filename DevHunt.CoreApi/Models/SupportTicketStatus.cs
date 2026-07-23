namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing support ticket status.
/// </summary>
public sealed record SupportTicketStatus
{
    public string Value { get; }

    private SupportTicketStatus(string value) => Value = value;

    public static readonly SupportTicketStatus Open = new("open");
    public static readonly SupportTicketStatus InProgress = new("in_progress");
    public static readonly SupportTicketStatus Resolved = new("resolved");

    public static implicit operator string(SupportTicketStatus status) => status.Value;

    public override string ToString() => Value;
}
