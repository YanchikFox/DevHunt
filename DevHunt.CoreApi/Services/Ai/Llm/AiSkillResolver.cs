using DevHunt.CoreApi.Services.Ai.Llm.Models;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Named skill a user can invoke at the start of an /ai message. The roster
/// is intentionally small: earlier drafts had Plan/Brainstorm/Review/Standup,
/// but they overlapped with general chat once project context is injected.
/// Add a skill only when it has a use case that plain "general + project
/// context" can't serve.
/// </summary>
public enum AiSkill
{
    /// <summary>No skill prefix: generic chat answer with project context.</summary>
    General,
    /// <summary>Recap of recent conversation messages.</summary>
    Summarize,
}

/// <summary>Bundles everything the chat service needs to know about a skill.</summary>
/// <param name="Skill">Resolved skill id.</param>
/// <param name="SystemPrompt">Base system prompt before project context is appended.</param>
/// <param name="DefaultUseHistory">Default history policy when the caller did not pin one.</param>
/// <param name="AllowedTools">Tool names exposed to this skill.</param>
public sealed record AiSkillProfile(
    AiSkill Skill,
    string SystemPrompt,
    bool DefaultUseHistory,
    IReadOnlyCollection<string> AllowedTools
);

/// <summary>
/// Parses leading skill markers and returns the matching <see cref="AiSkillProfile"/>.
/// </summary>
public interface IAiSkillResolver
{
    /// <summary>
    /// Parses a leading skill marker from the user message ("/summarize ...",
    /// "/plan ...") and returns the matched profile plus the cleaned user
    /// text with the marker stripped. Falls back to <see cref="AiSkill.General"/>
    /// when no marker is present.
    /// </summary>
    (AiSkillProfile Profile, string CleanedMessage) Resolve(string message);
}

/// <summary>
/// Maps <c>/summarize</c> and related markers to system prompts, history defaults, and allowed tools.
/// </summary>
public sealed class AiSkillResolver : IAiSkillResolver
{
    // Keep the marker map small + explicit; skill behaviour shouldn't depend
    // on locale-sensitive substring matches. Russian/Polish aliases route to
    // the same profile.
    private static readonly Dictionary<string, AiSkill> Markers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/summarize"] = AiSkill.Summarize,
        ["/summary"] = AiSkill.Summarize,
        ["/recap"] = AiSkill.Summarize,
        ["/резюме"] = AiSkill.Summarize,
        ["/итог"] = AiSkill.Summarize,
    };

    // General gets the full task-mutation tool surface; project context
    // injection makes the LLM competent enough to pick when to use them.
    // Summarize is read-only by design; record_decision is exposed so the
    // model can persist insights it actually surfaces, but no task tools.
    private static readonly HashSet<string> GeneralTools = new(StringComparer.Ordinal)
    {
        "create_task", "update_task", "move_task", "delete_task",
        "create_multiple_tasks", "move_multiple_tasks", "delete_multiple_tasks",
        "update_project",
        "record_decision", "read_document",
    };

    private static readonly HashSet<string> SummarizeTools = new(StringComparer.Ordinal)
    {
        "record_decision", "read_document",
    };

    private static readonly Dictionary<AiSkill, AiSkillProfile> Profiles = new()
    {
        [AiSkill.General] = new AiSkillProfile(
            AiSkill.General,
            "You are DevHunt AI Assistant inside a developer collaboration chat. " +
            "Answer concisely, preserve technical accuracy, and never claim access to messages that were not provided. " +
            "Use chat history only when it is relevant. Do not invent prior messages. " +
            "When the user clearly asks to mutate the project (create a task, move a card, update settings), " +
            "use the provided tools. When something material is decided in chat, call record_decision " +
            "with a one-sentence summary so future you remembers it. Read project documents on demand with read_document.",
            DefaultUseHistory: false,
            AllowedTools: GeneralTools),

        [AiSkill.Summarize] = new AiSkillProfile(
            AiSkill.Summarize,
            "You are DevHunt AI Assistant operating in summarize mode. " +
            "Produce a short, factual recap of the provided conversation history. " +
            "Group by topic, name speakers, surface decisions and open questions. " +
            "If a piece of context is missing, say so explicitly; do not invent. " +
            "When you identify a meaningful decision, call record_decision so it isn't lost.",
            DefaultUseHistory: true,
            AllowedTools: SummarizeTools),
    };

    /// <inheritdoc />
    public (AiSkillProfile Profile, string CleanedMessage) Resolve(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return (Profiles[AiSkill.General], message);
        }

        var trimmed = message.TrimStart();

        // Skill markers must be the very first whitespace-delimited token;
        // matching mid-message would let "fix the /summarize endpoint bug"
        // silently switch into summarize mode.
        var spaceIdx = trimmed.IndexOfAny(new[] { ' ', '\n', '\t' });
        var head = spaceIdx < 0 ? trimmed : trimmed[..spaceIdx];

        if (Markers.TryGetValue(head, out var skill))
        {
            var rest = spaceIdx < 0 ? string.Empty : trimmed[(spaceIdx + 1)..].TrimStart();
            return (Profiles[skill], rest);
        }

        return (Profiles[AiSkill.General], message);
    }
}
