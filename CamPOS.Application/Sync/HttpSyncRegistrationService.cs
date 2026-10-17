using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CamPOS.Application.Sync;

/// <summary>
/// register Edge Function을 불러 토큰을 받고, 받은 것을 바로 저장한다.
///
/// 받은 토큰이 화면을 거치지 않는 것이 요점이다. 사람이 보지 않으므로 잘못
/// 붙여넣을 수 없고, 그래서 설치할 때 입력하는 것이 라이선스 코드 하나로 줄어든다.
/// </summary>
public sealed class HttpSyncRegistrationService : ISyncRegistrationService
{
    private readonly SyncSettingsService _settingsService;

    private readonly HttpClient _http = new()
    {
        // 사람이 활성화 버튼을 누르고 기다리는 중이다. 길게 잡을 자리가 아니다.
        Timeout = TimeSpan.FromSeconds(30),
    };

    public HttpSyncRegistrationService(SyncSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<SyncRegistrationResult> RegisterAsync(
        string licenseCode, CancellationToken cancellationToken = default)
    {
        try
        {
            // 주소와 공개 키는 앱에 실려 있다. 토큰만 비어 있는 상태로 읽어 온다.
            var settings = await _settingsService.LoadAsync();

            if (settings.ServerUrl.Length == 0 || settings.PublishableKey.Length == 0)
            {
                return SyncRegistrationResult.Failure("No server is configured for this installation.");
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post, settings.ServerUrl.TrimEnd('/') + "/functions/v1/register");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.PublishableKey);
            request.Headers.TryAddWithoutValidation("apikey", settings.PublishableKey);

            request.Content = new StringContent(
                JsonSerializer.Serialize(new { license_code = licenseCode }, SyncJson.Options),
                new UTF8Encoding(false),
                "application/json");

            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return SyncRegistrationResult.Failure(ReadError(body, (int)response.StatusCode));
            }

            return await StoreAsync(body);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SyncRegistrationResult.Failure(
                "The server did not answer in time. CamPOS works without this — set it up later in Data Center.");
        }
        catch (Exception exception)
        {
            return SyncRegistrationResult.Failure(
                "The server could not be reached: " + exception.Message);
        }
    }

    private async Task<SyncRegistrationResult> StoreAsync(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var token = root.TryGetProperty("token", out var t) ? t.GetString() : null;
            var siteId = root.TryGetProperty("site_id", out var s) ? s.GetString() : null;

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(siteId))
            {
                return SyncRegistrationResult.Failure(
                    "The server answered in a form this app does not understand.");
            }

            // 주소와 공개 키는 빈 값으로 저장된다(앱의 기본값을 쓴다는 뜻).
            // 토큰만 DPAPI로 암호화되어 들어간다.
            var saved = await _settingsService.SaveAsync(
                SyncDefaults.ServerUrl, SyncDefaults.PublishableKey, token);

            return saved.IsSuccess
                ? SyncRegistrationResult.Success(siteId)
                : SyncRegistrationResult.Failure(saved.Message);
        }
        catch (Exception exception)
        {
            return SyncRegistrationResult.Failure(
                "The token could not be stored: " + exception.Message);
        }
    }

    /// <summary>
    /// 서버가 보낸 사유를 그대로 전한다. "등록되지 않은 라이선스"와 "만료된
    /// 라이선스"는 할 일이 전혀 다른데, 여기서 뭉개면 그 차이가 사라진다.
    /// </summary>
    private static string ReadError(string body, int status)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? $"The server returned {status}.";
            }
        }
        catch
        {
            // JSON이 아니면 게이트웨이가 돌려준 것이다. 본문을 그대로 쓰지 않는다.
        }

        return $"The server returned {status}.";
    }
}
