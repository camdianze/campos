using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;

namespace PharmaPOS.Tools.LicenseIssuer;

/// <summary>업로드 결과. 발급 화면에 한 줄로 요약해 보여주기 위한 값이다.</summary>
public sealed record CloudUploadOutcome(int Uploaded, int Remaining, string? Error)
{
    public static CloudUploadOutcome Nothing { get; } = new(0, 0, null);

    public bool IsFullySynced => Remaining == 0;
}

/// <summary>
/// 발급 기록을 회사 Firestore에 올린다.
///
/// 설계에서 양보하지 않은 것: <b>업로드 실패가 발급을 막지 않는다.</b>
/// 계약하러 간 자리에서 인터넷이 안 되더라도 코드는 나와야 하고 USB에 담겨야 한다.
/// 그래서 순서가 "대기열에 먼저 넣고 → 올려보고 → 성공한 것만 대기열에서 뺀다"이다.
/// 업로드 도중에 프로그램이 죽어도 기록이 증발하지 않는다.
///
/// 문서 ID는 발급 번호라서 같은 건을 두 번 올려도 덮어쓰기가 된다(SetAsync).
/// 재시도가 중복 문서를 만들지 않는다는 뜻이다.
///
/// 이 클래스는 발급 도구 전용이다. 고객에게 나가는 PharmaPOS.exe에는 절대 들어가지 않는다 —
/// 고객 앱이 네트워크에 의존하는 순간 이 제품의 오프라인 전제가 깨진다.
/// </summary>
public sealed class CloudLedger
{
    /// <summary>
    /// 오프라인이거나 방화벽에 막혔을 때 gRPC가 기본 재시도로 한참을 붙들고 있는다.
    /// 업로드가 늦어도 발급은 이미 끝나 있어야 하므로 짧게 자른다.
    /// </summary>
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(15);

    private readonly CloudConfig _config;
    private readonly string _queuePath;

    public CloudLedger(CloudConfig config, string queuePath)
    {
        _config = config;
        _queuePath = queuePath;
    }

    public int PendingCount => ReadQueue().Count;

    /// <summary>새 발급 건을 대기열에 넣고, 밀린 것까지 한꺼번에 올려본다.</summary>
    public async Task<CloudUploadOutcome> UploadAsync(IssuanceRecord record)
    {
        Enqueue(record);
        return await FlushAsync();
    }

    /// <summary>대기열에 밀려 있는 것을 올린다. 실패한 건은 대기열에 그대로 남는다.</summary>
    public async Task<CloudUploadOutcome> FlushAsync()
    {
        var queue = ReadQueue();

        if (queue.Count == 0)
            return CloudUploadOutcome.Nothing;

        FirestoreDb database;

        try
        {
            database = await BuildDatabaseAsync();
        }
        catch (Exception ex)
        {
            // 자격 증명이 잘못됐거나 프로젝트를 못 찾은 경우. 한 건도 못 올린다.
            // 읽으면서 걸러낸 중복을 여기서 되써 준다. 안 그러면 설정이 잘못된 동안
            // 발급할 때마다 같은 줄이 파일에 계속 쌓인다.
            WriteQueue(queue);
            return new CloudUploadOutcome(0, queue.Count, Describe(ex));
        }

        var collection = database.Collection(_config.Collection);
        var remaining = new List<IssuanceRecord>();
        var uploaded = 0;
        string? lastError = null;

        foreach (var record in queue)
        {
            try
            {
                using var timeout = new CancellationTokenSource(UploadTimeout);

                await collection
                    .Document(record.SerialNumber.ToString("D6"))
                    .SetAsync(ToDocument(record), cancellationToken: timeout.Token);

                uploaded++;
            }
            catch (Exception ex)
            {
                lastError = Describe(ex);
                remaining.Add(record);
            }
        }

        WriteQueue(remaining);

        return new CloudUploadOutcome(uploaded, remaining.Count, lastError);
    }

