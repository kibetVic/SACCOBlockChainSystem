using System.Net;
using System.Net.Mail;

namespace SACCOBlockChainSystem.Services
{
    public interface IEmailService
    {
        Task<bool> SendVerificationCodeAsync(string email, string username, string code);
        Task<bool> SendEmailAsync(string to, string subject, string body);
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml);
        Task<bool> SendOtpAsync(string email, string username, string otp, int expiryMinutes = 5);
    }
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendVerificationCodeAsync(string email, string username, string code)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["EmailSettings:Username"];
                var smtpPassword = _configuration["EmailSettings:Password"];
                var fromEmail = _configuration["EmailSettings:FromEmail"] ?? smtpUsername;

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                var subject = "Password Reset Verification Code";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>Password Reset Request</h2>
                        <p>Dear {username},</p>
                        <p>You requested to reset your password. Please use the verification code below:</p>
                        <div style='background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px;'>
                            {code}
                        </div>
                        <p>This code will expire in <strong>10 minutes</strong>.</p>
                        <p>If you did not request this, please ignore this email.</p>
                        <hr/>
                        <p style='font-size: 12px; color: #666;'>SACCO Blockchain System</p>
                    </body>
                    </html>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Verification code sent to {email}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {email}");
                return false;
            }
        }

        // Add this method for general email sending
        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            return await SendEmailAsync(to, subject, body, true);
        }

        // Add this method for general email sending with HTML option
        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["EmailSettings:Username"];
                var smtpPassword = _configuration["EmailSettings:Password"];
                var fromEmail = _configuration["EmailSettings:FromEmail"] ?? smtpUsername;

                if (string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogWarning("Email credentials not configured. Email will not be sent.");
                    return false;
                }

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };
                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Email sent to {to} - Subject: {subject}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {to}");
                return false;
            }
        }


        public async Task<bool> SendOtpAsync(string email, string username, string otp, int expiryMinutes = 5)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["EmailSettings:Username"];
                var smtpPassword = _configuration["EmailSettings:Password"];
                var fromEmail = _configuration["EmailSettings:FromEmail"] ?? smtpUsername;

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                var subject = "OTP for Pending Disbursement Access";

                var body = $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background-color: #1a237e; color: white; padding: 20px; text-align: center; }}
                    .content {{ padding: 20px; background-color: #f8f9fa; }}
                    .otp-box {{ background-color: #fff; padding: 20px; text-align: center; border-radius: 8px; border: 2px dashed #1a237e; margin: 20px 0; }}
                    .otp-code {{ font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #1a237e; }}
                    .footer {{ text-align: center; padding: 15px; font-size: 12px; color: #666; }}
                    .warning {{ color: #d32f2f; font-weight: bold; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h2>🔐 One-Time Password (OTP)</h2>
                    </div>
                    <div class='content'>
                        <p>Dear <strong>{username}</strong>,</p>
                        <p>You are attempting to access <strong>Pending Disbursement</strong> section.</p>
                        <p>Please use the One-Time Password below to complete your verification:</p>

                        <div class='otp-box'>
                            <div class='otp-code'>{otp}</div>
                        </div>

                        <p>This OTP is valid for <strong>{expiryMinutes} minutes</strong>.</p>
                        <p class='warning'>⚠️ This is a one-time use OTP. Do not share it with anyone.</p>

                        <hr/>
                        <p><strong>Security Information:</strong></p>
                        <ul>
                            <li>OTP expires: {DateTime.UtcNow.AddMinutes(expiryMinutes):yyyy-MM-dd HH:mm:ss} UTC</li>
                            <li>IP Address: {System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? "Unknown"}</li>
                            <li>Request Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</li>
                        </ul>

                        <p>If you did not request this OTP, please ignore this email or contact support immediately.</p>
                    </div>
                    <div class='footer'>
                        <p>&copy; {DateTime.UtcNow.Year} SACCO Blockchain System. All rights reserved.</p>
                        <p>This is an automated email, please do not reply.</p>
                    </div>
                </div>
            </body>
            </html>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"OTP sent to {email} for {username}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send OTP to {email}");
                return false;
            }
        }
    }
}