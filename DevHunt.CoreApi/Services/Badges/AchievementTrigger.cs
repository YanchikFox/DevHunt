namespace DevHunt.CoreApi.Services.Badges;

/// <summary>
/// Describes which domain action triggered the achievement check.
/// Each trigger maps to a set of relevant achievement codes.
/// </summary>
public enum AchievementTrigger
{
    // Projects
    ProjectCreated,
    ProjectCompleted,
    ProjectPublished,
    ProjectFeatured,
    ShowcasePublished,

    // Teamwork
    TeamJoined,
    TeamLeaderAssigned,

    // Social
    UserFollowed,
    ReviewCreated,
    ShowcaseCommentCreated,

    // Profile
    ProfileUpdated,
    AvatarUploaded,
    UserVerified,
    GithubConnected,

    // Activity
    TaskCompleted,
    MessageSent,

    // Special
    ProjectIssueReported,
    RoleChanged,

    // Moderation
    ReportProcessed,
    UserBlocked,
    AdminVerifiedUser,
    IssueResolved,
    TicketResolved,
    ProjectActioned,
    IssueEscalated,
}
