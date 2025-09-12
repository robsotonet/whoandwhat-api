using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Infrastructure.Configuration;

namespace WhoAndWhat.Infrastructure.Services;

/// <summary>
/// Email service implementation using MailKit for sending emails
/// Follows 2025 best practices with async/await, proper error handling, and security
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> SendPasswordResetEmailAsync(string email, string username, string resetToken, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled)
        {
            _logger.LogInformation("Email sending is disabled. Skipping password reset email to {Email}", email);
            return true; // Return true to not break the flow when emails are disabled
        }

        var resetUrl = $"{_emailSettings.BaseUrl}/reset-password?token={resetToken}&email={email}";
        var subject = "Reset Your Password - WhoAndWhat";

        var htmlContent = GeneratePasswordResetEmailHtml(username, resetUrl, _emailSettings.Templates.PasswordResetExpirationHours);
        var plainTextContent = GeneratePasswordResetEmailPlainText(username, resetUrl, _emailSettings.Templates.PasswordResetExpirationHours);

        return await SendEmailAsync(email, subject, htmlContent, plainTextContent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendEmailVerificationAsync(string email, string username, string verificationToken, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled)
        {
            _logger.LogInformation("Email sending is disabled. Skipping email verification to {Email}", email);
            return true;
        }

        var verificationUrl = $"{_emailSettings.BaseUrl}/verify-email?token={verificationToken}&userId={userId}";
        var subject = "Verify Your Email Address - WhoAndWhat";

        var htmlContent = GenerateEmailVerificationEmailHtml(username, verificationUrl, _emailSettings.Templates.EmailVerificationExpirationHours);
        var plainTextContent = GenerateEmailVerificationEmailPlainText(username, verificationUrl, _emailSettings.Templates.EmailVerificationExpirationHours);

        return await SendEmailAsync(email, subject, htmlContent, plainTextContent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendWelcomeEmailAsync(string email, string username, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled)
        {
            _logger.LogInformation("Email sending is disabled. Skipping welcome email to {Email}", email);
            return true;
        }

        var subject = $"Welcome to WhoAndWhat, {username}!";

        var htmlContent = GenerateWelcomeEmailHtml(username);
        var plainTextContent = GenerateWelcomeEmailPlainText(username);

        return await SendEmailAsync(email, subject, htmlContent, plainTextContent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendAccountLockedEmailAsync(string email, string username, DateTime lockedUntil, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled)
        {
            _logger.LogInformation("Email sending is disabled. Skipping account locked email to {Email}", email);
            return true;
        }

        var subject = "Account Security Alert - WhoAndWhat";

        var htmlContent = GenerateAccountLockedEmailHtml(username, lockedUntil);
        var plainTextContent = GenerateAccountLockedEmailPlainText(username, lockedUntil);

        return await SendEmailAsync(email, subject, htmlContent, plainTextContent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendPasswordChangedEmailAsync(string email, string username, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled)
        {
            _logger.LogInformation("Email sending is disabled. Skipping password changed email to {Email}", email);
            return true;
        }

        var subject = "Password Changed - WhoAndWhat";

        var htmlContent = GeneratePasswordChangedEmailHtml(username);
        var plainTextContent = GeneratePasswordChangedEmailPlainText(username);

        return await SendEmailAsync(email, subject, htmlContent, plainTextContent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendEmailAsync(string to, string subject, string htmlContent, string? plainTextContent = null, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled)
        {
            _logger.LogInformation("Email sending is disabled. Skipping email to {Email} with subject {Subject}", to, subject);
            return true;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = htmlContent;

            if (!string.IsNullOrEmpty(plainTextContent))
            {
                bodyBuilder.TextBody = plainTextContent;
            }

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Set timeout
            client.Timeout = _emailSettings.TimeoutMs;

            // Connect to SMTP server
            var secureSocketOptions = _emailSettings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, secureSocketOptions, cancellationToken);

            // Authenticate if required
            if (_emailSettings.UseAuthentication)
            {
                await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password, cancellationToken);
            }

            // Send email
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Email} with subject: {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} with subject: {Subject}", to, subject);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsEmailServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled)
        {
            return false;
        }

        try
        {
            using var client = new SmtpClient();
            client.Timeout = 10000; // 10 seconds for health check

            var secureSocketOptions = _emailSettings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, secureSocketOptions, cancellationToken);

            if (_emailSettings.UseAuthentication)
            {
                await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password, cancellationToken);
            }

            await client.DisconnectAsync(true, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email service health check failed");
            return false;
        }
    }

    #region Email Template Generators

    private string GeneratePasswordResetEmailHtml(string username, string resetUrl, int expirationHours)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Password Reset</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; }}
        .button {{ display: inline-block; background-color: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; margin: 20px 0; }}
        .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; border-radius: 4px; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; color: #6c757d; font-size: 12px; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>🔐 Password Reset Request</h1>
    </div>
    <div class='content'>
        <p>Hello {username},</p>
        
        <p>We received a request to reset your password for your WhoAndWhat account. If you made this request, click the button below to reset your password:</p>
        
        <div style='text-align: center;'>
            <a href='{resetUrl}' class='button'>Reset My Password</a>
        </div>
        
        <div class='warning'>
            <strong>⏰ Important:</strong> This link will expire in {expirationHours} hour{(expirationHours != 1 ? "s" : "")}.
        </div>
        
        <p>If you didn't request a password reset, you can safely ignore this email. Your password won't be changed.</p>
        
        <p>For security reasons, if you continue to receive these emails without requesting them, please contact our support team.</p>
        
        <p>Best regards,<br>
        The WhoAndWhat Team</p>
    </div>
    <div class='footer'>
        <p>This email was sent to you because a password reset was requested for your account.</p>
        <p>If you need help, contact us at {_emailSettings.Templates.SupportEmail}</p>
    </div>
