namespace PharmaPOS.Application.PasswordPolicy;

/// <summary>
/// IPasswordPolicyValidator의 구현체.
/// 공통 규칙:
/// - 8자 이상
/// - 영문자와 숫자 포함
/// - 공백 불가
/// - username과 동일한 비밀번호 불가
/// - (currentPassword가 제공된 경우) 현재 비밀번호와 동일한 비밀번호 불가
/// </summary>
public class PasswordPolicyValidator : IPasswordPolicyValidator
{
    public PasswordValidationResult Validate(string newPassword, string username, string? currentPassword = null)
    {
        if (newPassword.Length < 8)
        {
            return PasswordValidationResult.Invalid("Password does not meet the required rules.");
        }

        if (newPassword.Any(char.IsWhiteSpace))
        {
            return PasswordValidationResult.Invalid("Password does not meet the required rules.");
        }

        var hasLetter = newPassword.Any(char.IsLetter);
        var hasDigit = newPassword.Any(char.IsDigit);
        if (!hasLetter || !hasDigit)
        {
            return PasswordValidationResult.Invalid("Password does not meet the required rules.");
        }

        if (string.Equals(newPassword, username, StringComparison.OrdinalIgnoreCase))
        {
            return PasswordValidationResult.Invalid("Password cannot be the same as username.");
        }

        if (currentPassword is not null && newPassword == currentPassword)
        {
            return PasswordValidationResult.Invalid("New password must be different from the current password.");
        }

        return PasswordValidationResult.Valid();
    }
}