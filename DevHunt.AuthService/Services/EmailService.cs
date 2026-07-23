using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Text.RegularExpressions;

namespace DevHunt.AuthService.Services;

/// <summary>
/// Security helper for log sanitization.
/// </summary>
internal static class LogSanitizer
{
    /// <summary>
    /// Sanitizes user input for safe logging to prevent log injection attacks.
    /// Removes newlines, carriage returns, and control characters.
    /// </summary>
    public static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        
        // Remove newlines, carriage returns, tabs, and other control characters
        return Regex.Replace(input, @"[\r\n\t\x00-\x1F\x7F]", "");
    }
}

/// <summary>
/// Email delivery contract for AuthService notifications.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an HTML email, or treats it as sent when SMTP host or port is not configured.
    /// </summary>
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
    /// <summary>
    /// Builds an email verification message with a code and frontend verification link.
    /// </summary>
    Task<bool> SendVerificationEmailAsync(string email, string code, Guid userId, string? locale = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Builds a password reset message with a URL-safe reset token link.
    /// </summary>
    Task<bool> SendPasswordResetEmailAsync(string email, string token, Guid userId, string? locale = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// SMTP email sender implemented with MailKit.
/// </summary>
public class MailKitEmailService : IEmailService
{
    private readonly ILogger<MailKitEmailService> _logger;
    private readonly IConfiguration _configuration;

    private readonly string? _smtpHost;
    private readonly int? _smtpPort;
    private readonly string? _smtpUser;
    private readonly string? _smtpPass;
    private readonly bool _useSsl;
    private readonly string _fromEmail;
    private readonly string _fromName;

    /// <summary>
    /// Reads SMTP settings and sender defaults from configuration.
    /// </summary>
    public MailKitEmailService(
        ILogger<MailKitEmailService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        _smtpHost = _configuration["Email:SmtpHost"];
        _smtpPort = _configuration.GetValue<int?>("Email:SmtpPort");
        _smtpUser = _configuration["Email:SmtpUser"] ?? _configuration["Email:SmtpUsername"];
        _smtpPass = _configuration["Email:SmtpPass"] ?? _configuration["Email:SmtpPassword"];
        _useSsl = _configuration.GetValue("Email:SmtpUseSsl", true);
        _fromEmail = _configuration["Email:FromEmail"] ?? "no-reply@devhunt.local";
        _fromName = _configuration["Email:FromName"] ?? "DevHunt";
    }

    /// <summary>
    /// Sends a MIME email through SMTP, authenticating when a user is configured; when SMTP is incomplete, logs sanitized mock details and returns success, and on send errors logs sanitized details and returns false.
    /// </summary>
    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpHost) || !_smtpPort.HasValue)
        {
            _logger.LogWarning("SMTP is not configured. Skipping email sending.");
            // SEC-LOG: Sanitize email and subject to prevent log injection
            _logger.LogInformation(
                "=== MOCK EMAIL TO {Email} === Subject: {Subject} ===", 
                LogSanitizer.Sanitize(toEmail), 
                LogSanitizer.Sanitize(subject));
            return true; // Return true so the flow continues as if email was sent
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = StripHtml(htmlBody)
        };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_smtpHost, _smtpPort.Value, _useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);

            if (!string.IsNullOrEmpty(_smtpUser))
            {
                await client.AuthenticateAsync(_smtpUser, _smtpPass, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            // SEC-LOG: Sanitize email to prevent log injection
            _logger.LogInformation("Verification email sent to {Email}", LogSanitizer.Sanitize(toEmail));
            return true;
        }
        catch (Exception ex)
        {
            // SEC-LOG: Sanitize user input to prevent log injection
            _logger.LogError(ex, "Failed to send email to {Email}.", LogSanitizer.Sanitize(toEmail));
            _logger.LogDebug(
                "Failed email details - To: {Email}, Subject: {Subject}", 
                LogSanitizer.Sanitize(toEmail), 
                LogSanitizer.Sanitize(subject));
            return false;
        }
    }

    /// <summary>
    /// Creates a verification email containing the provided code and `/verify-email` link, then delegates delivery to <see cref="SendEmailAsync"/>.
    /// </summary>
    public Task<bool> SendVerificationEmailAsync(string email, string code, Guid userId, string? locale = null, CancellationToken cancellationToken = default)
    {
        var subject = "Verify your DevHunt account";
        var frontendBase = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var verifyLink = $"{frontendBase.TrimEnd('/')}/verify-email?code={code}&userId={userId}";
        var html = $@"
            <div style=""font-family: Arial, sans-serif; color: #1f2937;"">
                <h2 style=""color:#111827;"">Verify your email</h2>
                <p>Use the code below to verify your email address:</p>
                <div style=""font-size:20px;font-weight:bold;letter-spacing:4px;background:#f3f4f6;padding:12px 16px;border-radius:6px;display:inline-block;"">{code}</div>
                <p style=""margin-top:12px;"">
                    Or click the link: <a href=""{verifyLink}"" target=""_blank"">Verify email</a>
                </p>
                <p style=""margin-top:12px;color:#6b7280;"">This code expires in 24 hours. If you didn't request this, please ignore this email.</p>
            </div>";
        return SendEmailAsync(email, subject, html, cancellationToken);
    }

    /// <summary>
    /// Creates a password reset email containing an escaped reset token link, then delegates delivery to <see cref="SendEmailAsync"/>.
    /// </summary>
    public Task<bool> SendPasswordResetEmailAsync(string email, string token, Guid userId, string? locale = null, CancellationToken cancellationToken = default)
    {
        var subject = "Reset your DevHunt password";
        var frontendBase = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var resetLink = $"{frontendBase.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";
        var html = $@"
            <div style=""font-family: Arial, sans-serif; color: #1f2937;"">
                <h2 style=""color:#111827;"">Reset your password</h2>
                <p>You requested to reset your password. Click the button below to set a new password:</p>
                <p style=""margin-top:16px;"">
                    <a href=""{resetLink}"" target=""_blank"" style=""background:#2563eb;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;display:inline-block;font-weight:600;"">Reset Password</a>
                </p>
                <p style=""margin-top:16px;color:#6b7280;font-size:14px;"">
                    Or copy this link: <span style=""word-break:break-all;"">{resetLink}</span>
                </p>
                <p style=""margin-top:16px;color:#6b7280;font-size:14px;"">
                    This link expires in 1 hour. If you didn't request a password reset, please ignore this email.
                </p>
            </div>";
        return SendEmailAsync(email, subject, html, cancellationToken);
    }

    /// <summary>
    /// Removes simple HTML tags to derive a plain-text body for SMTP clients.
    /// </summary>
    private static string StripHtml(string input)
    {
        return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
    }
}
