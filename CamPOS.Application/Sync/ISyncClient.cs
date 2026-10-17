namespace CamPOS.Application.Sync;

/// <summary>
/// 묶음 하나를 서버로 보낸다. 보내는 일 말고는 아무것도 하지 않는다 —
/// 워터마크를 올리는 것은 응답을 본 쪽의 몫이고, 그 순서가 뒤집히면 실패한
/// 묶음의 행들이 다시 선택되지 않는다.
/// </summary>
public interface ISyncClient
{
    Task<SyncSendResult> SendAsync(
        SyncSettings settings, SyncPayload payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// 사진 한 장. 묶음과 다른 주소로, 바이트를 그대로 올린다.
    ///
    /// 서버가 "이미 같은 것이 있다"고 답하면 <see cref="SyncPhotoResult.Skipped"/>가 켜진다.
    /// 그것도 성공이고, 보낸 쪽은 그 사진을 "올린 것"으로 기록해야 한다 —
    /// 기록하지 않으면 같은 사진을 매번 다시 올린다.
    /// </summary>
    Task<SyncPhotoResult> UploadPhotoAsync(
        SyncSettings settings, SyncPhoto photo, CancellationToken cancellationToken = default);
}

public sealed class SyncPhotoResult
{
    private SyncPhotoResult(bool isSuccess, bool skipped, string message)
    {
        IsSuccess = isSuccess;
        Skipped = skipped;
        Message = message;
    }

    public bool IsSuccess { get; }

    /// <summary>서버에 이미 같은 사진이 있어 보내지 않았다.</summary>
    public bool Skipped { get; }

    public string Message { get; }

    public static SyncPhotoResult Uploaded() => new(true, false, string.Empty);

    public static SyncPhotoResult AlreadyThere() => new(true, true, string.Empty);

    public static SyncPhotoResult Failure(string message) => new(false, false, message);
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
