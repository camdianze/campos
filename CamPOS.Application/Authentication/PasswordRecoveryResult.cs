namespace PharmaPOS.Application.Authentication;

/// <summary>비밀번호 복구 각 단계(질문 검증/OTP 검증/비밀번호 재설정)의 시도 결과.</summary>
public class PasswordRecoveryResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }

    /// <summary>
    /// 검증 성공 시(VerifySecurityAnswerAsync, VerifyEmailOtpAsync) 발급되는 토큰.
    /// 다음 단계(ResetPasswordAsync) 호출 시 반드시 이 값을 그대로 전달해야 한다.
    /// </summary>
    public string? VerifiedToken { get; }

    private PasswordRecoveryResult(bool isSuccess, string? message, string? verifiedToken)
    {
        IsSuccess = isSuccess;
        Message = message;
        VerifiedToken = verifiedToken;
    }

    public static PasswordRecoveryResult Success(string? verifiedToken = null) => new(true, null, verifiedToken);
    public static PasswordRecoveryResult Failure(string message) => new(false, message, null);
}