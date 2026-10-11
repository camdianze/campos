namespace PharmaPOS.Application.Sync;

/// <summary>
/// 묶음 하나를 서버로 보낸다. 보내는 일 말고는 아무것도 하지 않는다 —
/// 워터마크를 올리는 것은 응답을 본 쪽의 몫이고, 그 순서가 뒤집히면 실패한
/// 묶음의 행들이 다시 선택되지 않는다.
/// </summary>
public interface ISyncClient
{
    Task<SyncSendResult> SendAsync(
        SyncSettings settings, SyncPayload payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// 전송 결과. 서비스 경계 밖으로 예외를 던지지 않는다 — 네트워크가 없는 것은
/// 약국에서 흔한 일이고, 그때 앱이 멈추는 대신 사유를 보여줘야 한다.
/// </summary>
public sealed class SyncSendResult
{
    private SyncSendResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public bool IsSuccess { get; }

    public string Message { get; }

    /// <summary>서버가 확인해 준 약국 코드. 토큰이 어느 약국인지 말해 준 결과다.</summary>
    public string SiteId { get; private init; } = string.Empty;

    /// <summary>서버가 받았다고 말한 표별 행 수.</summary>
    public IReadOnlyDictionary<string, int> Accepted { get; private init; }
        = new Dictionary<string, int>();

    /// <summary>
    /// 서버가 되돌려준 위치. <b>이 값이 돌아온 뒤에만</b> Sync_State에 적는다.
    /// </summary>
    public IReadOnlyDictionary<string, long> Positions { get; private init; }
        = new Dictionary<string, long>();

    public static SyncSendResult Success(
        string message,
        string siteId,
        IReadOnlyDictionary<string, int> accepted,
        IReadOnlyDictionary<string, long> positions) =>
        new(true, message) { SiteId = siteId, Accepted = accepted, Positions = positions };

    public static SyncSendResult Failure(string message) => new(false, message);
}
