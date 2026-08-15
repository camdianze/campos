namespace PharmaPOS.Application.PasswordPolicy;

/// <summary>
/// IPasswordPolicyValidator의 구현체.
/// 공통 규칙:
/// - 8자 이상
/// - 영문자와 숫자를 각각 하나 이상 포함
/// - 공백 불가
/// - username과 동일한 비밀번호 불가
/// - (currentPassword가 제공된 경우) 현재 비밀번호와 동일한 비밀번호 불가
///
/// 특수문자(!@#$ 등)는 처음부터 막은 적이 없다. 다만 "숫자와 영문자를 각각 하나씩"이라는
/// 조건이 있어서, !@#$%^&amp;* 같은 비밀번호는 숫자가 없다는 이유로 걸린다.
/// 그때 "규칙에 맞지 않습니다" 한 줄만 돌려주면 무엇이 문제인지 알 수가 없어
/// 사용자가 특수문자 자체를 금지한 것으로 오해하게 된다. 그래서 사유를 나눠 돌려준다.
/// </summary>
public class PasswordPolicyValidator : IPasswordPolicyValidator
{
    /// <summary>화면에 함께 띄울 규칙 안내. 문구가 갈라지지 않게 여기 한 곳에 둔다.</summary>
    public const string RuleSummary =
        "At least 8 characters, including one letter and one number. "
        + "Special characters are allowed. Spaces are not.";

    public PasswordValidationResult Validate(string newPassword, string username, string? currentPassword = null)
    {
        if (newPassword.Length < 8)
        {
            return PasswordValidationResult.Invalid("Password must be at least 8 characters long.");
        }

        if (newPassword.Any(char.IsWhiteSpace))
        {
            return PasswordValidationResult.Invalid("Password cannot contain spaces.");
        }

        var hasLetter = newPassword.Any(char.IsLetter);
        var hasDigit = newPassword.Any(char.IsDigit);

        if (!hasLetter || !hasDigit)
        {
            return PasswordValidationResult.Invalid(
                "Password must contain at least one letter and one number. Special characters are allowed.");
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
