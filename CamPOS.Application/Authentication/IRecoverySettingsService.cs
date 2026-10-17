using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Authentication;

/// <summary>
/// 로그인한 관리자 본인의 비밀번호 복구 정보(보안질문/이메일)를 등록/수정하는 인터페이스.
/// </summary>
public interface IRecoverySettingsService
{
    Task<RecoverySettingsResult> SaveRecoverySettingsAsync(
        string userId, string username,
        string? securityQuestion, string? securityAnswer,
        string? recoveryEmail, EmailProvider? emailProvider,
        string? emailAppPassword, string? smtpHost, int? smtpPort);
}