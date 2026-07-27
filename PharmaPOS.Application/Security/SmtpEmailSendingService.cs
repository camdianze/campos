using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Security;

/// <summary>IEmailSendingService의 SMTP 구현체.</summary>
public class SmtpEmailSendingService : IEmailSendingService
{
    public async Task<bool> SendOtpCodeAsync(
        string recipientEmail, string otpCode, EmailProvider provider,
        string? smtpHost, int? smtpPort, string senderEmail, string appPassword)
    {
        try
        {
            var (host, port) = EmailProviderPresets.GetSmtpSettings(provider, smtpHost, smtpPort);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, appPassword)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail),
                Subject = "PharmaPOS Password Recovery Code",
                Body = $"Your password recovery code is: {otpCode}\n\nThis code will expire in 10 minutes.",
                IsBodyHtml = false
            };
            message.To.Add(recipientEmail);

            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public async Task<bool> SendUsernameAsync(
        string recipientEmail, string username, EmailProvider provider,
        string? smtpHost, int? smtpPort, string senderEmail, string appPassword)
    {
        try
        {
            var (host, port) = EmailProviderPresets.GetSmtpSettings(provider, smtpHost, smtpPort);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, appPassword)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail),
                Subject = "PharmaPOS Username Recovery",
                Body = $"Your PharmaPOS username is: {username}",
                IsBodyHtml = false
            };
            message.To.Add(recipientEmail);

            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public Task<bool> IsInternetAvailableAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                using var ping = new Ping();
                var reply = ping.Send("8.8.8.8", 2000);
                return reply?.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        });
    }
}