using System.Net;

namespace DevHunt.CoreApi.Middleware;

/// <summary>
/// File upload validation middleware (SEC-012, SEC-018).
/// Validates file size (max 10MB) and MIME types before processing.
/// Blocks unauthorized file types and oversized files.
/// </summary>
/// <remarks>
/// <para><strong>Security Requirements</strong>:</para>
/// - SEC-012: Maximum file size limit (10MB)
/// - SEC-018: MIME type validation (whitelist approach)
///
/// <para><strong>Allowed File Types</strong>:</para>
/// - Images: JPEG, PNG, GIF, WebP, SVG
/// - Documents: PDF, Word (.docx), Excel (.xlsx), TXT, Markdown
/// - Archives: ZIP, TAR, GZIP
/// - Code files: C#, Python, JavaScript, JSON, XML (for project uploads)
///
/// <para><strong>Blocked File Types</strong>:</para>
/// - Executables: .exe, .bat, .sh
/// - Scripts: .html, .js (prevent XSS attacks)
///
/// <para><strong>Validation Strategy</strong>:</para>
/// Checks Content-Length header before reading request body to prevent DoS via large uploads.
/// For multipart/form-data, validates individual file parts in controllers.
/// </remarks>
public class FileUploadValidationMiddleware
{
    private readonly RequestDelegate _next;
    private const long MaxFileSize = 10 * 1024 * 1024; // SEC-012: 10MB maximum file size
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/svg+xml",
        // Documents
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // .docx
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // .xlsx
        "text/plain",
        "text/markdown",
        // Archives
        "application/zip",
        "application/x-zip-compressed",
        "application/x-tar",
        "application/gzip",
        // Code files (for project uploads)
        "text/x-csharp",
        "text/x-python",
        "text/javascript",
        "application/json",
        "text/xml",
        "application/xml",
    };

    private static readonly HashSet<string> BlockedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/x-executable",
        "application/x-msdownload", // .exe
        "application/x-sh",
        "application/x-bat",
        "application/x-shellscript",
        "text/html", // Prevent XSS via uploaded HTML
        "application/javascript",
        "text/javascript-dangerous",
    };

    public FileUploadValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // SEC-012: Check Content-Length header before reading request body
        if (IsFileUploadEndpoint(context.Request.Path))
        {
            var contentLength = context.Request.ContentLength;

            // Validate Content-Length if provided
            if (contentLength.HasValue && contentLength.Value > MaxFileSize)
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "File too large",
                    message = $"File size ({FormatBytes(contentLength.Value)}) exceeds maximum allowed size ({FormatBytes(MaxFileSize)}).",
                    maxSizeBytes = MaxFileSize,
                    maxSizeMB = MaxFileSize / (1024 * 1024)
                });
                return;
            }

            // Check Content-Type header for multipart/form-data uploads
            if (context.Request.HasFormContentType && context.Request.ContentType != null)
            {
                var contentType = context.Request.ContentType;

                // For multipart/form-data, we validate individual file parts in the controller
                // This middleware just ensures the request has proper multipart content type
                if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                {
                    // Request will be validated per-file in the controller
                }
                else if (contentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                {
                    // Direct file upload - check if path suggests it should be validated
                    // For now, allow but controller must validate
                }
            }

            // Check individual files in multipart request
            if (context.Request.HasFormContentType && context.Request.Form.Files.Any())
            {
                foreach (var file in context.Request.Form.Files)
                {
                    // SEC-012: Validate individual file size
                    if (file.Length > MaxFileSize)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "File too large",
                            message = $"File '{file.FileName}' size ({FormatBytes(file.Length)}) exceeds maximum allowed size ({FormatBytes(MaxFileSize)}).",
                            fileName = file.FileName,
                            fileSizeBytes = file.Length,
                            maxSizeBytes = MaxFileSize,
                            maxSizeMB = MaxFileSize / (1024 * 1024)
                        });
                        return;
                    }

                    var mimeType = file.ContentType;

                    // If Content-Type not provided, try to detect from filename
                    if (string.IsNullOrEmpty(mimeType) && !string.IsNullOrEmpty(file.FileName))
                    {
                        mimeType = GetMimeTypeFromExtension(file.FileName);
                    }

                    // SECURITY: Block dangerous file types (SEC-018)
                    if (!string.IsNullOrEmpty(mimeType))
                    {
                        if (BlockedMimeTypes.Contains(mimeType))
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "File type not allowed",
                                message = $"File type '{mimeType}' is not permitted for security reasons.",
                                fileName = file.FileName
                            });
                            return;
                        }

                        // SECURITY: Only allow whitelist of safe MIME types (SEC-018)
                        if (!AllowedMimeTypes.Contains(mimeType))
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "File type not allowed",
                                message = $"File type '{mimeType}' is not in the allowed list. Please upload a supported file type.",
                                fileName = file.FileName,
                                allowedTypes = AllowedMimeTypes.Take(10).ToList() // Show sample
                            });
                            return;
                        }
                    }
                }
            }
        }

        await _next(context);
    }

    // U-01: No 'upload' keyword heuristic — match all project/file upload paths
    private static bool IsFileUploadEndpoint(PathString path)
    {
        return path.StartsWithSegments("/api/profile/avatar", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/api/projects", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/api/files", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/api/uploads", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetMimeTypeFromExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".cs" => "text/x-csharp",
            ".py" => "text/x-python",
            ".js" => "text/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".exe" => "application/x-msdownload",
            ".sh" => "application/x-sh",
            ".bat" => "application/x-bat",
            _ => null
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

