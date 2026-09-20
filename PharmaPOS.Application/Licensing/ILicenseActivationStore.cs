namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 활성화 사실을 이 PC에 남기고 읽는 저장소.
///
/// 구현체가 WPF 쪽에 있는 이유는 IRecoveryDataProtector와 같다. 저장 파일을
/// Windows DPAPI로 보호하는데, DPAPI는 Application 계층이 알 필요 없는 OS 기능이다.
/// </summary>
public interface ILicenseActivationStore
{
    /// <summary>
    /// 저장해 둔 라이선스 코드. 파일이 없거나, 손상됐거나, 다른 PC·계정에서 복사해 와
    /// 복호화되지 않으면 null.
    ///
    /// 예전에는 "열리는지"만 묻는 IsActivated()였다. 그래서 기한이 지난 코드로도 앱이
    /// 계속 열렸다 — 만료 판정은 코드를 입력하는 순간에만 있었고, 그 뒤로는 아무도
    /// 코드를 다시 읽지 않았기 때문이다. 코드 자체를 돌려주어 매번 다시 검증한다.
    /// </summary>
    string? ReadActivatedCode();

    /// <summary>활성화 기록을 남긴다. 이후 실행부터는 코드를 다시 묻지 않는다.</summary>
    void SaveActivation(string licenseCode);
}
