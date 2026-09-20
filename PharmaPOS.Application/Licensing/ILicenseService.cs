namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 라이선스 코드 검증과 활성화 상태 조회.
/// 인터넷 연결이 필요 없고, 검증은 프로그램에 내장된 해시와의 비교로만 이루어진다.
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// 이 PC의 라이선스 상태. 저장된 코드를 <b>매번 다시 검증해서</b> 만든다 —
    /// 파일이 열리는지만 보면 기한이 지난 코드로도 앱이 계속 열린다.
    /// </summary>
    LicenseStatus GetStatus();

    /// <summary>
    /// 입력된 코드를 검증하고, 맞으면 활성화 기록을 남긴다.
    /// </summary>
    LicenseActivationResult Activate(string licenseCode);
}
