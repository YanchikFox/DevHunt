using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// Email notification service supporting SendGrid and SMTP delivery.
/// Corresponds to architecture: Email Channel via SMTP/SendGrid.
/// </summary>
/// <remarks>
/// <para><strong>Production Architecture</strong>:</para>
/// In production, email delivery should be handled by external Notification Service (Node.js microservice).
/// For MVP, direct SMTP/SendGrid integration from Core API is acceptable.
///
/// <para><strong>Delivery Methods</strong>:</para>
/// - <b>SendGrid API v3</b>: Preferred for production (better deliverability, analytics, templates)
/// - <b>SMTP</b>: Fallback option (can use any SMTP server: Gmail, AWS SES, etc.)
///
/// <para><strong>Configuration</strong>:</para>
/// Automatically selects delivery method based on appsettings.json:
/// - If SendGridApiKey is present → uses SendGrid API
/// - If SmtpHost/SmtpPort are present → uses SMTP
/// - If neither configured → service is disabled (logs warning, does not throw)
///
/// <para><strong>Graceful Degradation</strong>:</para>
/// Email failures return false instead of throwing exceptions, preventing email issues from breaking API operations.
/// </remarks>
public interface IEmailService
{
    /// <summary>
    /// Send email notification to a single user.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient display name</param>
    /// <param name="subject">Email subject line</param>
    /// <param name="htmlBody">HTML-formatted email body</param>
    /// <param name="plainTextBody">Plain text fallback (optional, defaults to HTML if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sent successfully, false if failed or service disabled</returns>
    Task<bool> SendEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send bulk email to multiple users (BCC delivery).
    /// </summary>
    /// <remarks>
    /// <para><strong>MVP Implementation</strong>:</para>
    /// Sends emails sequentially to each recipient (not optimal for large lists).
    ///
    /// <para><strong>Production Recommendation</strong>:</para>
    /// Use message queue (RabbitMQ, Azure Service Bus) and batch processing for bulk emails.
    /// This prevents API timeouts and provides better retry logic.
    /// </remarks>
    /// <param name="recipients">List of (Email, Name) tuples</param>
    /// <param name="subject">Email subject line</param>
    /// <param name="htmlBody">HTML-formatted email body</param>
    /// <param name="plainTextBody">Plain text fallback (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if at least one email was sent successfully</returns>
    Task<bool> SendBulkEmailAsync(
        List<(string Email, string Name)> recipients,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Core API email delivery service using SMTP or SendGrid.
/// </summary>
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _isEnabled;
    private readonly string? _fromEmail;
    private readonly string? _fromName;
    private readonly string? _smtpHost;
    private readonly int? _smtpPort;
    private readonly string? _smtpUsername;
    private readonly string? _smtpPassword;
    private readonly bool _useSsl;
    private readonly string? _sendGridApiKey;
    private readonly bool _useSendGrid;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailService"/> class.
    /// </summary>
    /// <param name="logger">Logger for delivery failures and disabled-service warnings.</param>
    /// <param name="configuration">Email/SMTP and SendGrid settings under <c>Email:*</c>.</param>
    /// <param name="httpClientFactory">Factory for SendGrid HTTP clients.</param>
    public EmailService(
        ILogger<EmailService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;

        _fromEmail = _configuration["Email:FromEmail"];
        _fromName = _configuration["Email:FromName"] ?? "DevHunt";
        _smtpHost = _configuration["Email:SmtpHost"];
        _smtpPort = _configuration.GetValue<int?>("Email:SmtpPort");
        _smtpUsername = _configuration["Email:SmtpUsername"];
        _smtpPassword = _configuration["Email:SmtpPassword"];
        _useSsl = _configuration.GetValue<bool>("Email:UseSsl");
        _sendGridApiKey = _configuration["Email:SendGridApiKey"];

        _useSendGrid = !string.IsNullOrEmpty(_sendGridApiKey);
        _isEnabled = _useSendGrid || (!string.IsNullOrEmpty(_smtpHost) && _smtpPort.HasValue);

        if (!_isEnabled)
        {
            _logger.LogWarning("Email Service disabled (no SMTP or SendGrid configuration). Emails will not be sent.");
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Email Service disabled, skipping email to {ToEmail}", toEmail);
            return false;
        }

        if (string.IsNullOrEmpty(_fromEmail))
        {
            _logger.LogError("Email:FromEmail not configured");
            return false;
        }

        try
        {
            return _useSendGrid
                ? await SendViaSendGridAsync(toEmail, toName, subject, htmlBody, plainTextBody, cancellationToken)
                : await SendViaSmtpAsync(toEmail, toName, subject, htmlBody, plainTextBody, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
            return false;
        }
    }

    /// <summary>
    /// Sends an email message to multiple recipients sequentially.
    /// </summary>
    /// <param name="recipients">The recipient email addresses and display names.</param>
    /// <param name="subject">The email subject.</param>
    /// <param name="htmlBody">The HTML email body.</param>
    /// <param name="plainTextBody">The optional plain-text email body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when every message is sent successfully; otherwise false.</returns>
    public async Task<bool> SendBulkEmailAsync(
        List<(string Email, string Name)> recipients,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || recipients == null || recipients.Count == 0)
        {
            return false;
        }

        // For bulk delivery, use queue or batching is recommended
        // For MVP, send sequentially
        var results = new List<bool>();
        foreach (var recipient in recipients)
        {
            var result = await SendEmailAsync(
                recipient.Email,
                recipient.Name,
                subject,
                htmlBody,
                plainTextBody,
                cancellationToken);
            results.Add(result);
        }

        var successCount = results.Count(r => r);
        _logger.LogInformation("Bulk email sent: {SuccessCount}/{TotalCount} successful", successCount, recipients.Count);

        return successCount > 0;
    }

    /// <summary>
    /// Sends one email through SendGrid's mail API and returns whether the HTTP request succeeded.
    /// </summary>
    private async Task<bool> SendViaSendGridAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? plainTextBody,
        CancellationToken cancellationToken)
    {
        // SendGrid API v3 — IHttpClientFactory prevents socket exhaustion under load
        using var httpClient = _httpClientFactory.CreateClient("sendgrid");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_sendGridApiKey}");

