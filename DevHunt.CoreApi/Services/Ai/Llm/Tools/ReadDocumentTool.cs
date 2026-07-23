using System.Text.Json;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm.Tools;

/// <summary>
/// Implements <c>read_document</c>. Returns the full body of a project doc
/// the LLM has already seen listed in the project-context block. The doc
/// must belong to the conversation's project. The LLM cannot smuggle in a
/// document id from another project, and we never trust the id at face
/// value.
/// </summary>
public sealed class ReadDocumentTool : IAiTool
{
    // Hard ceiling so a 500 KB design doc doesn't get pasted into the next
    // LLM turn. The model can read_document again for a different section
    // if it really needs more.
    private const int MaxContentChars = 12_000;
    private const int MaxAvailableTitlesInError = 12;

    private readonly DevHuntDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadDocumentTool"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    public ReadDocumentTool(DevHuntDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public string Name => "read_document";

    /// <inheritdoc />
    public async Task<AiToolResult> ExecuteAsync(JsonElement args, AiToolExecutionContext ctx, CancellationToken ct)
    {
        if (ctx.ProjectId is not Guid projectId)
        {
            return Fail("read_document can only be invoked from a project conversation.");
        }

        var rawId = TryGetString(args, "documentId");
        var rawTitle = TryGetString(args, "title");

        if (string.IsNullOrWhiteSpace(rawId) && string.IsNullOrWhiteSpace(rawTitle))
        {
            return Fail("Provide either 'documentId' (UUID) or 'title' (case-insensitive document name).");
        }

        // Pull all candidates for this project once. Caps in ProjectContextBuilder
        // already keep doc counts modest; full list keeps lookup logic simple
        // and avoids a second round-trip when title resolution fails.
        var candidates = await _db.ProjectDocuments
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && d.DeletedAt == null)
            .Select(d => new DocumentRow(d.Id, d.Title, d.Content, d.DocumentType, d.ContentFormat))
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return Fail("This project has no documents.");
        }

        var match = ResolveDocument(candidates, rawId, rawTitle, out var ambiguityHint);
        if (match == null)
        {
            var available = string.Join(", ", candidates.Take(MaxAvailableTitlesInError).Select(c => $"\"{c.Title}\""));
            var suffix = candidates.Count > MaxAvailableTitlesInError ? "…" : string.Empty;
            var hint = ambiguityHint ?? $"Document not found. Available: {available}{suffix}.";
            return Fail(hint);
        }

        var body = match.Content ?? string.Empty;
        var truncated = body.Length > MaxContentChars;
        if (truncated) body = body[..MaxContentChars];

        var payload = new
        {
            id = match.Id,
            title = match.Title,
            type = match.DocumentType,
            format = match.ContentFormat,
            content = body,
            truncated,
        };

        return new AiToolResult(
            Success: true,
            ResultJson: JsonSerializer.Serialize(payload),
            UserFacingSummary: $"Read document \"{match.Title}\"{(truncated ? " (truncated)" : string.Empty)}.");
    }

    /// <summary>
    /// Resolution order: explicit UUID → exact (case-insensitive) title match →
    /// case-insensitive starts-with → contains. If a fuzzy step yields more
    /// than one hit we refuse and surface the candidates so the model can ask
    /// the user to disambiguate, rather than picking the wrong file.
    /// </summary>
    private static DocumentRow? ResolveDocument(
        IReadOnlyList<DocumentRow> candidates,
        string? rawId,
        string? rawTitle,
        out string? ambiguityHint)
    {
        ambiguityHint = null;

        if (!string.IsNullOrWhiteSpace(rawId) && Guid.TryParse(rawId, out var documentId))
        {
            return candidates.FirstOrDefault(c => c.Id == documentId);
        }

        if (string.IsNullOrWhiteSpace(rawTitle)) return null;

        var needle = rawTitle.Trim();

        var exact = candidates.Where(c => string.Equals(c.Title, needle, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1) return exact[0];
        if (exact.Count > 1)
        {
            ambiguityHint = BuildAmbiguityHint(needle, exact);
            return null;
        }

        var startsWith = candidates.Where(c => c.Title.StartsWith(needle, StringComparison.OrdinalIgnoreCase)).ToList();
        if (startsWith.Count == 1) return startsWith[0];
        if (startsWith.Count > 1)
        {
            ambiguityHint = BuildAmbiguityHint(needle, startsWith);
            return null;
        }

        var contains = candidates.Where(c => c.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
        if (contains.Count == 1) return contains[0];
        if (contains.Count > 1)
        {
            ambiguityHint = BuildAmbiguityHint(needle, contains);
            return null;
        }

        return null;
    }

    /// <summary>Builds a disambiguation message listing matching document titles and ids.</summary>
    private static string BuildAmbiguityHint(string needle, IReadOnlyList<DocumentRow> matches)
    {
        var listed = string.Join(", ", matches.Take(MaxAvailableTitlesInError).Select(m => $"\"{m.Title}\" ({m.Id})"));
        return $"Title '{needle}' matches multiple documents: {listed}. Re-call with documentId.";
    }

    /// <summary>Reads a string argument property when present.</summary>
    private static string? TryGetString(JsonElement args, string property)
    {
        if (!args.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;
        return value.GetString();
    }

    /// <summary>Returns a failed tool result with a compact JSON error payload.</summary>
    private static AiToolResult Fail(string message) => new(
        Success: false,
        ResultJson: JsonSerializer.Serialize(new { error = message }),
        ErrorMessage: message);

    /// <summary>Projected document fields used for tool-side lookup and response payloads.</summary>
    private sealed record DocumentRow(Guid Id, string Title, string? Content, string? DocumentType, string? ContentFormat);
}
