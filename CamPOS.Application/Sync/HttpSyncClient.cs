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
    /// 서버가 보낸 사유를 그대로 전한다. 함수가 돌려주는 문구는 사람이 읽을
    /// 영어이고, 무엇을 고쳐야 하는지 이미 말하고 있다 — 여기서 "동기화 실패"로
    /// 뭉개면 그 정보가 사라진다.
    /// </summary>
    private static string DescribeFailure(int status, string body)
    {
        var served = ReadErrorField(body);

        if (served.Length > 0)
        {
            return served;
        }

        return status switch
        {
            401 => "The server refused this computer. Check the device token.",
            404 => "The sync function was not found at that address. Check the server address.",
            _ => $"The server returned {status}.",
        };
    }

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
