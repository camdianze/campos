using System.Globalization;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Settings;

namespace PharmaPOS.Application.Sync;

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

    public async Task<SyncRunResult> RunAsync(
        string facilityId, CancellationToken cancellationToken = default)
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
                payload = await _builder.BuildAsync(facilityId, includeCatalogue: round == 0);
            }
            catch (Exception exception)
            {
                return SyncRunResult.Failure("The data to send could not be read: " + exception.Message);
            }

            var hasLedgerRows = payload.Transactions.Count > 0 || payload.CounsellingLogs.Count > 0;

            // 첫 바퀴는 원장이 비어 있어도 보낸다 — 상품과 재고는 매번 전체를
            // 올리므로, 판매가 없던 날에도 카탈로그 수정은 서버에 닿아야 한다.
            if (round > 0 && !hasLedgerRows)
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
                    : SyncRunResult.Partial(sent, await CountPendingSafelyAsync(facilityId), result.Message);
            }

            // ── 서버가 확인했다. 이제 적는다. ──
            foreach (var (stream, position) in result.Positions)
            {
                await _repository.SavePositionAsync(stream, position);
            }

            sent += result.Accepted.TryGetValue(SyncStreams.Transactions, out var transactions) ? transactions : 0;
            sent += result.Accepted.TryGetValue(SyncStreams.CounsellingLogs, out var counselling) ? counselling : 0;
            siteId = result.SiteId;
            batches++;

            if (!hasLedgerRows)
            {
                break;
            }
        }

        await RecordLastSuccessAsync();

        var pending = await CountPendingSafelyAsync(facilityId);

        return SyncRunResult.Success(sent, pending, siteId);
    }

    /// <summary>화면에 적을 "밀린 행 수". 못 세어도 동기화를 실패로 만들지 않는다.</summary>
    public async Task<int> CountPendingAsync(string facilityId) =>
        await CountPendingSafelyAsync(facilityId);

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

    private async Task<int> CountPendingSafelyAsync(string facilityId)
    {
        try
        {
            return await _repository.CountPendingAsync(facilityId);
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

    public static SyncRunResult Success(int sent, int pending, string siteId) =>
        new(true, false, Describe(sent, pending, siteId))
        { SentRows = sent, PendingRows = pending, SiteId = siteId };

    public static SyncRunResult Partial(int sent, int pending, string reason) =>
        new(false, true, $"{sent:N0} rows were sent, then it stopped: {reason}")
        { SentRows = sent, PendingRows = pending };

    public static SyncRunResult Failure(string message) =>
        new(false, false, message) { PendingRows = -1 };

    private static string Describe(int sent, int pending, string siteId)
    {
        var who = siteId.Length > 0 ? $" ({siteId})" : string.Empty;

        if (pending > 0)
        {
            return $"{sent:N0} rows sent{who}. {pending:N0} still waiting — press Synchronize again.";
        }

        return sent > 0
            ? $"{sent:N0} rows sent{who}. Everything is up to date."
            : $"Everything is already up to date{who}.";
    }
}
