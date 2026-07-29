namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 라이선스 코드 검증과 활성화 상태 조회.
/// 인터넷 연결이 필요 없고, 검증은 프로그램에 내장된 해시와의 비교로만 이루어진다.
/// </summary>
public interface ILicenseService
{
    /// <summary>이 PC가 이미 활성화되어 코드 입력을 건너뛰어도 되는지.</summary>
    bool IsActivated();

    /// <summary>
    /// 입력된 코드를 검증하고, 맞으면 활성화 기록을 남긴다.
    /// </summary>
    LicenseActivationResult Activate(string licenseCode);
}
