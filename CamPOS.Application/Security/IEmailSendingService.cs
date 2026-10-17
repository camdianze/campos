using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Security;

/// <summary>비밀번호 복구 인증코드(OTP)를 이메일로 발송하는 인터페이스.</summary>
public interface IEmailSendingService
{
    Task<bool> SendOtpCodeAsync(
        string recipientEmail, string otpCode, EmailProvider provider,
        string? smtpHost, int? smtpPort, string senderEmail, string appPassword);

    /// <summary>짧은 타임아웃으로 인터넷 연결 가능 여부를 확인한다.</summary>
    Task<bool> IsInternetAvailableAsync();
    /// <summary>아이디 찾기 결과 이메일을 발송한다.</summary>
    Task<bool> SendUsernameAsync(
        string recipientEmail, string username, EmailProvider provider,
        string? smtpHost, int? smtpPort, string senderEmail, string appPassword);
}