    private async Task<FirestoreDb> BuildDatabaseAsync()
    {
        var builder = new FirestoreDbBuilder { ProjectId = _config.ProjectId };

        // 경로를 비워두면 GOOGLE_APPLICATION_CREDENTIALS 환경변수나 gcloud 로그인 자격 증명을 쓴다.
        //
        // 경로를 넘길 때 굳이 ServiceAccountCredential로 타입을 못박는 이유:
        // 예전 API(GoogleCredential.FromFile)는 파일 내용을 보고 종류를 알아서 정했다.
        // 그래서 키 파일이 바꿔치기되면 impersonation 자격 증명 같은 다른 종류로 둔갑할 수 있어
        // 폐기 예정이 됐다. 여기서 쓰는 건 언제나 서비스 계정 키뿐이므로 그렇게 못 박는다.
        if (!string.IsNullOrWhiteSpace(_config.CredentialsPath))
        {
            builder.GoogleCredential = CredentialFactory
                .FromFile<ServiceAccountCredential>(_config.CredentialsPath)
                .ToGoogleCredential();
        }

        return await builder.BuildAsync();
    }

    /// <summary>
    /// 문서 필드. 발급 번호(D6)로 문서 ID를 만들어 콘솔에서든 웹에서든 번호순으로 정렬되게 한다.
    /// uploadedAt은 서버 시각이다 — 발급 PC 시계가 틀어져 있어도 동기화 시점은 정확하다.
    /// </summary>
    private static Dictionary<string, object?> ToDocument(IssuanceRecord record) => new()
    {
        ["serial"] = (long)record.SerialNumber,
        ["customer"] = record.CustomerName,
        ["issuedAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(record.IssuedAt)),
        ["expiresAt"] = record.IsPerpetual
            ? null
            : Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(record.ExpiresAt)),
        ["isPerpetual"] = record.IsPerpetual,
        ["code"] = record.Code,
        ["issuedBy"] = record.IssuedBy,
        ["uploadedAt"] = FieldValue.ServerTimestamp
    };

    // ── 업로드 대기열 ─────────────────────────────────────────────────────

    /// <summary>한 줄에 JSON 하나(JSONL). 이어붙이기가 쉬워서 중간에 죽어도 앞부분은 멀쩡하다.</summary>
    private void Enqueue(IssuanceRecord record)
    {
        var directory = Path.GetDirectoryName(_queuePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.AppendAllText(_queuePath, JsonSerializer.Serialize(record) + "\n", Encoding.UTF8);
    }

    private List<IssuanceRecord> ReadQueue()
    {
        if (!File.Exists(_queuePath))
            return [];

        var records = new List<IssuanceRecord>();
        var seenSerials = new HashSet<uint>();

        foreach (var line in File.ReadAllLines(_queuePath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            IssuanceRecord? record;

            try
            {
                record = JsonSerializer.Deserialize<IssuanceRecord>(line);
            }
            catch (JsonException)
            {
                // 깨진 줄 하나 때문에 나머지 대기열을 통째로 버릴 수는 없다.
                continue;
            }

            // 같은 건이 두 번 들어가 있으면 한 번만 올린다.
            if (record is not null && seenSerials.Add(record.SerialNumber))
                records.Add(record);
        }

        return records;
    }

    private void WriteQueue(List<IssuanceRecord> records)
    {
        if (records.Count == 0)
        {
            if (File.Exists(_queuePath))
                File.Delete(_queuePath);

            return;
        }

        var builder = new StringBuilder();

        foreach (var record in records)
            builder.Append(JsonSerializer.Serialize(record)).Append('\n');

        File.WriteAllText(_queuePath, builder.ToString(), Encoding.UTF8);
    }

    /// <summary>gRPC 예외 메시지는 여러 줄에 스택까지 붙어 나온다. 첫 줄만 짧게 보여준다.</summary>
    private static string Describe(Exception ex)
    {
        var message = ex.Message.Split('\n')[0].Trim();
        return message.Length > 160 ? message[..160] + "…" : message;
    }
}
