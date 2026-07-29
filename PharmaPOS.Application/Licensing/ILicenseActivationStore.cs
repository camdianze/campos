namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 활성화 사실을 이 PC에 남기고 읽는 저장소.
///
/// 구현체가 WPF 쪽에 있는 이유는 IRecoveryDataProtector와 같다. 저장 파일을
/// Windows DPAPI로 보호하는데, DPAPI는 Application 계층이 알 필요 없는 OS 기능이다.
/// </summary>
public interface ILicenseActivationStore
{
    /// <summary>이미 활성화된 PC인지. 파일이 없거나 손상됐으면 false.</summary>
    bool IsActivated();

    /// <summary>활성화 기록을 남긴다. 이후 실행부터는 코드를 다시 묻지 않는다.</summary>
    void SaveActivation(string licenseCode);
}
