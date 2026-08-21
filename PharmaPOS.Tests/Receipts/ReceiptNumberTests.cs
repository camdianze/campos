using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Receipts;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;

namespace PharmaPOS.Tests.Receipts;

/// <summary>
/// 영수증 번호 발급. 실제 SQLite에 대고 돈다 — 번호 중복은 스키마와 트랜잭션이
/// 함께 막는 것이라 가짜 저장소로는 확인할 수 없다.
/// </summary>
public class ReceiptNumberTests : IDisposable
{
    /// <summary>2026-08-21 07:30 UTC = 프놈펜 14:30, 같은 날.</summary>
    private const long MorningUtc = 1_787_297_400_000;

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ReceiptNumberService _service;

    public ReceiptNumberTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-receipt-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();

        _service = new ReceiptNumberService(new ReceiptNumberRepository(_connectionFactory));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // 임시 폴더가 남는 것은 결과에 영향을 주지 않는다.
        }
    }

    private static ReceiptSettings Settings(
        string prefix = "INV",
        ReceiptNumberResetCycle cycle = ReceiptNumberResetCycle.Daily) => new()
    {
        ReceiptPrefix = prefix,
        ResetCycle = cycle
    };

    /// <summary>프놈펜 기준으로 days일 뒤의 같은 시각.</summary>
    private static long DaysLater(int days) =>
        MorningUtc + (long)TimeSpan.FromDays(days).TotalMilliseconds;

    [Fact]
    public async Task IssueAsync_FormatsPrefixDateAndSequence()
    {
        var number = await _service.IssueAsync("sale-1", Settings(), MorningUtc);

        Assert.Equal("INV-20260821-0001", number);
    }

    [Fact]
    public async Task IssueAsync_CountsUpWithinTheSameDay()
    {
        var first = await _service.IssueAsync("sale-1", Settings(), MorningUtc);
        var second = await _service.IssueAsync("sale-2", Settings(), MorningUtc + 60_000);

        Assert.Equal("INV-20260821-0001", first);
        Assert.Equal("INV-20260821-0002", second);
    }

    /// <summary>
    /// 같은 판매를 다시 출력하면 처음 번호가 그대로 나온다.
    /// 재출력마다 새 번호가 나가면 환불 대조가 깨진다.
    /// </summary>
    [Fact]
    public async Task IssueAsync_ReusesTheNumberForTheSameSale()
    {
        var first = await _service.IssueAsync("sale-1", Settings(), MorningUtc);
        var reprint = await _service.IssueAsync("sale-1", Settings(), MorningUtc);
        var next = await _service.IssueAsync("sale-2", Settings(), MorningUtc);

        Assert.Equal(first, reprint);
        // 재출력이 일련번호를 소모하지 않았다.
        Assert.Equal("INV-20260821-0002", next);
    }

    [Fact]
    public async Task IssueAsync_RestartsEachDayWhenTheCycleIsDaily()
    {
        await _service.IssueAsync("sale-1", Settings(), MorningUtc);
        var nextDay = await _service.IssueAsync("sale-2", Settings(), DaysLater(1));

        Assert.Equal("INV-20260822-0001", nextDay);
    }

    [Fact]
    public async Task IssueAsync_KeepsCountingAcrossDaysWhenTheCycleIsMonthly()
    {
        var settings = Settings(cycle: ReceiptNumberResetCycle.Monthly);

        await _service.IssueAsync("sale-1", settings, MorningUtc);
        var nextDay = await _service.IssueAsync("sale-2", settings, DaysLater(1));

        // 날짜 부분은 그날 날짜지만 일련번호는 이어진다.
        Assert.Equal("INV-20260822-0002", nextDay);
    }

    [Fact]
    public async Task IssueAsync_RestartsEachMonthWhenTheCycleIsMonthly()
    {
        var settings = Settings(cycle: ReceiptNumberResetCycle.Monthly);

        await _service.IssueAsync("sale-1", settings, MorningUtc);
        var nextMonth = await _service.IssueAsync("sale-2", settings, DaysLater(15));

        Assert.Equal("INV-20260905-0001", nextMonth);
    }

    [Fact]
    public async Task IssueAsync_NeverRestartsWhenTheCycleIsNever()
    {
        var settings = Settings(cycle: ReceiptNumberResetCycle.Never);

        await _service.IssueAsync("sale-1", settings, MorningUtc);
        var nextMonth = await _service.IssueAsync("sale-2", settings, DaysLater(15));

        Assert.Equal("INV-20260905-0002", nextMonth);
    }

    /// <summary>
    /// 하루 경계는 프놈펜 시간으로 갈린다. 서버(PC) 시간대를 따르면
    /// 같은 영업일의 판매가 두 날짜로 갈라진다.
    /// </summary>
    [Fact]
    public async Task IssueAsync_UsesPhnomPenhTimeForTheDateAndTheCycle()
    {
        // 2026-08-21 18:00 UTC = 프놈펜 2026-08-22 01:00. UTC로는 아직 21일이다.
        var lateUtc = 1_787_335_200_000;

        var number = await _service.IssueAsync("sale-1", Settings(), lateUtc);

        Assert.Equal("INV-20260822-0001", number);
    }

    /// <summary>접두어가 비어 있으면 번호를 만들지 않는다. 번호 없이 인쇄될 뿐이다.</summary>
    [Fact]
    public async Task IssueAsync_ReturnsNullWithoutAPrefix()
    {
        Assert.Null(await _service.IssueAsync("sale-1", Settings(prefix: "  "), MorningUtc));
    }

    /// <summary>동시에 확정된 판매들이 같은 번호를 받으면 안 된다.</summary>
    [Fact]
    public async Task IssueAsync_GivesEveryConcurrentSaleItsOwnNumber()
    {
        var issued = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(index =>
                _service.IssueAsync("sale-" + index, Settings(), MorningUtc)));

        Assert.Equal(20, issued.Distinct().Count());
    }
}
