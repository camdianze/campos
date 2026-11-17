using System.Net.Http;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CamPOS.Application.Sync;

/// <summary>
/// ISyncClient의 HTTP 구현. SMTP 발송이 Application에 있는 것과 같은 자리다 —
/// 네트워크는 BCL이고, WPF만 할 수 있는 일(DPAPI·인쇄·이미지)이 아니다.
///
/// 어떤 실패도 예외로 나가지 않는다. 호출하는 쪽은 사람이 버튼을 누른 직후이고,
/// 인터넷이 없는 것은 약국에서 흔한 일이다.
/// </summary>
public sealed class HttpSyncClient : ISyncClient
{
    /// <summary>
    /// 하나만 만들어 돌려 쓴다. 요청마다 새로 만들면 소켓이 쌓인다.
    /// </summary>
    private readonly HttpClient _http = new()
    {
        // 수백 행을 느린 회선으로 올리는 경우를 생각한 값. 기본 100초는 사람이
        // 버튼을 누르고 기다리기에 너무 길다.
        Timeout = TimeSpan.FromSeconds(60),
    };

    public async Task<SyncSendResult> SendAsync(
        SyncSettings settings, SyncPayload payload, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
        {
            return SyncSendResult.Failure("This computer is not connected to a server yet.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);

            // 공개 키는 게이트웨이용 체이고, 실제 신원은 토큰이 말한다. 게이트웨이가
            // 경로에 따라 둘 중 하나를 보므로 두 이름으로 모두 보낸다.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.PublishableKey);
            request.Headers.TryAddWithoutValidation("apikey", settings.PublishableKey);
            request.Headers.TryAddWithoutValidation("x-campos-token", settings.Token);

            // 전송은 들여쓰지 않는다. 미리보기 파일과 내용은 같고 모양만 다르다.
            var json = JsonSerializer.Serialize(payload, SyncJson.Options);

            // BOM 없이 UTF-8. 서버는 떼어 내 주지만, 보내지 않는 쪽이 맞다.
            request.Content = new StringContent(json, new UTF8Encoding(false), "application/json");

            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return SyncSendResult.Failure(DescribeFailure((int)response.StatusCode, body));
            }

            return ReadSuccess(body);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SyncSendResult.Failure("The server did not answer in time. Check the internet connection and try again.");
        }
        catch (OperationCanceledException)
        {
            return SyncSendResult.Failure("Sync was cancelled.");
        }
        catch (HttpRequestException exception)
        {
            return SyncSendResult.Failure("The server could not be reached: " + exception.Message);
        }
        catch (Exception exception)
        {
            return SyncSendResult.Failure("Sync failed: " + exception.Message);
        }
    }

