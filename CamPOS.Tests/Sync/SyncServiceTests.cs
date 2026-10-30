using CamPOS.Application.Repositories;
using CamPOS.Application.Security;
using CamPOS.Application.Settings;
using CamPOS.Application.Sync;

namespace CamPOS.Tests.Sync;

/// <summary>
/// 워터마크를 언제 적느냐가 이 기능의 전부다.
///
/// 너무 일찍 적으면 실패한 묶음의 행들은 다음부터 선택되지 않는다. 그 행들은
/// 영영 서버에 없고, 화면에는 아무 표시도 나지 않으며, 비어 있는 날짜를 누가
/// 알아차릴 때쯤이면 원인은 몇 달 전이다. 그래서 여기에 테스트가 있다.
/// </summary>
public class SyncServiceTests
{
    private const string Facility = "fac-1";

    // ── 적어서는 안 되는 때 ──────────────────────────────────────────────

    /// <summary>이 테스트 하나가 3b의 이유다.</summary>
    [Fact]
    public async Task AFailedSendDoesNotMoveTheWatermark()
    {
        var repository = new FakeSyncRepository(transactions: 3);
        var service = NewService(repository, FakeSyncClient.ThatFails("no network"));

        var result = await service.RunAsync(Facility);

        Assert.False(result.IsSuccess);
        Assert.Empty(repository.SavedPositions);
        Assert.Equal(0, await repository.GetPositionAsync(SyncStreams.Transactions));
    }

    /// <summary>
    /// 실패한 뒤에도 그 행들은 여전히 "보낼 것"으로 남아야 한다. 남지 않으면
    /// 다음 전송이 그것들을 건너뛴다.
    /// </summary>
    [Fact]
    public async Task RowsFromAFailedSendAreStillPendingAfterwards()
    {
        var repository = new FakeSyncRepository(transactions: 3);
        var service = NewService(repository, FakeSyncClient.ThatFails("server down"));

        await service.RunAsync(Facility);

        Assert.Equal(3, await service.CountPendingAsync(Facility));
    }