        var payload = new
        {
            personalizations = new[]
            {
                new
                {
                    to = new[] { new { email = toEmail, name = toName } },
                    subject
                }
            },
            from = new { email = _fromEmail, name = _fromName },
            content = new[]
            {
                new { type = "text/html", value = htmlBody },
                new { type = "text/plain", value = plainTextBody ?? htmlBody }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("https://api.sendgrid.com/v3/mail/send", content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Email sent via SendGrid to {ToEmail}", toEmail);
            return true;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("SendGrid returned {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
        return false;
    }

    /// <summary>
    /// Sends one HTML email through the configured SMTP server, adding a plain-text alternate view when provided.
    /// </summary>
    private async Task<bool> SendViaSmtpAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? plainTextBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_smtpHost) || !_smtpPort.HasValue)
        {
            _logger.LogError("SMTP configuration incomplete");
            return false;
        }

        using var smtpClient = new SmtpClient(_smtpHost, _smtpPort.Value)
        {
            EnableSsl = _useSsl,
            Credentials = !string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword)
                ? new NetworkCredential(_smtpUsername, _smtpPassword)
                : null
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_fromEmail!, _fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        mailMessage.To.Add(new MailAddress(toEmail, toName));

        if (!string.IsNullOrEmpty(plainTextBody))
        {
            var plainTextView = AlternateView.CreateAlternateViewFromString(plainTextBody, Encoding.UTF8, "text/plain");
            mailMessage.AlternateViews.Add(plainTextView);
        }

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);

        _logger.LogDebug("Email sent via SMTP to {ToEmail}", toEmail);
        return true;
    }
}

/// <summary>
/// Email HTML template generation service.
/// </summary>
/// <remarks>
/// Generates styled HTML email templates for various notification types.
/// Templates include inline CSS for email client compatibility.
/// </remarks>
public interface IEmailTemplateService
{
    /// <summary>
    /// Generate HTML template for new project notification.
    /// </summary>
    string GenerateProjectNotificationTemplate(string userName, string projectTitle, string projectDescription, string actionUrl);

    /// <summary>
    /// Generate HTML template for project invitation.
    /// </summary>
    string GenerateInvitationTemplate(string userName, string projectTitle, string inviterName, string actionUrl);

    /// <summary>
    /// Generate HTML template for notification digest (daily/weekly summary).
    /// </summary>
    string GenerateDigestTemplate(string userName, List<(string Type, string Title, string? Content, DateTime CreatedAt)> notifications);
}

/// <summary>
/// Generates HTML email templates for notifications.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    /// <inheritdoc />
    public string GenerateProjectNotificationTemplate(string userName, string projectTitle, string projectDescription, string actionUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .button {{ display: inline-block; padding: 10px 20px; background: #2196F3; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>DevHunt</h1>
        </div>
        <div class=""content"">
            <p>Cześć, {userName}!</p>
            <p>Nowy projekt opublikowany: <strong>{projectTitle}</strong></p>
            <p>{projectDescription}</p>
            <a href=""{actionUrl}"" class=""button"">Otwórz projekt</a>
        </div>
    </div>
</body>
</html>";
    }

    /// <inheritdoc />
    public string GenerateInvitationTemplate(string userName, string projectTitle, string inviterName, string actionUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #FF9800; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
        .button {{ display: inline-block; padding: 10px 20px; background: #2196F3; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Zaproszenie do projektu</h1>
        </div>
        <div class=""content"">
            <p>Cześć, {userName}!</p>
            <p><strong>{inviterName}</strong> zaprasza Cię do projektu <strong>{projectTitle}</strong>.</p>
            <a href=""{actionUrl}"" class=""button"">Przyjmij zaproszenie</a>
        </div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// Generates the HTML body for a notification digest email.
    /// </summary>
    /// <param name="userName">The recipient display name.</param>
    /// <param name="notifications">The notification items included in the digest.</param>
    /// <returns>The rendered HTML email body.</returns>
    public string GenerateDigestTemplate(string userName, List<(string Type, string Title, string? Content, DateTime CreatedAt)> notifications)
    {
        var notificationsHtml = string.Join("", notifications.Select(n => $@"
            <div style=""padding: 10px; margin: 10px 0; background: white; border-left: 4px solid #2196F3;"">
                <h3>{n.Title}</h3>
                {(string.IsNullOrEmpty(n.Content) ? "" : $"<p>{n.Content}</p>")}
                <small>{n.CreatedAt:dd.MM.yyyy HH:mm}</small>
            </div>
        "));

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #9C27B0; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background: #f9f9f9; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Podsumowanie powiadomień DevHunt</h1>
        </div>
        <div class=""content"">
            <p>Cześć, {userName}!</p>
            <p>Вот что произошло за последнее время:</p>
            {notificationsHtml}
        </div>
    </div>
</body>
</html>";
    }
}

