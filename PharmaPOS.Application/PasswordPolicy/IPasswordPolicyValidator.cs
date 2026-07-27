namespace PharmaPOS.Application.PasswordPolicy;

/// <summary>
/// 비밀번호 규칙을 검사하는 인터페이스.
/// </summary>
public interface IPasswordPolicyValidator
{
    /// <summary>
    /// newPassword가 비밀번호 규칙을 만족하는지 검사한다.
    /// username과의 동일 여부는 항상 검사한다.
    /// currentPassword가 제공되면(비밀번호 변경 시), 현재 비밀번호와 동일한지도 검사한다.
    /// currentPassword가 null이면(초기 설정 시), 이 검사는 건너뛴다.
    /// </summary>
    PasswordValidationResult Validate(string newPassword, string username, string? currentPassword = null);
}