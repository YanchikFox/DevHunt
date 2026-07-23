namespace DevHunt.CoreApi.Models;

/// <summary>
/// Value object representing a project moderation action (hide, archive, feature, unfeature).
/// </summary>
public sealed record ProjectModerationAction
{
    public string Value { get; }

    private ProjectModerationAction(string value) => Value = value;

    public static readonly ProjectModerationAction Hide = new("hide");
    public static readonly ProjectModerationAction Archive = new("archive");
    public static readonly ProjectModerationAction Feature = new("feature");
    public static readonly ProjectModerationAction Unfeature = new("unfeature");

    public static implicit operator string(ProjectModerationAction action) => action.Value;

    public override string ToString() => Value;
}
