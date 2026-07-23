namespace DevHunt.CoreApi.Models;

public sealed class TaskPriority
{
    public string Value { get; }

    private TaskPriority(string value) => Value = value;

    public static readonly TaskPriority Low = new("low");
    public static readonly TaskPriority Medium = new("medium");
    public static readonly TaskPriority High = new("high");
    public static readonly TaskPriority Urgent = new("urgent");

    private static readonly Dictionary<string, TaskPriority> All = new(StringComparer.OrdinalIgnoreCase)
    {
        [Low.Value] = Low,
        [Medium.Value] = Medium,
        [High.Value] = High,
        [Urgent.Value] = Urgent
    };

    public static TaskPriority FromString(string value) =>
        All.TryGetValue(value, out var priority) ? priority : throw new ArgumentException($"Invalid priority: {value}");

    public static implicit operator string(TaskPriority priority) => priority.Value;
}
