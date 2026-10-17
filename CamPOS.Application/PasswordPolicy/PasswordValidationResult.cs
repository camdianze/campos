namespace PharmaPOS.Application.PasswordPolicy;

/// <summary>
/// 비밀번호 규칙 검사 결과. 실패 시 첫 번째로 위반한 규칙의 메시지를 담는다.
/// </summary>
public class PasswordValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private PasswordValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static PasswordValidationResult Valid() => new(true, null);

    public static PasswordValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}