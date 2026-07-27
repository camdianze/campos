namespace PharmaPOS.Application.Authentication;

/// <summary>
/// 아이디 입력 후, 그 계정에 사용 가능한 복구 수단을 화면에 알려주기 위한 정보.
/// 보안 원칙: 존재하지 않는 계정과 복구 수단이 없는 계정을 구분하지 않는다 (계정 추측 방지).
/// </summary>
public class RecoveryMethodInfo
{
    public bool HasSecurityQuestion { get; set; }
    public string? SecurityQuestion { get; set; }

    public bool HasEmail { get; set; }

    /// <summary>이메일이 있어도 인터넷이 안 되면 false — 화면에서 이메일 옵션을 숨기는 데 사용.</summary>
    public bool IsEmailUsable { get; set; }

    /// <summary>둘 다 없으면(또는 계정이 없으면) 이 값이 true — 화면에 일반적인 메시지만 보여준다.</summary>
    public bool NoRecoveryMethodAvailable => !HasSecurityQuestion && !IsEmailUsable;
}