namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing a bulk user action (block, unblock, verify).
/// </summary>
public sealed record BulkAction
{
    public string Value { get; }

    private BulkAction(string value) => Value = value;

    public static readonly BulkAction Block = new("block");
    public static readonly BulkAction Unblock = new("unblock");
    public static readonly BulkAction Verify = new("verify");

    private static readonly HashSet<string> _valid = new(StringComparer.OrdinalIgnoreCase)
    {
        Block.Value, Unblock.Value, Verify.Value
    };

    public static bool IsValid(string? value) =>
        value is not null && _valid.Contains(value);

    public static implicit operator string(BulkAction action) => action.Value;

    public override string ToString() => Value;
}
