namespace DevHunt.Infrastructure.Constants;

/// <summary>
/// Project status values persisted for DevHunt project records.
/// </summary>
public static class ProjectStatus
{
    /// <summary>Status for a project that is still being prepared.</summary>
    public const string Draft = "draft";
    /// <summary>Status for a project that is looking for contributors.</summary>
    public const string Recruiting = "recruiting";
    /// <summary>Status for a project currently in progress.</summary>
    public const string Active = "active";
    /// <summary>Status for a project that has finished.</summary>
    public const string Completed = "completed";
    /// <summary>Status for a project hidden from normal active project flows.</summary>
    public const string Archived = "archived";
    /// <summary>Status for a project that was stopped before completion.</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>
/// Team member status values persisted for membership records.
/// </summary>
public static class TeamMemberStatus
{
    /// <summary>Status for a current project team member.</summary>
    public const string Active = "active";
    /// <summary>Status for a project team member who is not currently active.</summary>
    public const string Inactive = "inactive";
    /// <summary>Status for a project team member who left the team.</summary>
    public const string Left = "left";
}

/// <summary>
/// Visibility values used to control project discovery and access.
/// </summary>
public static class ProjectVisibility
{
    /// <summary>Visibility for projects discoverable by everyone.</summary>
    public const string Public = "public";
    /// <summary>Visibility for projects restricted from public discovery.</summary>
    public const string Private = "private";
    /// <summary>Visibility for projects reachable by link but not listed publicly.</summary>
    public const string Unlisted = "unlisted";
}

/// <summary>
/// Moderation report status values used for admin review queues.
/// </summary>
public static class ModerationReportStatus
{
    /// <summary>Status for a moderation report awaiting review.</summary>
    public const string Pending = "pending";
    /// <summary>Status for a moderation report that has been handled.</summary>
    public const string Resolved = "resolved";
    /// <summary>Status for a moderation report closed without action.</summary>
    public const string Dismissed = "dismissed";
}

/// <summary>
/// Target type values used to identify what a moderation report references.
/// </summary>
public static class ReportTargetType
{
    /// <summary>Report target type for a user profile.</summary>
    public const string User = "user";
    /// <summary>Report target type for a project.</summary>
    public const string Project = "project";
    /// <summary>Report target type for a chat message.</summary>
    public const string Message = "message";
    /// <summary>Report target type for a project news post.</summary>
    public const string NewsPost = "news_post";
    /// <summary>Report target type for a news post comment.</summary>
    public const string NewsComment = "news_comment";
    /// <summary>Report target type for a showcase project comment.</summary>
    public const string ShowcaseComment = "showcase_comment";
    /// <summary>Report target type for a community post.</summary>
    public const string CommunityPost = "community_post";
    /// <summary>Report target type for a project task.</summary>
    public const string Task = "task";
    /// <summary>Report target type for an uploaded image.</summary>
    public const string Image = "image";
}

/// <summary>
/// Feedback status values persisted for community feedback items.
/// </summary>
public static class FeedbackStatus
{
    /// <summary>Status for feedback that is open for triage.</summary>
    public const string Open = "open";
    /// <summary>Status for feedback currently being evaluated.</summary>
    public const string UnderReview = "under_review";
    /// <summary>Status for feedback accepted for future work.</summary>
    public const string Planned = "planned";
    /// <summary>Status for feedback actively being worked on.</summary>
    public const string InProgress = "in_progress";
    /// <summary>Status for feedback that has been delivered.</summary>
    public const string Completed = "completed";
    /// <summary>Status for feedback declined by the team.</summary>
    public const string Rejected = "rejected";
    /// <summary>Status for feedback that duplicates another item.</summary>
    public const string Duplicate = "duplicate";
}