    [Fact]
    public async Task NothingIsSentWhenTheComputerIsNotConnected()
    {
        var repository = new FakeSyncRepository(transactions: 3);
        var client = FakeSyncClient.ThatSucceeds();
        var service = NewService(repository, client, configured: false);

        var result = await service.RunAsync(Facility);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, client.Sends);
        Assert.Empty(repository.SavedPositions);
    }

    // ── 적어야 하는 때 ──────────────────────────────────────────────────

    [Fact]
    public async Task AConfirmedSendMovesTheWatermark()
    {
        var repository = new FakeSyncRepository(transactions: 3);
        var service = NewService(repository, FakeSyncClient.ThatSucceeds());

        var result = await service.RunAsync(Facility);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, await repository.GetPositionAsync(SyncStreams.Transactions));
        Assert.Equal(0, await service.CountPendingAsync(Facility));
    }

    /// <summary>
    /// 적는 값은 <b>서버가 돌려준 것</b>이지 보낸 묶음에 적혀 있던 것이 아니다.
    /// 보낸 쪽 값을 그대로 쓰면, 서버가 일부만 받은 경우에도 전부 받은 것처럼
    /// 적힌다. 가짜 서버가 일부러 다른 숫자를 돌려주어 그것을 확인한다.
    /// </summary>
    [Fact]
    public async Task TheWatermarkComesFromTheServersAnswer()
    {
        var repository = new FakeSyncRepository(transactions: 3);
        var client = FakeSyncClient.ThatSucceeds(positionOverride: 2);

        await NewService(repository, client).RunAsync(Facility);

        Assert.Equal(2, await repository.GetPositionAsync(SyncStreams.Transactions));
    }

    /// <summary>보낼 원장이 없어도 상품·재고는 매번 올라간다.</summary>
    [Fact]
    public async Task TheCatalogueIsSentEvenOnADayWithNoSales()
    {
        var repository = new FakeSyncRepository(transactions: 0, products: 5);
        var client = FakeSyncClient.ThatSucceeds();

        var result = await NewService(repository, client).RunAsync(Facility);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, client.Sends);
        Assert.Equal(5, client.LastPayload!.Products.Count);
    }

    // ── 여러 묶음 ───────────────────────────────────────────────────────

    /// <summary>
    /// 두 번째 묶음부터는 상품·재고·직원을 싣지 않는다. 같은 수백 개를 묶음마다
    /// 다시 올릴 이유가 없다.
    /// </summary>
    [Fact]
    public async Task OnlyTheFirstBatchCarriesTheCatalogue()
    {
        var repository = new FakeSyncRepository(
            transactions: SyncPayloadBuilder.BatchRows + 10, products: 5);
        var client = FakeSyncClient.ThatSucceeds();

        await NewService(repository, client).RunAsync(Facility);

        Assert.Equal(2, client.Sends);
        Assert.Equal(5, client.PayloadsSent[0].Products.Count);
        Assert.Empty(client.PayloadsSent[1].Products);
    }

    /// <summary>
    /// 두 묶음 중 뒤의 것이 실패하면, 앞의 것은 이미 서버에 들어가 있다.
    /// 그것을 "실패"라고만 말하면 올라간 행까지 안 간 것처럼 읽힌다.
    /// </summary>
    [Fact]
    public async Task WhenTheSecondBatchFailsTheFirstOnesWatermarkStays()
    {
        var repository = new FakeSyncRepository(
            transactions: SyncPayloadBuilder.BatchRows + 10);
        var client = FakeSyncClient.ThatSucceedsThenFails(afterSends: 1, reason: "connection lost");

        var result = await NewService(repository, client).RunAsync(Facility);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPartial);
        Assert.Equal(SyncPayloadBuilder.BatchRows, await repository.GetPositionAsync(SyncStreams.Transactions));
        Assert.Equal(10, await repository.CountPendingAsync(Facility));
    }

    // ── 사진 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 원장과 같은 규칙이다 — 서버가 답하기 전에 "올림"으로 표시하면 그 사진은
    /// 다시 선택되지 않고, 서버에는 영영 없다.
    /// </summary>
    [Fact]
    public async Task APhotoThatFailedToUploadIsNotMarkedAsUploaded()
    {
        var repository = new FakeSyncRepository(transactions: 0);
        repository.Photos.Add(NewPhoto("p1", "aaa"));

        var client = FakeSyncClient.ThatSucceeds();
        client.PhotosFail = true;

        await NewService(repository, client).RunAsync(Facility);

        Assert.Empty(repository.MarkedUploaded);
        Assert.Equal(1, await repository.CountPhotosToUploadAsync());
    }

    [Fact]
    public async Task AnUploadedPhotoIsMarkedAndNotSentAgain()
    {
        var repository = new FakeSyncRepository(transactions: 0);
        repository.Photos.Add(NewPhoto("p1", "aaa"));

        var client = FakeSyncClient.ThatSucceeds();
        var service = NewService(repository, client);

        await service.RunAsync(Facility);
        Assert.Equal(1, client.PhotoUploads);

        await service.RunAsync(Facility);
        Assert.Equal(1, client.PhotoUploads);
    }

    /// <summary>
    /// 서버가 "이미 같은 것이 있다"고 답한 것도 표시해야 한다. 표시하지 않으면
    /// 그 사진을 동기화할 때마다 다시 올린다 — 매번 성공하므로 아무 표시도 나지
    /// 않고, 약국의 회선만 쓴다.
    /// </summary>
    [Fact]
    public async Task APhotoTheServerAlreadyHadIsStillMarked()
    {
        var repository = new FakeSyncRepository(transactions: 0);
        repository.Photos.Add(NewPhoto("p1", "aaa"));

        var client = FakeSyncClient.ThatSucceeds();
        client.PhotosAlreadyThere = true;

        await NewService(repository, client).RunAsync(Facility);

        Assert.Single(repository.MarkedUploaded);
        Assert.Equal(0, await repository.CountPhotosToUploadAsync());
    }

    /// <summary>한 장이 실패해도 나머지는 간다.</summary>
    [Fact]
    public async Task OnePhotoFailingDoesNotStopTheLedgerFromBeingSent()
    {
        var repository = new FakeSyncRepository(transactions: 2);
        repository.Photos.Add(NewPhoto("p1", "aaa"));

        var client = FakeSyncClient.ThatSucceeds();
        client.PhotosFail = true;

        var result = await NewService(repository, client).RunAsync(Facility);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await repository.GetPositionAsync(SyncStreams.Transactions));
    }

    private static SyncPhoto NewPhoto(string productId, string sha256) =>
        new() { ProductId = productId, Bytes = [1, 2, 3], Sha256 = sha256 };

    /// <summary>
    /// Data Center를 나갔다 다시 들어오면 ViewModel이 새로 생기고 "보내는 중"
    /// 표시가 초기화된다. 그 상태에서 다시 누르면 두 전송이 겹치는데, 겹쳐도
    /// 데이터는 깨지지 않지만 약국 회선으로 같은 것을 두 번 올리게 되고 결과
    /// 문구 두 개가 서로를 덮어쓴다.
    /// </summary>
    [Fact]
    public async Task TwoRunsAtOnceAreRefusedRatherThanOverlapping()
    {
        var repository = new FakeSyncRepository(transactions: 1);
        var client = FakeSyncClient.ThatSucceeds();
        client.BlockUntilReleased = true;

        var service = NewService(repository, client);

        var first = service.RunAsync(Facility);
        await client.WaitUntilInsideSendAsync();

        var second = await service.RunAsync(Facility);

        Assert.False(second.IsSuccess);
        Assert.Contains("already running", second.Message, StringComparison.OrdinalIgnoreCase);

        client.Release();
        Assert.True((await first).IsSuccess);
        Assert.Equal(1, client.Sends);
    }

    // ── 마지막 성공 시각 ────────────────────────────────────────────────

    [Fact]
    public async Task TheLastSuccessTimeIsOnlyWrittenAfterASuccess()
    {
        var settings = new FakeSettings();
        var repository = new FakeSyncRepository(transactions: 1);

        var failing = NewService(repository, FakeSyncClient.ThatFails("nope"), settings: settings);
        await failing.RunAsync(Facility);
        Assert.Null(await failing.GetLastSuccessAsync());

        var working = NewService(repository, FakeSyncClient.ThatSucceeds(), settings: settings);
        await working.RunAsync(Facility);
        Assert.NotNull(await working.GetLastSuccessAsync());
    }

    // ── 받침대 ──────────────────────────────────────────────────────────

    private static SyncService NewService(
        FakeSyncRepository repository,
        FakeSyncClient client,
        bool configured = true,
        FakeSettings? settings = null)
    {
        settings ??= new FakeSettings();

        if (configured)
        {
            settings.Values[AppSettingKeys.SyncServerUrl] = "https://abc.supabase.co";
            settings.Values[AppSettingKeys.SyncPublishableKey] = "sb_publishable_x";
            settings.Values[AppSettingKeys.SyncToken] = "token";
        }

        return new SyncService(
            new SyncPayloadBuilder(repository),
            repository,
            client,
            new SyncSettingsService(settings, new PassThroughProtector()),
            settings);
    }

    private sealed class FakeSyncRepository : ISyncRepository
    {
        private readonly List<(long RowId, SyncTransaction Row)> _transactions = new();
        private readonly int _products;
        private readonly Dictionary<string, long> _positions = new();

        public FakeSyncRepository(int transactions, int products = 0)
        {
            _products = products;

            for (var i = 1; i <= transactions; i++)
            {
                _transactions.Add((i, new SyncTransaction
                {
                    TransactionId = "t" + i,
                    ProductId = "p",
                    UserId = "u",
                    TransactionType = "StockOut",
                    BatchNumber = "b",
                    ExpiryDate = 0,
                    Quantity = 1,
                    TransactionTime = 0,
                }));
            }
        }

        public List<(string Stream, long Position)> SavedPositions { get; } = new();

        public Task<IReadOnlyList<SyncProduct>> GetProductsAsync() =>
            Task.FromResult<IReadOnlyList<SyncProduct>>(Enumerable.Range(1, _products)
                .Select(i => new SyncProduct
                {
                    ProductId = "p" + i,
                    ProductName = "n",
                    Unit = "ea",
                    UnitsPerBox = 1,
                    SellsLoose = false,
                    IsCombination = false,
                    SafetyStockLevel = 0,
                    Status = "Active",
                    CostPrice = 0m,
                    SellingPrice = 1m,
                    CreatedAt = 0,
                })
                .ToList());

        public Task<IReadOnlyList<SyncInventory>> GetInventoryAsync(string facilityId) =>
            Task.FromResult<IReadOnlyList<SyncInventory>>([]);

        public Task<IReadOnlyList<SyncUser>> GetUsersAsync(string facilityId) =>
            Task.FromResult<IReadOnlyList<SyncUser>>([]);

        public Task<IReadOnlyList<(long RowId, SyncTransaction Row)>> GetTransactionsAfterAsync(
            string facilityId, long afterRowId, int limit) =>
            Task.FromResult<IReadOnlyList<(long, SyncTransaction)>>(
                _transactions.Where(t => t.RowId > afterRowId).Take(limit).ToList());

        public Task<IReadOnlyList<(long RowId, SyncCounsellingLog Row)>> GetCounsellingLogsAfterAsync(
            long afterRowId, int limit) =>
            Task.FromResult<IReadOnlyList<(long, SyncCounsellingLog)>>([]);

        public Task<IReadOnlyList<(long RowId, SyncUsageEvent Row)>> GetUsageEventsAfterAsync(
            long afterRowId, int limit) =>
            Task.FromResult<IReadOnlyList<(long, SyncUsageEvent)>>([]);

        public Task<int> CountPendingAsync(string facilityId) =>
            Task.FromResult(_transactions.Count(
                t => t.RowId > (_positions.TryGetValue(SyncStreams.Transactions, out var p) ? p : 0)));

        public List<SyncPhoto> Photos { get; } = new();

        public List<(string ProductId, string Sha256)> MarkedUploaded { get; } = new();

        public Task<IReadOnlyList<SyncPhoto>> GetPhotosToUploadAsync(int limit) =>
            Task.FromResult<IReadOnlyList<SyncPhoto>>(Photos
                .Where(p => !MarkedUploaded.Any(m => m.ProductId == p.ProductId && m.Sha256 == p.Sha256))
                .Take(limit)
                .ToList());

        public Task MarkPhotoUploadedAsync(string productId, string sha256)
        {
            MarkedUploaded.Add((productId, sha256));
            return Task.CompletedTask;
        }

        public Task<int> CountPhotosToUploadAsync() =>
            Task.FromResult(Photos.Count(
                p => !MarkedUploaded.Any(m => m.ProductId == p.ProductId && m.Sha256 == p.Sha256)));

        public Task<long> GetPositionAsync(string stream) =>
            Task.FromResult(_positions.TryGetValue(stream, out var p) ? p : 0L);

        public Task SavePositionAsync(string stream, long position)
        {
            SavedPositions.Add((stream, position));
            _positions[stream] = position;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSyncClient : ISyncClient
    {
        private bool _succeeds;
        private int _failAfter = int.MaxValue;
        private string _reason = string.Empty;
        private long? _positionOverride;

        public int Sends { get; private set; }

        public List<SyncPayload> PayloadsSent { get; } = new();

        public SyncPayload? LastPayload => PayloadsSent.Count > 0 ? PayloadsSent[^1] : null;

        public static FakeSyncClient ThatSucceeds(long? positionOverride = null) =>
            new() { _succeeds = true, _positionOverride = positionOverride };

        public static FakeSyncClient ThatFails(string reason) =>
            new() { _succeeds = false, _reason = reason };

        public static FakeSyncClient ThatSucceedsThenFails(int afterSends, string reason) =>
            new() { _succeeds = true, _failAfter = afterSends, _reason = reason };

        public int PhotoUploads { get; private set; }

        public bool PhotosFail { get; set; }

        public bool PhotosAlreadyThere { get; set; }

        public Task<SyncPhotoResult> UploadPhotoAsync(
            SyncSettings settings, SyncPhoto photo, CancellationToken cancellationToken = default)
        {
            PhotoUploads++;

            if (PhotosFail)
            {
                return Task.FromResult(SyncPhotoResult.Failure("no network"));
            }

            return Task.FromResult(PhotosAlreadyThere
                ? SyncPhotoResult.AlreadyThere()
                : SyncPhotoResult.Uploaded());
        }

        public bool BlockUntilReleased { get; set; }

        private readonly TaskCompletionSource _insideSend = new();
        private readonly TaskCompletionSource _released = new();

        public Task WaitUntilInsideSendAsync() => _insideSend.Task;

        public void Release() => _released.TrySetResult();

        public async Task<SyncSendResult> SendAsync(
            SyncSettings settings, SyncPayload payload, CancellationToken cancellationToken = default)
        {
            Sends++;
            PayloadsSent.Add(payload);

            if (BlockUntilReleased)
            {
                _insideSend.TrySetResult();
                await _released.Task;
            }

            if (!_succeeds || Sends > _failAfter)
            {
                return SyncSendResult.Failure(_reason);
            }

            var positions = payload.Positions.ToDictionary(
                entry => entry.Key,
                entry => _positionOverride ?? entry.Value);

            var accepted = new Dictionary<string, int>
            {
                [SyncStreams.Transactions] = payload.Transactions.Count,
                [SyncStreams.CounsellingLogs] = payload.CounsellingLogs.Count,
            };

            return SyncSendResult.Success("ok", "DEV-001", accepted, positions);
        }
    }

    private sealed class FakeSettings : IAppSettingRepository
    {
        public Dictionary<string, string> Values { get; } = new();

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(Values.TryGetValue(key, out var v) ? v : null);

        public Task<IReadOnlyDictionary<string, string>> GetManyAsync(IReadOnlyList<string> keys) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                keys.Where(Values.ContainsKey).ToDictionary(k => k, k => Values[k]));

        public Task SetAsync(string key, string value, string? valueType = null, string? updatedBy = null)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughProtector : IRecoveryDataProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string encryptedText) => encryptedText;
    }
}
