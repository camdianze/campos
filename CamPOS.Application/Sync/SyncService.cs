using System.Globalization;
using CamPOS.Application.Repositories;
using CamPOS.Application.Settings;

namespace CamPOS.Application.Sync;

/// <summary>
/// 한 번의 동기화. 묶음을 만들고, 보내고, <b>서버가 확인해 준 뒤에만</b> 어디까지
/// 보냈는지 적는다.
///
/// 그 순서가 이 클래스의 전부다. 보내기 전에 적거나, 응답을 보지 않고 적으면,
/// 실패한 묶음의 행들은 다음부터 선택되지 않는다 — 그 행들은 영영 서버에 없고,
/// 화면에는 아무 표시도 나지 않는다. 비어 있는 날짜를 누가 알아차릴 때쯤이면
/// 원인은 몇 달 전이다.
///
/// ViewModel이 아니라 Application에 두는 이유도 같다. 이 순서는 화면의 사정이
/// 아니라 규칙이고, 규칙은 테스트가 붙잡을 수 있는 자리에 있어야 한다.
/// </summary>
public sealed class SyncService
{
    /// <summary>
    /// 한 번 눌렀을 때 보낼 묶음의 최대 개수. 밀린 행이 많으면 여러 묶음으로
    /// 나뉘는데, 무한정 돌면 사람이 멈출 수 없는 작업이 된다. 500행씩이므로
    /// 한 번에 최대 1만 행이고, 남으면 화면이 몇 개 남았는지 말한다.
    /// </summary>
    public const int MaxBatchesPerRun = 20;

    /// <summary>
    /// 한 번 눌렀을 때 사진에 쓸 시간. <b>장수가 아니라 시간으로 끊는다</b> —
    /// 사람이 신경 쓰는 것은 얼마나 기다리느냐이지 몇 장이냐가 아니고, 회선이
    /// 빠르면 한 번에 다 가야 한다. 장수로 끊으면 300장짜리 약국은 빠른 회선에서도
    /// 버튼을 여섯 번 눌러야 했다.
    /// </summary>
    public static readonly TimeSpan PhotoTimeBudget = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 시간 예산과 별개의 안전장치. 한 장이 즉시 실패해 되돌아오는 상태에서는
    /// 2분 동안 수천 번을 돌 수 있다.
    /// </summary>
    public const int MaxPhotosPerRun = 2000;

