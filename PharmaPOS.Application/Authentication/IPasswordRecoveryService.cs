namespace PharmaPOS.Application.Authentication;

/// <summary>F-01 부속 기능: 관리자 본인 비밀번호 복구 로직을 담당하는 인터페이스.</summary>
public interface IPasswordRecoveryService
{
    /// <summary>아이디로 사용 가능한 복구 수단을 조회한다. 계정이 없어도 예외를 던지지 않는다.</summary>
    Task<RecoveryMethodInfo> GetAvailableRecoveryMethodsAsync(string username);

    /// <summary>보안 질문 답을 검증한다.</summary>
    Task<PasswordRecoveryResult> VerifySecurityAnswerAsync(string username, string answer);

    /// <summary>이메일로 OTP 코드를 발송한다.</summary>
    Task<PasswordRecoveryResult> SendEmailOtpAsync(string username);

    /// <summary>발송된 OTP 코드를 검증한다.</summary>
    Task<PasswordRecoveryResult> VerifyEmailOtpAsync(string username, string enteredCode);

    /// <summary>
    /// 검증 완료 후 새 비밀번호를 설정한다. verifiedToken은 앞선 검증 단계가
    /// 실제로 성공했음을 증명하는 값이다 (검증 없이 바로 이 메서드를 호출하는 것을 막기 위함).
    /// </summary>
    Task<PasswordRecoveryResult> ResetPasswordAsync(string username, string verifiedToken, string newPassword, string confirmNewPassword);
    /// <summary>
    /// 등록된 복구 이메일로 사용자의 아이디를 발송한다. (F-01 부속: 아이디 찾기)
    /// 보안 원칙: 이메일이 실제로 등록되어 있는지 여부와 무관하게 항상 동일한 성공 결과를 반환한다.
    /// (계정 존재 여부 추측 방지 — Screen 스펙 없이 제품 오너 결정으로 신규 추가)
    /// </summary>
    Task<PasswordRecoveryResult> SendUsernameByEmailAsync(string recoveryEmail);
}