namespace CamPOS.Application.Sync;

/// <summary>
/// 이 PC가 서버로 보내기 위해 알아야 하는 것 전부. 세 개뿐이다.
///
/// 셋 중 <see cref="Token"/>만 비밀이다. 주소와 공개 키는 모든 약국이 같고
/// 앱에 실려 나가도 되는 값이라 평문으로 저장하지만, 토큰은 이 PC 하나를
/// 가리키므로 DPAPI로 암호화해 둔다 — <see cref="SyncSettingsService"/>가 그 일을
/// 한다. 복사해 간 데이터베이스가 다른 PC에서 전송할 수 없는 이유가 그것이다.
/// </summary>
public sealed class SyncSettings
{
    public string ServerUrl { get; init; } = string.Empty;

    public string PublishableKey { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public bool IsConfigured =>
        ServerUrl.Length > 0 && PublishableKey.Length > 0 && Token.Length > 0;

    /// <summary>실제로 POST할 주소. 함수 이름 <c>sync</c>는 서버 쪽에서 고정이다.</summary>
    public string Endpoint => ServerUrl.TrimEnd('/') + "/functions/v1/sync";

    /// <summary>사진을 올릴 주소. 묶음과 싣는 것이 달라 함수가 따로 있다.</summary>
    public string PhotoEndpoint => ServerUrl.TrimEnd('/') + "/functions/v1/sync-photo";

    /// <summary>
    /// 붙여넣은 주소를 쓸 수 있는 꼴로 다듬는다.
    ///
    /// Supabase 설정 화면이 보여주는 주소는 <c>https://xxxx.supabase.co/rest/v1/</c>이고,
    /// 사람은 거기 있는 것을 복사한다. 그 꼬리를 그대로 두면 POST가
    /// <c>/rest/v1/functions/v1/sync</c>로 가서 404가 나는데, 화면에 보이는 주소는
    /// 콘솔에 적힌 것과 글자 하나 다르지 않아 틀린 곳을 찾기 어렵다. 그래서
    /// 받아 주고 떼어 낸다.
    /// </summary>
    public static string NormalizeUrl(string text)
    {
        var trimmed = (text ?? string.Empty).Trim().TrimEnd('/');

        foreach (var tail in new[] { "/rest/v1", "/functions/v1", "/functions/v1/sync" })
        {
            if (trimmed.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^tail.Length].TrimEnd('/');
            }
        }

        return trimmed;
    }
}

/// <summary>
/// 설정 저장 결과. 서비스 경계 밖으로 예외를 던지지 않는 이 저장소의 방식을 따른다.
/// 어느 칸이 문제인지 문구가 직접 말한다 — 칸이 셋뿐이라 칸별 사전까지는 필요 없다.
/// </summary>
public sealed class SyncSettingsSaveResult
{
    private SyncSettingsSaveResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public bool IsSuccess { get; }

    public string Message { get; }

    public static SyncSettingsSaveResult Success(string message) => new(true, message);

    public static SyncSettingsSaveResult Failure(string message) => new(false, message);
}