    /// <summary>
    /// 한 번에 하나만 돈다. <c>static</c>인 이유는 이 서비스가 Transient라 화면마다
    /// 새 인스턴스를 받기 때문이다 — Data Center를 나갔다 다시 들어오면 두 번째
    /// 인스턴스가 생기고, 인스턴스 잠금으로는 서로를 못 본다.
    ///
    /// 두 번 겹쳐도 데이터가 깨지지는 않는다(키가 (site_id, 원본 ID)라 제자리에서
    /// 갱신되고, 워터마크는 뒤로 가도 그 행들이 한 번 더 갈 뿐이다). 막는 이유는
    /// 약국 회선으로 같은 것을 두 번 올리는 일과, 두 개의 결과 문구가 서로를
    /// 덮어써서 무엇이 실제로 일어났는지 알 수 없게 되는 쪽이다.
    /// </summary>
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);

    private readonly SyncPayloadBuilder _builder;
    private readonly ISyncRepository _repository;
    private readonly ISyncClient _client;
    private readonly SyncSettingsService _settingsService;
    private readonly IAppSettingRepository _settings;

    public SyncService(
        SyncPayloadBuilder builder,
        ISyncRepository repository,
        ISyncClient client,
        SyncSettingsService settingsService,
        IAppSettingRepository settings)
    {
        _builder = builder;
        _repository = repository;
        _client = client;
        _settingsService = settingsService;
        _settings = settings;
    }

    /// <param name="allowPharmacyData">
    /// 약국 데이터를 담아도 되는 호출인가. 자동 전송이 꺼진 약국에서 시계가
    /// 도는 경우가 false다 — 그때도 <b>사용 기록은 간다</b>. 그것은 이용 조건이고
    /// 약국이 고르는 값이 아니다.
    ///
    /// 실제로 담기는지는 <c>sync.pharmacyData</c> 설정과 함께 정해진다. 둘 중
    /// 하나라도 아니라고 하면 사용 기록만 간다.
    /// </param>
    public async Task<SyncRunResult> RunAsync(
        string facilityId,
        CancellationToken cancellationToken = default,
        bool allowPharmacyData = true)
    {
        if (!await OneAtATime.WaitAsync(0, cancellationToken))
        {
            return SyncRunResult.Failure("A sync is already running. Wait for it to finish.");
        }

        try
        {
            var includePharmacyData =
                allowPharmacyData && await IsPharmacyDataEnabledAsync();

            return await RunOnceAsync(facilityId, includePharmacyData, cancellationToken);
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private async Task<SyncRunResult> RunOnceAsync(
        string facilityId, bool includePharmacyData, CancellationToken cancellationToken)
    {
        SyncSettings settings;

        try
        {
            settings = await _settingsService.LoadAsync();
        }
        catch (Exception exception)
        {
            return SyncRunResult.Failure("The server connection could not be read: " + exception.Message);
        }

        if (!settings.IsConfigured)
        {
            return SyncRunResult.Failure("This computer is not connected to a server yet. Fill in the fields above and press Save.");
        }

        var sent = 0;
        var batches = 0;
        var siteId = string.Empty;

        for (var round = 0; round < MaxBatchesPerRun; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SyncPayload payload;

            try
            {
                // 첫 묶음에만 상품·재고·직원을 싣는다. 두 번째부터는 원장만 —
                // 같은 수백 개의 상품을 묶음마다 다시 올릴 이유가 없다.
                payload = await _builder.BuildAsync(
                    facilityId,
                    includeCatalogue: round == 0,
                    includePharmacyData: includePharmacyData);
            }
            catch (Exception exception)
            {
                return SyncRunResult.Failure("The data to send could not be read: " + exception.Message);
            }

            // 사용 기록도 세야 한다. 빼 두면 사용 기록만 500건 넘게 밀렸을 때
            // 둘째 바퀴에서 멈춰 버리고, 나머지는 다음 전송까지 그대로 남는다.
            var hasRowsToSend =
                payload.Transactions.Count > 0
                || payload.CounsellingLogs.Count > 0
                || payload.UsageEvents.Count > 0;

            // 사용 기록만 보내는 약국에서는 보낼 것이 없으면 아예 보내지 않는다.
            // 카탈로그가 실리지 않으므로 빈 묶음을 보낼 이유가 없고, 30분마다
            // sync_batch에 빈 행만 쌓으면 "이 약국이 살아 있나"를 그 표에서
            // 읽을 수 없게 된다.
            if (!includePharmacyData && !hasRowsToSend)
            {
                break;
            }

            // 첫 바퀴는 원장이 비어 있어도 보낸다 — 상품과 재고는 매번 전체를
            // 올리므로, 판매가 없던 날에도 카탈로그 수정은 서버에 닿아야 한다.
            if (round > 0 && !hasRowsToSend)
            {
                break;
            }

            var result = await _client.SendAsync(settings, payload, cancellationToken);

            if (!result.IsSuccess)
            {
                // 여기서 위치를 적지 않는 것이 핵심이다. 적으면 이 묶음의 행들은
                // 다시 선택되지 않는다.
                return batches == 0
                    ? SyncRunResult.Failure(result.Message)
                    : SyncRunResult.Partial(
                        sent,
                        await CountPendingSafelyAsync(facilityId, includePharmacyData),
                        result.Message);
            }

            // ── 서버가 확인했다. 이제 적는다. ──
            foreach (var (stream, position) in result.Positions)
            {
                await _repository.SavePositionAsync(stream, position);
            }

            sent += result.Accepted.TryGetValue(SyncStreams.Transactions, out var transactions) ? transactions : 0;
            sent += result.Accepted.TryGetValue(SyncStreams.CounsellingLogs, out var counselling) ? counselling : 0;
            sent += result.Accepted.TryGetValue(SyncStreams.UsageEvents, out var usage) ? usage : 0;
            siteId = result.SiteId;
            batches++;

            // 이 바퀴에서 보낼 행이 더 없었으면 멈춘다. 사용 기록도 여기 포함돼야
            // 한다 — 빼 두면 사용 기록이 한 묶음(500건)을 넘게 밀렸을 때 나머지가
            // 다음 전송까지 남는다.
            if (!hasRowsToSend)
            {
                break;
            }
        }

        // ── 사진 ──
        // 원장이 먼저다. 사진은 늦게 올라가도 재고와 매출은 맞지만, 그 반대는
        // 아니다 — 느린 회선에서 사진을 먼저 올리다 시간이 다 가면 그날 판매가
        // 서버에 없다.
        var photos = await UploadPhotosAsync(settings, cancellationToken);

        await RecordLastSuccessAsync();

        var pending = await CountPendingSafelyAsync(facilityId, includePharmacyData);

        return SyncRunResult.Success(sent, pending, siteId, photos.Uploaded, await CountPhotosSafelyAsync());
    }

    /// <summary>
    /// 바뀐 사진을 올린다. 한 장이 실패해도 나머지는 계속 간다 — 사진 하나 때문에
    /// 나머지 사진이 전부 밀릴 이유가 없고, 실패한 것은 표시를 안 하므로 다음에
    /// 다시 선택된다.
    ///
    /// <b>올렸다는 표시는 서버가 답한 뒤에만</b> 한다. 원장의 워터마크와 같은 규칙이다.
    /// </summary>
    private async Task<(int Uploaded, int Failed)> UploadPhotosAsync(
        SyncSettings settings, CancellationToken cancellationToken)
    {
        var uploaded = 0;
        var failed = 0;

        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();

            // 바이트까지 읽으므로 한 번에 조금씩 가져온다.
            for (var taken = 0; taken < MaxPhotosPerRun; taken += PhotoBatch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (clock.Elapsed > PhotoTimeBudget)
                {
                    break;
                }

                var photos = await _repository.GetPhotosToUploadAsync(PhotoBatch);

                if (photos.Count == 0)
                {
                    break;
                }

                foreach (var photo in photos)
                {
                    if (clock.Elapsed > PhotoTimeBudget)
                    {
                        break;
                    }

                    var result = await _client.UploadPhotoAsync(settings, photo, cancellationToken);

                    if (!result.IsSuccess)
                    {
                        failed++;
                        continue;
                    }

                    // 서버가 "이미 있다"고 한 경우도 표시한다. 안 하면 그 사진을
                    // 매번 다시 올린다.
                    await _repository.MarkPhotoUploadedAsync(photo.ProductId, photo.Sha256);

                    if (!result.Skipped)
                    {
                        uploaded++;
                    }
                }

                // 전부 실패했다면 다음 묶음도 같은 이유로 실패한다. 회선이 끊긴
                // 상태에서 50장을 하나씩 시도하며 기다릴 이유가 없다.
                if (failed >= photos.Count)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 사진을 못 올린 것으로 원장 전송까지 실패로 만들지 않는다.
        }

        return (uploaded, failed);
    }

    private const int PhotoBatch = 10;

    private async Task<int> CountPhotosSafelyAsync()
    {
        try
        {
            return await _repository.CountPhotosToUploadAsync();
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// 화면에 적을 "밀린 행 수". 못 세어도 동기화를 실패로 만들지 않는다.
    ///
    /// 약국 데이터를 끈 약국에서는 사용 기록만 센다. 보내지 않기로 한 원장을
    /// 세면 영영 줄지 않는 숫자가 화면에 남고, 상단 바 배지가 그것을 고장으로
    /// 읽어 계속 경고한다.
    /// </summary>
    public async Task<int> CountPendingAsync(string facilityId) =>
        await CountPendingSafelyAsync(facilityId, await IsPharmacyDataEnabledAsync());

    /// <summary>아직 안 올린 사진 수. 바이트는 읽지 않는다.</summary>
    public async Task<int> CountPhotosPendingAsync() =>
        await CountPhotosSafelyAsync();

    /// <summary>자동 전송이 켜져 있나. 설정이 없으면 켜진 것으로 읽는다.</summary>
    public async Task<bool> IsAutomaticAsync()
    {
        try
        {
            var stored = await _settings.GetAsync(AppSettingKeys.SyncAutomatic);

            // 저장된 빈 문자열은 값이지 "없음"이 아니지만, 여기서는 둘 다 기본값으로
            // 간다 — 읽을 수 없는 설정이 전송을 멈추게 해서는 안 된다.
            return stored is null || stored.Length == 0
                || !bool.TryParse(stored, out var on) || on;
        }
        catch
        {
            return true;
        }
    }

    public async Task SetAutomaticAsync(bool on)
    {
        await _settings.SetAsync(
            AppSettingKeys.SyncAutomatic,
            on ? "true" : "false",
            "bool");
    }

    /// <summary>
    /// 약국 데이터를 보내도 되나. 설정이 없으면 켜진 것으로 읽는다 — 거절은
    /// 약국이 분명히 밝힌 경우에만이고, 읽을 수 없는 설정이 전송을 멈추게
    /// 해서는 안 된다(자동 전송 설정과 같은 규칙이다).
    /// </summary>
    public async Task<bool> IsPharmacyDataEnabledAsync()
    {
        try
        {
            var stored = await _settings.GetAsync(AppSettingKeys.SyncPharmacyData);

            return stored is null || stored.Length == 0
                || !bool.TryParse(stored, out var on) || on;
        }
        catch
        {
            return true;
        }
    }

    public async Task SetPharmacyDataAsync(bool on)
    {
        await _settings.SetAsync(
            AppSettingKeys.SyncPharmacyData,
            on ? "true" : "false",
            "bool");
    }

    /// <summary>
    /// 상단 바 배지를 띄울지 정하는 판단. 로그인할 때 한 번 읽는다.
    /// </summary>
    public async Task<SyncHealth> GetHealthAsync(string facilityId)
    {
        try
        {
            var settings = await _settingsService.LoadAsync();

            return SyncHealth.Of(
                settings.IsConfigured,
                await IsAutomaticAsync(),
                await GetLastSuccessAsync(),
                // 보내지 않기로 한 원장은 밀린 것이 아니다. 세면 배지가
                // 영영 경고한 채로 남고, 그러면 진짜 고장 때 아무도 안 본다.
                await CountPendingSafelyAsync(facilityId, await IsPharmacyDataEnabledAsync())
                    + await CountPhotosSafelyAsync());
        }
        catch
        {
            return SyncHealth.Unknown();
        }
    }

    public async Task<DateTimeOffset?> GetLastSuccessAsync()
    {
        try
        {
            var stored = await _settings.GetAsync(AppSettingKeys.SyncLastSuccess);

            // 기계가 다시 읽는 값이므로 InvariantCulture로 읽는다.
            return stored is not null
                && long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochMs)
                ? DateTimeOffset.FromUnixTimeMilliseconds(epochMs)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task RecordLastSuccessAsync()
    {
        try
        {
            await _settings.SetAsync(
                AppSettingKeys.SyncLastSuccess,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
                "number");
        }
        catch
        {
            // 전송은 이미 성공했다. 시각을 못 적은 것으로 성공을 취소하지 않는다.
        }
    }

    private async Task<int> CountPendingSafelyAsync(
        string facilityId, bool includePharmacyData = true)
    {
        try
        {
            return await _repository.CountPendingAsync(facilityId, includePharmacyData);
        }
        catch
        {
            return -1;
        }
    }
}

/// <summary>
/// 동기화 한 번의 결과. 성공과 실패 말고 <see cref="IsPartial"/>이 따로 있는 이유는,
/// 여러 묶음 중 앞의 것은 들어가고 뒤의 것이 실패한 상태가 실제로 생기기 때문이다.
/// 그것을 실패라고만 말하면 이미 올라간 행까지 안 간 것처럼 읽힌다.
/// </summary>
public sealed class SyncRunResult
{
    private SyncRunResult(bool isSuccess, bool isPartial, string message)
    {
        IsSuccess = isSuccess;
        IsPartial = isPartial;
        Message = message;
    }

    public bool IsSuccess { get; }

    public bool IsPartial { get; }

    public string Message { get; }

    /// <summary>이번에 서버가 받은 원장·상담 행 수.</summary>
    public int SentRows { get; private init; }

    /// <summary>아직 남은 행 수. 셀 수 없었으면 -1.</summary>
    public int PendingRows { get; private init; }

    public string SiteId { get; private init; } = string.Empty;

    /// <summary>이번에 올린 사진 수.</summary>
    public int UploadedPhotos { get; private init; }

    /// <summary>아직 안 올린 사진 수. 셀 수 없었으면 -1.</summary>
    public int PendingPhotos { get; private init; }

    public static SyncRunResult Success(
        int sent, int pending, string siteId, int uploadedPhotos = 0, int pendingPhotos = 0) =>
        new(true, false, Describe(sent, pending, siteId, uploadedPhotos, pendingPhotos))
        {
            SentRows = sent, PendingRows = pending, SiteId = siteId,
            UploadedPhotos = uploadedPhotos, PendingPhotos = pendingPhotos,
        };

    public static SyncRunResult Partial(int sent, int pending, string reason) =>
        new(false, true, $"{sent:N0} rows were sent, then it stopped: {reason}")
        { SentRows = sent, PendingRows = pending };

    public static SyncRunResult Failure(string message) =>
        new(false, false, message) { PendingRows = -1 };

    private static string Describe(
        int sent, int pending, string siteId, int uploadedPhotos, int pendingPhotos)
    {
        var who = siteId.Length > 0 ? $" ({siteId})" : string.Empty;
        var photos = uploadedPhotos > 0 ? $", {uploadedPhotos:N0} photos" : string.Empty;

        if (pending > 0 || pendingPhotos > 0)
        {
            var waiting = pendingPhotos > 0 && pending > 0
                ? $"{pending:N0} rows and {pendingPhotos:N0} photos"
                : pending > 0 ? $"{pending:N0} rows" : $"{pendingPhotos:N0} photos";

            return $"{sent:N0} rows{photos} sent{who}. {waiting} still waiting — press Synchronize again.";
        }

        return sent > 0 || uploadedPhotos > 0
            ? $"{sent:N0} rows{photos} sent{who}. Everything is up to date."
            : $"Everything is already up to date{who}.";
    }
}