    public async Task<SyncPhotoResult> UploadPhotoAsync(
        SyncSettings settings, SyncPhoto photo, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
        {
            return SyncPhotoResult.Failure("This computer is not connected to a server yet.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.PhotoEndpoint);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.PublishableKey);
            request.Headers.TryAddWithoutValidation("apikey", settings.PublishableKey);
            request.Headers.TryAddWithoutValidation("x-campos-token", settings.Token);
            request.Headers.TryAddWithoutValidation("x-campos-product-id", photo.ProductId);
            request.Headers.TryAddWithoutValidation("x-campos-sha256", photo.Sha256);

            if (photo.PhotoUpdatedAt is { } updatedAt)
            {
                request.Headers.TryAddWithoutValidation(
                    "x-campos-photo-updated-at", updatedAt.ToString(CultureInfo.InvariantCulture));
            }

            // ProductPhotoService가 JPEG로 다시 인코딩해 두므로 항상 image/jpeg다.
            request.Content = new ByteArrayContent(photo.Bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return SyncPhotoResult.Failure(DescribeFailure((int)response.StatusCode, body));
            }

            return ReadBool(body, "skipped")
                ? SyncPhotoResult.AlreadyThere()
                : SyncPhotoResult.Uploaded();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SyncPhotoResult.Failure("The server did not answer in time while sending a photo.");
        }
        catch (OperationCanceledException)
        {
            return SyncPhotoResult.Failure("Sync was cancelled.");
        }
        catch (Exception exception)
        {
            return SyncPhotoResult.Failure("The photo could not be sent: " + exception.Message);
        }
    }

    private static bool ReadBool(string body, string field)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty(field, out var value)
                && value.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 화면에 적을 한 줄. 서버가 보낸 사유를 쓰되, <b>쓸 만한 모양일 때만</b> 쓴다.
    ///
    /// 함수가 돌려주는 문구는 사람이 읽을 영어이고 무엇을 고쳐야 하는지 이미
    /// 말하고 있으므로, 여기서 "동기화 실패"로 뭉개면 그 정보가 사라진다. 그래서
    /// 그대로 전하는 것이 기본이다.
    ///
    /// 다만 <b>서버의 오류 메시지 안에 다른 서버의 오류 페이지가 들어 있을 수
    /// 있다.</b> Supabase가 깨어나는 중이면 Edge Function은 떠 있는데 그 뒤의
    /// 데이터베이스가 아직 아니고, 드라이버가 받은 Cloudflare의 521 HTML 한 쪽이
    /// 함수의 메시지에 이어 붙어 정상적인 JSON으로 돌아온다. 파싱은 성공하므로
    /// "JSON이 아니면 버린다"는 검사로는 걸리지 않는다.
    ///
    /// 약국 화면에 HTML이 쏟아지는 것은 그 자체로 고장처럼 보이고, 무엇을 해야
    /// 하는지도 말해 주지 않는다. 그래서 <see cref="Tidy"/>로 한 줄만 남기고,
    /// 태그가 섞여 있으면 통째로 버린 뒤 상태 코드로 설명한다.
    /// </summary>
    internal static string DescribeFailure(int status, string body)
    {
        var served = Tidy(ReadErrorField(body));

        if (served.Length > 0)
        {
            return served;
        }

        return status switch
        {
            401 => "The server refused this computer. Check the device token.",
            404 => "The sync function was not found at that address. Check the server address.",

            // 502·503·504는 게이트웨이, 521은 Cloudflare가 "뒤쪽 서버가 안 떴다"는
            // 뜻이다. 무료 플랜 프로젝트가 잠들었다 깨어나는 동안 전부 나올 수 있고,
            // 약국이 할 일은 똑같다 — 몇 분 기다렸다 다시.
            502 or 503 or 504 or 521 or 522 or 523 or 524 =>
                "The server is not answering yet. It may be starting up — try again in a few minutes.",

            >= 500 => $"The server had a problem (code {status}). Try again in a few minutes.",
            _ => $"The server returned {status}.",
        };
    }

    /// <summary>화면에 내보낼 만한 한 줄인가. 아니면 빈 문자열.</summary>
    /// <remarks>
    /// 버리는 기준은 <b>태그가 보이는가</b>이다. 길이로만 자르면 HTML의 첫 200자가
    /// 그대로 나가고, 그것은 오류 메시지가 아니라 깨진 화면으로 읽힌다.
    /// </remarks>
    internal static string Tidy(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        // 첫 줄만. 여러 줄짜리 서버 오류는 뒤쪽이 스택 추적인 경우가 많다.
        var line = message
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(string.Empty)
            .Trim();

        if (line.Length == 0 || LooksLikeMarkup(line) || LooksLikeMarkup(message))
        {
            return string.Empty;
        }

        const int limit = 200;

        return line.Length <= limit ? line : line[..limit].TrimEnd() + "…";
    }

    private static bool LooksLikeMarkup(string text) =>
        text.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
        || text.Contains("<html", StringComparison.OrdinalIgnoreCase)
        || text.Contains("<head", StringComparison.OrdinalIgnoreCase)
        || text.Contains("<body", StringComparison.OrdinalIgnoreCase)
        || text.Contains("<div", StringComparison.OrdinalIgnoreCase)
        || text.Contains("<span", StringComparison.OrdinalIgnoreCase)
        || text.Contains("<script", StringComparison.OrdinalIgnoreCase);

    private static string ReadErrorField(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty("error", out var error)
                ? error.GetString() ?? string.Empty
                : string.Empty;
        }
        catch
        {
            // JSON이 아니면 게이트웨이가 돌려준 것이다. 본문을 그대로 쓰지 않는다 —
            // HTML 한 쪽이 통째로 화면에 나올 수 있다.
            return string.Empty;
        }
    }

    private static SyncSendResult ReadSuccess(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var siteId = root.TryGetProperty("site_id", out var site)
                ? site.GetString() ?? string.Empty
                : string.Empty;

            var accepted = new Dictionary<string, int>();

            if (root.TryGetProperty("accepted", out var acceptedElement)
                && acceptedElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in acceptedElement.EnumerateObject())
                {
                    if (entry.Value.TryGetInt32(out var count))
                    {
                        accepted[entry.Name] = count;
                    }
                }
            }

            var positions = new Dictionary<string, long>();

            if (root.TryGetProperty("positions", out var positionsElement)
                && positionsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in positionsElement.EnumerateObject())
                {
                    if (entry.Value.TryGetInt64(out var position))
                    {
                        positions[entry.Name] = position;
                    }
                }
            }

            return SyncSendResult.Success(
                accepted.Values.Sum() + " rows accepted.", siteId, accepted, positions);
        }
        catch (Exception exception)
        {
            // 200인데 읽을 수 없는 본문. 서버에는 들어갔을 수도 있으므로 성공이라고
            // 말하지 않는다 — 성공이라 하면 워터마크가 올라가고, 틀렸다면 그 행들은
            // 영영 다시 선택되지 않는다.
            return SyncSendResult.Failure(
                "The server answered in a form this app does not understand: " + exception.Message);
        }
    }
}
