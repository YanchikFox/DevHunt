namespace DevHunt.CoreApi.Services.Badges;

/// <summary>
/// Checks and awards achievements for a user after a domain action.
/// Defines awaitable achievement trigger checks; callers should await TriggerAchievementCheckAsync.
/// </summary>
public interface IAchievementTriggerService
{
    /// <summary>
    /// Check relevant achievement conditions and award any newly earned badges.
    /// </summary>
    Task CheckAndAwardAsync(Guid userId, AchievementTrigger trigger);
}
