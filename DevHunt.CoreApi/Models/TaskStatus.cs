namespace DevHunt.CoreApi.Models;

public sealed class TaskStatus
{
    public string Value { get; }

    private TaskStatus(string value) => Value = value;

    public static readonly TaskStatus Todo = new("todo");
    public static readonly TaskStatus Doing = new("doing");
    // B09-02: "in_progress" is the value actually stored in the DB by legacy code paths
    public static readonly TaskStatus InProgress = new("in_progress");
    public static readonly TaskStatus Review = new("review");
    public static readonly TaskStatus Done = new("done");
    public static readonly TaskStatus Archived = new("archived");
    public static readonly TaskStatus Cancelled = new("cancelled");

    private static readonly Dictionary<string, TaskStatus> All = new(StringComparer.OrdinalIgnoreCase)
    {
        [Todo.Value] = Todo,
        [Doing.Value] = Doing,
        [InProgress.Value] = InProgress,
        [Review.Value] = Review,
        [Done.Value] = Done,
        [Archived.Value] = Archived,
        [Cancelled.Value] = Cancelled
    };

    public static TaskStatus FromString(string value) =>
        All.TryGetValue(value, out var status) ? status : throw new ArgumentException($"Invalid status: {value}");

    public static implicit operator string(TaskStatus status) => status.Value;
}
