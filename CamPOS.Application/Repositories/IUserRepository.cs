using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// Users 테이블에 대한 데이터 접근을 추상화한 인터페이스.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);

    Task UpdatePasswordHashAsync(string userId, string newPasswordHash);

    Task<IReadOnlyList<User>> SearchUsersAsync(string facilityId, string searchTerm, EntityStatus? statusFilter);

    Task InsertAsync(User user);

    Task UpdateRoleAsync(string userId, UserRole role);

    Task UpdateStatusAsync(string userId, EntityStatus status);

    /// <summary>
    /// 보안 질문, 이메일, 암호화된 앱 비밀번호를 갱신한다.
    /// (Account Recovery Settings 화면에서 사용)
    /// </summary>
    Task UpdateRecoveryInfoAsync(
        string userId, string? securityQuestion, string? securityAnswerHash,
        string? recoveryEmail, EmailProvider? emailProvider, string? emailAppPasswordEncrypted,
        string? smtpHost, int? smtpPort);
    /// <summary>
    /// 복구 이메일 주소로 사용자를 조회한다. (F-01 부속: 아이디 찾기)
    /// 존재하지 않으면 null을 반환한다 — 호출하는 쪽이 "존재 여부를 노출하지 않는" 처리를 해야 한다.
    /// </summary>
    Task<User?> GetByRecoveryEmailAsync(string recoveryEmail);
}