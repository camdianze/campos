using CamPOS.Application.Repositories;
using CamPOS.Application.Security;
using CamPOS.Application.Settings;

namespace CamPOS.Application.Sync;

/// <summary>
/// 서버 연결 설정을 App_Setting에서 읽고 쓴다.
///
/// 토큰은 <see cref="IRecoveryDataProtector"/>를 통해 암호화해서 넣는다. 이름이
/// "Recovery"인 것은 이메일 앱 비밀번호를 지키려고 먼저 만들어졌기 때문이고,
/// 하는 일은 "이 PC·이 Windows 계정에서만 풀리는 암호화"라 여기에도 그대로
/// 맞는다. 토큰을 위해 같은 것을 하나 더 만들면 DPAPI 호출이 두 군데가 된다.
/// </summary>
public sealed class SyncSettingsService
{
    private readonly IAppSettingRepository _settings;
    private readonly IRecoveryDataProtector _protector;

    public SyncSettingsService(IAppSettingRepository settings, IRecoveryDataProtector protector)
    {
        _settings = settings;
        _protector = protector;
    }

    public async Task<SyncSettings> LoadAsync()
    {
        var stored = await _settings.GetManyAsync(new[]
        {
            AppSettingKeys.SyncServerUrl,
            AppSettingKeys.SyncPublishableKey,
            AppSettingKeys.SyncToken,
        });

        // 적어 넣은 값이 있으면 그쪽이 이긴다. 비어 있으면 앱에 실린 기본값을 쓴다 —
        // 주소와 공개 키는 모든 약국이 같고 비밀이 아니라, 설치할 때 입력받아서
        // 얻을 것이 없고 오타가 날 자리만 는다.
        return new SyncSettings
        {
            ServerUrl = Or(Read(stored, AppSettingKeys.SyncServerUrl), SyncDefaults.ServerUrl),
            PublishableKey = Or(
                Read(stored, AppSettingKeys.SyncPublishableKey), SyncDefaults.PublishableKey),
            Token = ReadToken(stored),
        };
    }

    public async Task<SyncSettingsSaveResult> SaveAsync(string serverUrl, string publishableKey, string token)
    {
        var url = SyncSettings.NormalizeUrl(serverUrl);
        publishableKey = (publishableKey ?? string.Empty).Trim();
        token = (token ?? string.Empty).Trim();

        if (url.Length == 0)
        {
            return SyncSettingsSaveResult.Failure("Enter the server address.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return SyncSettingsSaveResult.Failure(
                "The server address must start with https:// — for example https://abcdefg.supabase.co");
        }

        // http를 막지는 않지만 한마디 한다. 토큰이 평문으로 흐른다.
        if (parsed.Scheme == Uri.UriSchemeHttp)
        {
            return SyncSettingsSaveResult.Failure(
                "The server address must use https, not http — the token would travel unencrypted.");
        }

        if (publishableKey.Length == 0)
        {
            return SyncSettingsSaveResult.Failure("Enter the publishable key.");
        }

        if (token.Length == 0)
        {
            return SyncSettingsSaveResult.Failure("Enter the device token.");
        }

        // 비밀 키를 토큰 칸에 붙여넣는 사고를 막는다. 그 키는 RLS를 통째로
        // 우회하므로 PC에 내려와서는 안 되고, 틀린 채로 저장되면 전송이 거부될
        // 뿐이라 원인이 드러나지 않는다.
        if (token.StartsWith("sb_secret_", StringComparison.OrdinalIgnoreCase))
        {
            return SyncSettingsSaveResult.Failure(
                "That is the server's secret key, not this device's token. The secret key must never be stored on a pharmacy PC.");
        }

        if (publishableKey.StartsWith("sb_secret_", StringComparison.OrdinalIgnoreCase))
        {
            return SyncSettingsSaveResult.Failure(
                "That is the server's secret key. Use the publishable key, which is safe to ship with the app.");
        }

        try
        {
            // 기본값과 같으면 빈 값으로 적는다. 저장해 두면 나중에 프로젝트를 옮겨
            // 앱의 기본값을 바꿔도 약국 PC는 옛 주소를 계속 쓴다 — 그리고 아무도
            // 그 사실을 모른다.
            await _settings.SetAsync(AppSettingKeys.SyncServerUrl,
                url == SyncDefaults.ServerUrl ? string.Empty : url, "text");
            await _settings.SetAsync(AppSettingKeys.SyncPublishableKey,
                publishableKey == SyncDefaults.PublishableKey ? string.Empty : publishableKey, "text");
            await _settings.SetAsync(AppSettingKeys.SyncToken, _protector.Protect(token), "text");
        }
        catch (Exception exception)
        {
            return SyncSettingsSaveResult.Failure("The settings could not be saved: " + exception.Message);
        }

        return SyncSettingsSaveResult.Success("Server connection saved.");
    }

    private static string Read(IReadOnlyDictionary<string, string> stored, string key) =>
        stored.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>
    /// 저장된 빈 문자열은 "값이 없다"로 읽는다. 영수증 설정과 반대인데, 거기서는
    /// 주소를 지운 것이 값이지만 여기서는 지울 이유가 없는 칸이기 때문이다 —
    /// 비워 두면 기본값으로 돌아가는 쪽이 쓸모 있다.
    /// </summary>
    private static string Or(string stored, string fallback) =>
        stored.Trim().Length > 0 ? stored : fallback;

    /// <summary>
    /// 토큰을 푼다. 다른 PC나 다른 Windows 계정으로 데이터베이스만 옮겨 가면
    /// DPAPI가 풀지 못하고 예외를 던지는데, 그것은 고장이 아니라 의도한 결과다.
    /// 빈 값으로 돌려주면 화면은 "연결되지 않음"을 보여주고, 토큰을 다시 넣으면
    /// 된다.
    /// </summary>
    private string ReadToken(IReadOnlyDictionary<string, string> stored)
    {
        var encrypted = Read(stored, AppSettingKeys.SyncToken);

        if (encrypted.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch
        {
            return string.Empty;
        }
    }
}
