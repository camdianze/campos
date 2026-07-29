namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 라이선스 활성화 시도 결과. 이 앱의 다른 서비스들과 같은 결과 객체 규칙을 따른다
/// (예외를 서비스 경계 밖으로 던지지 않고 사용자에게 보여줄 메시지를 담아 돌려준다).
/// </summary>
public class LicenseActivationResult
{
    public bool IsSuccess { get; }

    /// <summary>실패했을 때 화면에 그대로 띄울 영문 메시지.</summary>
    public string Message { get; }

    private LicenseActivationResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static LicenseActivationResult Success() => new(true, string.Empty);

    public static LicenseActivationResult Failure(string message) => new(false, message);
}
