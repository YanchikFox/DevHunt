namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing AI plan status (B-09 compliance).
/// </summary>
public sealed record AiPlanStatus
{
    public string Value { get; }

    private AiPlanStatus(string value) => Value = value;

    public static readonly AiPlanStatus Draft = new("draft");
    public static readonly AiPlanStatus Applying = new("applying");
    public static readonly AiPlanStatus Applied = new("applied");
    public static readonly AiPlanStatus Failed = new("failed");

    /// <summary>Implicit conversion to string for EF Core queries.</summary>
    public static implicit operator string(AiPlanStatus status) => status.Value;

    public override string ToString() => Value;
}
