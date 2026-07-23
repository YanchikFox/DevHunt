using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Security;

namespace DevHunt.CoreApi.Services.Projects;

/// <summary>
/// Serializes, validates, and resolves project news attachments stored as JSON on
/// <see cref="ProjectNewsPost.AttachmentsJson"/>.
/// </summary>
public static class ProjectNewsAttachmentHelper
{
    /// <summary>Combines distinct file id and external URL attachments into one list.</summary>
    public static List<ProjectNewsAttachmentDto> MergeAttachments(
        IEnumerable<Guid>? fileIds,
        IEnumerable<string>? urls)
    {
        var fileAttachments = (fileIds ?? Enumerable.Empty<Guid>())
            .Distinct()
            .Select(id => new ProjectNewsAttachmentDto("file", id, null));

        var urlAttachments = (urls ?? Enumerable.Empty<string>())
            .Distinct()
            .Select(url => new ProjectNewsAttachmentDto("url", null, url));

        return fileAttachments.Concat(urlAttachments).ToList();
    }

    /// <summary>Persists attachment DTOs as JSON for storage on the news post row.</summary>
    public static string SerializeAttachments(IEnumerable<ProjectNewsAttachmentDto> attachments)
        => JsonSerializer.Serialize(attachments);

    /// <summary>Parses stored JSON; returns an empty collection on blank or invalid input.</summary>
    public static IReadOnlyCollection<ProjectNewsAttachmentDto> DeserializeAttachments(string? attachmentsJson)
    {
        if (string.IsNullOrWhiteSpace(attachmentsJson))
            return Array.Empty<ProjectNewsAttachmentDto>();

        try
        {
            var attachments = JsonSerializer.Deserialize<List<ProjectNewsAttachmentDto>>(attachmentsJson);
            if (attachments == null) return Array.Empty<ProjectNewsAttachmentDto>();
            return attachments;
        }
        catch
        {
            return Array.Empty<ProjectNewsAttachmentDto>();
        }
    }

    /// <summary>
    /// Maps a stored attachment to an API response, resolving file metadata from a preloaded dictionary.
    /// </summary>
    public static ProjectNewsAttachmentResponse MapAttachmentToResponse(
        ProjectNewsAttachmentDto a,
        Guid projectId,
        Dictionary<Guid, ProjectFile> files)
    {
        if (a.Type == "file" && a.FileId.HasValue)
        {
            if (files.TryGetValue(a.FileId.Value, out var file))
            {
                return new ProjectNewsAttachmentResponse(
                    a.Type,
                    a.FileId,
                    null,
                    file.FileName,
                    file.ContentType,
                    $"/api/projects/{projectId}/files/{file.Id}");
            }
        }

        return new ProjectNewsAttachmentResponse(
            a.Type,
            a.FileId,
            a.Url,
            null,
            null,
            null);
    }

    /// <summary>Loads project files and maps each attachment to a client-facing response.</summary>
    public static async Task<ProjectNewsAttachmentResponse[]> BuildAttachmentResponsesAsync(
        DevHuntDbContext dbContext,
        Guid projectId,
        IEnumerable<ProjectNewsAttachmentDto> attachments)
    {
        var fileIds = attachments
            .Where(a => a.Type == "file" && a.FileId.HasValue)
            .Select(a => a.FileId!.Value)
            .Distinct()
            .ToList();

        var files = await dbContext.ProjectFiles
            .Where(pf => pf.ProjectId == projectId)
            .Where(pf => fileIds.Contains(pf.Id))
            .Where(pf => pf.DeletedAt == null)
            .ToDictionaryAsync(pf => pf.Id);

        return attachments
            .Select(a => MapAttachmentToResponse(a, projectId, files))
            .ToArray();
    }

    /// <summary>
    /// Validates URL attachments and ensures referenced project files exist and are not deleted.
    /// Returns an error message or null when valid.
    /// </summary>
    public static async Task<string?> ValidateAttachmentsAsync(
        DevHuntDbContext dbContext,
        Guid projectId,
        List<ProjectNewsAttachmentDto> attachments, CancellationToken ct = default)
    {
        if (attachments.Any(a => a.Type == "url" && !SecurityHelpers.IsValidUrl(a.Url)))
            return "One or more attachment URLs are invalid";

        var fileIds = attachments
            .Where(a => a.Type == "file" && a.FileId.HasValue)
            .Select(a => a.FileId!.Value)
            .Distinct()
            .ToList();

        if (fileIds.Count == 0)
            return null;

        var existingCount = await dbContext.ProjectFiles
            .Where(pf => pf.ProjectId == projectId)
            .Where(pf => fileIds.Contains(pf.Id))
            .Where(pf => pf.DeletedAt == null)
            .CountAsync(ct);

        return existingCount != fileIds.Count
            ? "One or more attachment files were not found"
            : null;
    }
}
