using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Security;

/// <summary>Gmail/Outlook의 SMTP 서버 주소·포트 고정값.</summary>
public static class EmailProviderPresets
{
    public static (string Host, int Port) GetSmtpSettings(EmailProvider provider, string? customHost, int? customPort)
    {
        return provider switch
        {
            EmailProvider.Gmail => ("smtp.gmail.com", 587),
            EmailProvider.Outlook => ("smtp.office365.com", 587),
            EmailProvider.Other => (customHost ?? throw new InvalidOperationException("SMTP host is required for 'Other' provider."),
                                     customPort ?? 587),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }
}