</body>
</html>";
    }

    private string GeneratePasswordResetEmailPlainText(string username, string resetUrl, int expirationHours)
    {
        return $@"Password Reset Request

Hello {username},

We received a request to reset your password for your WhoAndWhat account. 

To reset your password, please visit this link:
{resetUrl}

Important: This link will expire in {expirationHours} hour{(expirationHours != 1 ? "s" : "")}.

If you didn't request a password reset, you can safely ignore this email. Your password won't be changed.

Best regards,
The WhoAndWhat Team

---
If you need help, contact us at {_emailSettings.Templates.SupportEmail}";
    }

    private string GenerateEmailVerificationEmailHtml(string username, string verificationUrl, int expirationHours)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Verify Your Email</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; }}
        .button {{ display: inline-block; background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; margin: 20px 0; }}
        .info {{ background-color: #d1ecf1; border: 1px solid #bee5eb; border-radius: 4px; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; color: #6c757d; font-size: 12px; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>📧 Verify Your Email Address</h1>
    </div>
    <div class='content'>
        <p>Hello {username},</p>
        
        <p>Welcome to WhoAndWhat! To complete your account setup and ensure you can receive important notifications, please verify your email address by clicking the button below:</p>
        
        <div style='text-align: center;'>
            <a href='{verificationUrl}' class='button'>Verify My Email</a>
        </div>
        
        <div class='info'>
            <strong>ℹ️ Note:</strong> This verification link will expire in {expirationHours} hours.
        </div>
        
        <p>Once verified, you'll be able to:</p>
        <ul>
            <li>Receive password reset emails</li>
            <li>Get important account notifications</li>
            <li>Access all WhoAndWhat features</li>
        </ul>
        
        <p>If you didn't create an account with WhoAndWhat, you can safely ignore this email.</p>
        
        <p>Best regards,<br>
        The WhoAndWhat Team</p>
    </div>
    <div class='footer'>
        <p>This email was sent because you registered for a WhoAndWhat account.</p>
        <p>If you need help, contact us at {_emailSettings.Templates.SupportEmail}</p>
    </div>
</body>
</html>";
    }

    private string GenerateEmailVerificationEmailPlainText(string username, string verificationUrl, int expirationHours)
    {
        return $@"Verify Your Email Address

Hello {username},

Welcome to WhoAndWhat! Please verify your email address by visiting this link:
{verificationUrl}

This verification link will expire in {expirationHours} hours.

Once verified, you'll be able to receive password reset emails, account notifications, and access all WhoAndWhat features.

If you didn't create an account with WhoAndWhat, you can safely ignore this email.

Best regards,
The WhoAndWhat Team

---
If you need help, contact us at {_emailSettings.Templates.SupportEmail}";
    }

    private string GenerateWelcomeEmailHtml(string username)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Welcome to WhoAndWhat</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #6f42c1; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; }}
        .feature {{ background-color: white; margin: 15px 0; padding: 20px; border-radius: 5px; border-left: 4px solid #6f42c1; }}
        .footer {{ text-align: center; color: #6c757d; font-size: 12px; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>🎉 Welcome to WhoAndWhat!</h1>
    </div>
    <div class='content'>
        <p>Hello {username},</p>
        
        <p>Welcome to <strong>WhoAndWhat</strong> - your smart, bilingual task management platform! We're excited to have you on board.</p>
        
        <h3>🚀 Getting Started</h3>
        <p>Here's what you can do with WhoAndWhat:</p>
        
        <div class='feature'>
            <h4>📋 Smart Task Management</h4>
            <p>Organize your tasks by categories: To-Dos, Ideas, Appointments, Bill Reminders, and Projects.</p>
        </div>
        
        <div class='feature'>
            <h4>🤝 Social Connectivity</h4>
            <p>Link contacts to tasks, share responsibilities, and collaborate seamlessly.</p>
        </div>
        
        <div class='feature'>
            <h4>🧠 AI-Powered Planning</h4>
            <p>Get intelligent suggestions and automated planning to boost your productivity.</p>
        </div>
        
        <div class='feature'>
            <h4>🌍 Bilingual Support</h4>
            <p>Switch between English and Spanish to work in your preferred language.</p>
        </div>
        
        <p>Ready to get started? Log in to your account and begin organizing your tasks like never before!</p>
        
        <p>If you have any questions, don't hesitate to reach out to our support team.</p>
        
        <p>Best regards,<br>
        The WhoAndWhat Team</p>
    </div>
    <div class='footer'>
        <p>Thank you for choosing WhoAndWhat for your task management needs.</p>
        <p>Need help? Contact us at {_emailSettings.Templates.SupportEmail}</p>
    </div>
</body>
</html>";
    }

    private string GenerateWelcomeEmailPlainText(string username)
    {
        return $@"Welcome to WhoAndWhat!

Hello {username},

Welcome to WhoAndWhat - your smart, bilingual task management platform! We're excited to have you on board.

Getting Started:
Here's what you can do with WhoAndWhat:

📋 Smart Task Management
Organize your tasks by categories: To-Dos, Ideas, Appointments, Bill Reminders, and Projects.

🤝 Social Connectivity  
Link contacts to tasks, share responsibilities, and collaborate seamlessly.

🧠 AI-Powered Planning
Get intelligent suggestions and automated planning to boost your productivity.

🌍 Bilingual Support
Switch between English and Spanish to work in your preferred language.

Ready to get started? Log in to your account and begin organizing your tasks like never before!

If you have any questions, don't hesitate to reach out to our support team.

Best regards,
The WhoAndWhat Team

---
Need help? Contact us at {_emailSettings.Templates.SupportEmail}";
    }

    private string GenerateAccountLockedEmailHtml(string username, DateTime lockedUntil)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Account Security Alert</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; }}
        .alert {{ background-color: #f8d7da; border: 1px solid #f5c6cb; border-radius: 4px; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; color: #6c757d; font-size: 12px; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>🔒 Account Security Alert</h1>
    </div>
    <div class='content'>
        <p>Hello {username},</p>
        
        <p>We're writing to inform you that your WhoAndWhat account has been temporarily locked due to multiple failed login attempts.</p>
        
        <div class='alert'>
            <strong>Account Status:</strong> Locked until {lockedUntil:yyyy-MM-dd HH:mm:ss} UTC
        </div>
        
        <h3>What happened?</h3>
        <p>Your account was locked after 5 consecutive failed login attempts. This is a security measure to protect your account from unauthorized access.</p>
        
        <h3>What can you do?</h3>
        <ul>
            <li>Wait until the lock expires and try logging in again</li>
            <li>If you forgot your password, use the ""Forgot Password"" link to reset it</li>
            <li>If you suspect unauthorized access, contact our support team immediately</li>
        </ul>
        
        <p>If this wasn't you trying to access your account, please contact our support team as soon as possible.</p>
        
        <p>Best regards,<br>
        The WhoAndWhat Security Team</p>
    </div>
    <div class='footer'>
        <p>This is an automated security notification for your WhoAndWhat account.</p>
        <p>For urgent security concerns, contact us at {_emailSettings.Templates.SupportEmail}</p>
    </div>
</body>
</html>";
    }

    private string GenerateAccountLockedEmailPlainText(string username, DateTime lockedUntil)
    {
        return $@"Account Security Alert

Hello {username},

Your WhoAndWhat account has been temporarily locked due to multiple failed login attempts.

Account Status: Locked until {lockedUntil:yyyy-MM-dd HH:mm:ss} UTC

What happened?
Your account was locked after 5 consecutive failed login attempts. This is a security measure to protect your account.

What can you do?
- Wait until the lock expires and try logging in again
- If you forgot your password, use the ""Forgot Password"" link to reset it  
- If you suspect unauthorized access, contact our support team immediately

Best regards,
The WhoAndWhat Security Team

---
For urgent security concerns, contact us at {_emailSettings.Templates.SupportEmail}";
    }

    private string GeneratePasswordChangedEmailHtml(string username)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Password Changed</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; }}
        .success {{ background-color: #d4edda; border: 1px solid #c3e6cb; border-radius: 4px; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; color: #6c757d; font-size: 12px; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>✅ Password Changed Successfully</h1>
    </div>
    <div class='content'>
        <p>Hello {username},</p>
        
        <div class='success'>
            <strong>✅ Confirmed:</strong> Your password has been successfully changed.
        </div>
        
        <p>This email confirms that your WhoAndWhat account password was changed on {DateTime.UtcNow:yyyy-MM-dd} at {DateTime.UtcNow:HH:mm:ss} UTC.</p>
        
        <h3>Security Tips</h3>
        <ul>
            <li>Keep your new password secure and don't share it with anyone</li>
            <li>Use a unique password that you don't use for other accounts</li>
            <li>Consider using a password manager for better security</li>
        </ul>
        
        <p><strong>Didn't change your password?</strong> If you didn't make this change, please contact our support team immediately and consider that your account may have been compromised.</p>
        
        <p>Best regards,<br>
        The WhoAndWhat Security Team</p>
    </div>
    <div class='footer'>
        <p>This is a security notification for your WhoAndWhat account.</p>
        <p>For security concerns, contact us at {_emailSettings.Templates.SupportEmail}</p>
    </div>
</body>
</html>";
    }

    private string GeneratePasswordChangedEmailPlainText(string username)
    {
        return $@"Password Changed Successfully

Hello {username},

Your WhoAndWhat account password was successfully changed on {DateTime.UtcNow:yyyy-MM-dd} at {DateTime.UtcNow:HH:mm:ss} UTC.

Security Tips:
- Keep your new password secure and don't share it with anyone
- Use a unique password that you don't use for other accounts  
- Consider using a password manager for better security

Didn't change your password? If you didn't make this change, please contact our support team immediately.

Best regards,
The WhoAndWhat Security Team

---
For security concerns, contact us at {_emailSettings.Templates.SupportEmail}";
    }

    #endregion
}
