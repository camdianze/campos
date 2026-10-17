using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Authentication;

/// <summary>
/// F-14 사용자 관리 로직을 담당하는 인터페이스. (Screen SCR-USER-016)
/// </summary>
public interface IUserManagementService
{
    /// <summary>신규 사용자를 생성한다.</summary>
    Task<UserManagementResult> CreateUserAsync(
        string facilityId, string username, string password, string confirmPassword, UserRole? role);

    /// <summary>
    /// 사용자를 비활성화한다. currentUserId와 targetUserId가 같으면
    /// "본인 계정 비활성화" 차단 규칙에 걸린다 (Screen §4.3절).
    /// </summary>
    Task<UserManagementResult> DeactivateUserAsync(string targetUserId, string currentUserId);

    /// <summary>
    /// 비활성화한 사용자를 다시 쓸 수 있게 되돌린다.
    /// 비활성화와 달리 본인 계정 검사가 없다 — 자기 계정은 애초에 비활성화할 수 없어
    /// 되살릴 일이 없고, 로그인해 있다는 것 자체가 이미 Active라는 뜻이다.
    /// </summary>
    Task<UserManagementResult> ActivateUserAsync(string targetUserId);

    /// <summary>사용자의 역할을 변경한다.</summary>
    Task<UserManagementResult> UpdateRoleAsync(string targetUserId, UserRole newRole);

    /// <summary>관리자가 대상 사용자의 비밀번호를 새 값으로 직접 설정한다 (현재 비밀번호 확인 불필요).</summary>
    Task<UserManagementResult> ResetPasswordAsync(string targetUserId, string username, string newPassword, string confirmPassword);
}