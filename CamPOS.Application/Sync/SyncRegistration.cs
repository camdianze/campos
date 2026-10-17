namespace CamPOS.Application.Sync;

/// <summary>
/// 라이선스 코드를 서버에 보내 이 PC의 동기화 토큰을 받아 온다.
///
/// 설치할 때 사람이 넣는 것을 <b>라이선스 코드 하나</b>로 줄이기 위한 것이다.
/// 전에는 라이선스와 토큰을 따로 발급해 따로 입력했는데, 토큰은 약국이 볼 이유가
/// 없는 값이고 손으로 나르는 동안 잘못 붙여넣을 자리만 됐다.
///
/// <b>활성화 자체는 여전히 오프라인이다.</b> 이것은 활성화가 끝난 뒤에 따로
/// 도는 일이고, 실패해도 앱은 그대로 쓸 수 있다 — 동기화만 설정되지 않은 채
/// 남고, 나중에 인터넷이 될 때 다시 하거나 토큰을 손으로 넣으면 된다.
/// </summary>
public interface ISyncRegistrationService
{
    Task<SyncRegistrationResult> RegisterAsync(
        string licenseCode, CancellationToken cancellationToken = default);
}

public sealed class SyncRegistrationResult
{
    private SyncRegistrationResult(bool isSuccess, string message, string siteId)
    {
        IsSuccess = isSuccess;
        Message = message;
        SiteId = siteId;
    }

    public bool IsSuccess { get; }

    public string Message { get; }

    /// <summary>서버가 알려준 약국 코드. 화면에 보여 주면 어느 약국으로 붙었는지 확인된다.</summary>
    public string SiteId { get; }

    public static SyncRegistrationResult Success(string siteId) =>
        new(true, $"This computer is now connected as {siteId}.", siteId);

    public static SyncRegistrationResult Failure(string message) =>
        new(false, message, string.Empty);
}
