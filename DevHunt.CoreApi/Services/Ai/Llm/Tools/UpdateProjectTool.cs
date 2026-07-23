using System.Text.Json;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Implements <c>update_project</c>. Updates project metadata (description,
/// stack, status, visibility, difficulty, max team size). Authorization is
/// stricter than the task tools — task-level permissions aren't enough; we
/// require <c>CanEdit</c> which is owner / leader / admin only. Otherwise a
/// regular team member could ask the AI to change project visibility from
/// "public" to "private".
/// </summary>
public sealed class UpdateProjectTool : IAiTool
{
    private static readonly HashSet<string> AllowedDifficulty = new(StringComparer.OrdinalIgnoreCase)
    {
        "beginner", "intermediate", "advanced",
    };

    private readonly DevHuntDbContext _db;
    private readonly IProjectPermissionService _permissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateProjectTool"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="permissions">Project permission service for authorization checks.</param>
    public UpdateProjectTool(DevHuntDbContext db, IProjectPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public string Name => "update_project";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("update_project can only be invoked from a project conversation.");
        }

        var perms = await _permissions.GetPermissionsAsync(projectId, ctx.UserId, isAdmin: false, ct);
        if (perms is null) return Fail("Project not found.");
        if (!perms.CanEdit)
        {
            return Fail("You don't have permission to edit this project (owner/leader/admin only).");
        }

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project == null) return Fail("Project not found.");

        var changes = new List<string>();

        if (TryReadTrimmed(args, "description", 5000, out var description))
        {
            // SanitizeHtml is also applied on the human-edit path — keep AI in
            // line with the same XSS rule rather than carving an exception.
            project.Description = SecurityHelpers.SanitizeHtml(description);
            changes.Add("description");
        }

        if (TryReadTrimmed(args, "shortDescription", 500, out var shortDescription))
        {
            project.ShortDescription = string.IsNullOrWhiteSpace(shortDescription)
                ? null
                : SecurityHelpers.SanitizeHtml(shortDescription);
            changes.Add("shortDescription");
        }

        if (TryReadStringArray(args, "techStack", out var stack))
        {
            project.TechStack = stack;
            changes.Add("techStack");
        }

        if (TryReadTrimmed(args, "status", 32, out var statusRaw))
        {
            var status = ProjectStatus.FromString(statusRaw);
            if (status == null)
            {
                return Fail("Argument 'status' must be one of: draft, recruiting, active, completed, archived, cancelled.");
            }
            project.Status = status.Value;
            changes.Add("status");
        }

        if (TryReadTrimmed(args, "visibility", 32, out var visibilityRaw))
        {
            var visibility = ProjectVisibility.FromString(visibilityRaw);
            if (visibility == null)
            {
                return Fail("Argument 'visibility' must be one of: public, private, unlisted, members, subscribers.");
            }
            project.Visibility = visibility.Value;
            changes.Add("visibility");
        }

        if (TryReadTrimmed(args, "difficultyLevel", 32, out var difficulty))
        {
            if (!AllowedDifficulty.Contains(difficulty))
            {
                return Fail($"Argument 'difficultyLevel' must be one of: {string.Join(", ", AllowedDifficulty)}.");
            }
            project.DifficultyLevel = difficulty;
            changes.Add("difficultyLevel");
        }

        if (TryReadInt(args, "maxTeamSize", out var maxTeamSize))
        {
            if (maxTeamSize is int size && (size < 1 || size > 1000))
            {
                return Fail("Argument 'maxTeamSize' must be between 1 and 1000.");
            }
            project.MaxTeamSize = maxTeamSize;
            changes.Add("maxTeamSize");
        }

        if (changes.Count == 0)
        {
            return Fail("No updatable fields were provided.");
        }

        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var payload = new
        {
            projectId = project.Id,
            title = project.Title,
            status = project.Status,
            visibility = project.Visibility,
            changedFields = changes,
        };

        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(payload),
            UserFacingSummary: $"Updated project ({string.Join(", ", changes)}).");
    }

    // --- arg helpers -------------------------------------------------------

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);

    /// <summary>Reads an optional string property, trims it, and truncates it to the accepted length.</summary>
    private static bool TryReadTrimmed(JsonElement args, string name, int maxLength, out string value)
    {
        value = string.Empty;
        if (!args.TryGetProperty(name, out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Null) { value = string.Empty; return true; }
        if (prop.ValueKind != JsonValueKind.String) return false;
        var raw = prop.GetString() ?? string.Empty;
        var trimmed = raw.Trim();
        value = trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
        return true;
    }

    /// <summary>Reads an optional integer property, treating explicit null as a clear operation.</summary>
    private static bool TryReadInt(JsonElement args, string name, out int? value)
    {
        value = null;
        if (!args.TryGetProperty(name, out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Null) return true;
        if (prop.ValueKind != JsonValueKind.Number) return false;
        if (!prop.TryGetInt32(out var i)) return false;
        value = i;
        return true;
    }

    /// <summary>Reads a string array argument, dropping blanks and preserving order.</summary>
    private static bool TryReadStringArray(JsonElement args, string name, out List<string> value)
    {
        value = new List<string>();
        if (!args.TryGetProperty(name, out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Null) return true;
        if (prop.ValueKind != JsonValueKind.Array) return false;
        value = prop.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Take(50)
            .ToList();
        return true;
    }
}
