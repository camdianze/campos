using Microsoft.Data.Sqlite;
using CamPOS.Application.Counselling;
using CamPOS.Application.Reports;
using CamPOS.DataAccess.Database;
using CamPOS.DataAccess.Repositories;
using CamPOS.Domain.Enums;

namespace CamPOS.Tests.Reports;

/// <summary>
/// 항생제 리포트 오른쪽 아래 "출력 현황" 패널.
///
/// 이 패널은 숫자를 <b>두 곳</b>에서 가져온다. 출력률과 인쇄 건수는 리포트 집계
/// (<see cref="ReportData"/>)에서, 건너뛴 이유별 건수는 <see cref="CounsellingMetrics"/>에서다.
/// 같은 로그를 두 쿼리가 각자 세므로, 둘이 어긋나면 한 패널 안의 숫자가 서로 맞지 않는다 —
/// "8건 중 6건 출력"이라면서 아래에 건너뜀 3건이 적혀 있는 식이다.
///
/// 화면을 가르기 전 Reports 화면은 출력률을 ReportData에서만 읽었다. 그 값이 그대로여야
/// 하고(화면이 갈라졌다고 숫자가 달라질 이유가 없다), 새로 더한 이유별 건수가 그 값과
/// 앞뒤가 맞아야 한다. 실제 SQLite에 대고 도는 이유는 확인 대상이 두 쿼리의 일치라서다.
/// </summary>
public class CounsellingOutputPanelTests : IDisposable
{
    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ReportRepository _reports;
    private readonly CounsellingLogRepository _logs;

    private int _rowCounter;

    public CounsellingOutputPanelTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-output-panel-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();

        Execute("""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('f1', 'Test Pharmacy', 'KH', 'PP', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('u1', 'f1', 'tester', 'hash', 'Administrator', 'Active', 0);

            INSERT INTO Product_Master
                (product_id, product_name, generic_name, unit, cost_price, selling_price,
                 safety_stock_level, status, created_at)
            VALUES ('p1', 'Amoxil 500mg', 'Amoxicillin', 'tablet', 1.0, 2.0, 10, 'Active', 0);
            """);

        _reports = new ReportRepository(_connectionFactory);
        _logs = new CounsellingLogRepository(_connectionFactory);
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

    private void Execute(string sql)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>기간 안의 하루. 날짜 경계가 현지 자정이라 심는 쪽도 현지 시각으로 준다.</summary>
    private static readonly DateTime Day = new(2026, 8, 10, 9, 0, 0);

    private static ReportRange Range =>
        ReportRange.Create(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

    /// <summary>
    /// 항생제 한 줄을 판다. <paramref name="skipReason"/>이 null이면 안내가 나간 것이다.
    /// </summary>
    private void Sell(string awareGroup, string? skipReason)
    {
        var id = "t" + _rowCounter++;
        var time = new DateTimeOffset(Day).ToUnixTimeMilliseconds();
        var printed = skipReason is null ? 1 : 0;
        var reason = skipReason is null ? "NULL" : $"'{skipReason}'";

        Execute($"""
            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 payment_method, total_amount, reason, transaction_time)
            VALUES
                ('{id}', 'f1', 'p1', 'u1', 'StockOut',
                 'B1', 0, 1, 2.0, 'Cash', 2.0, NULL, {time});

            INSERT INTO Counselling_Log
                (log_id, transaction_id, product_id, atc_code, aware_group,
                 printed, skip_reason, locale, created_at)
            VALUES
                ('log-{id}', '{id}', 'p1', NULL, '{awareGroup}', {printed}, {reason}, '', {time});
            """);
    }

    private Task<CounsellingMetrics> MetricsAsync()
    {
        var range = Range;

        // 화면이 하는 것과 같은 변환. ToUtc는 종료일 자정 직전이고 집계는 `< to`로
        // 자르므로 1을 더해야 그 하루가 온전히 들어온다.
        return _logs.GetMetricsAsync(range.FromUtc, range.ToUtc + 1);
    }

    private async Task<ReportData> ReportAsync()
    {
        var range = Range;
        var (current, previous) = await _reports.GetSalesTotalsAsync("f1", range);

        return new ReportData
        {
            Range = range,
            Current = current,
            Previous = previous,
            Products = await _reports.GetProductSalesAsync("f1", range),
            Antibiotics = await _reports.GetAntibioticSalesAsync("f1", range),
            AntibioticTrend = Array.Empty<AntibioticTrendPoint>(),
            SalesTrend = Array.Empty<SalesTrendPoint>()
        };
    }

    // ── 이유를 가른다 ────────────────────────────────────────────────────────

    /// <summary>
    /// 약사가 건너뛴 것과 인쇄가 실패한 것은 할 일이 전혀 다르다 — 전자는 설명해서
    /// 풀 일이고 후자는 프린터를 고칠 일이다. 합산된 한 숫자로는 어느 쪽인지 알 수 없다.
    /// </summary>
    [Fact]
    public async Task Metrics_SplitPharmacistSkipsFromPrintingFailures()
    {
        Sell(AwareGroupCodes.Access, skipReason: null);
        Sell(AwareGroupCodes.Access, CounsellingService.SkipReasonPharmacist);
        Sell(AwareGroupCodes.Watch, CounsellingService.SkipReasonPharmacist);
        Sell(AwareGroupCodes.Watch, CounsellingService.SkipReasonPrintFailed);

        var metrics = await MetricsAsync();

        Assert.Equal(1, metrics.PrintedCount);
        Assert.Equal(2, metrics.PharmacistSkippedCount);
        Assert.Equal(1, metrics.PrintFailedCount);
        Assert.Equal(3, metrics.SkippedCount);
    }

    /// <summary>
    /// 이유가 적히지 않은 행은 어느 쪽으로도 세지 않는다. 둘 중 하나로 밀어 넣으면
    /// 없는 근거를 만들어 내는 셈이고, 그래서 건너뜀 합계가 두 값의 합보다 클 수 있다 —
    /// 그 차이가 "이유를 모른다"는 사실 자체다.
    /// </summary>
    [Fact]
    public async Task Metrics_CountAReasonlessSkipInTheTotalOnly()
    {
        Sell(AwareGroupCodes.Access, CounsellingService.SkipReasonPharmacist);
        Sell(AwareGroupCodes.Access, skipReason: "");   // 이유가 비어 있는 옛 행

        var metrics = await MetricsAsync();

        Assert.Equal(2, metrics.SkippedCount);
        Assert.Equal(1, metrics.PharmacistSkippedCount);
        Assert.Equal(0, metrics.PrintFailedCount);
    }

    /// <summary>
    /// UNMATCHED는 항생제로 확인된 것이 아니므로 출력률의 분자·분모에서 빠지고,
    /// 따로 센다. 섞이면 시드 보강이 필요한 상품 때문에 출력률이 내려간다.
    /// </summary>
    [Fact]
    public async Task Metrics_KeepUnmatchedOutOfThePrintRate()
    {
        Sell(AwareGroupCodes.Access, skipReason: null);
        Sell(AwareGroupCodes.Unmatched, CounsellingService.SkipReasonUnmatched);
        Sell(AwareGroupCodes.Unmatched, CounsellingService.SkipReasonUnmatched);

        var metrics = await MetricsAsync();

        Assert.Equal(2, metrics.UnmatchedCount);
        Assert.Equal(1, metrics.AntibioticSaleLines);
        Assert.Equal(1, metrics.PrintedCount);
        Assert.Equal(0, metrics.SkippedCount);
        Assert.Equal(1d, metrics.PrintRate);
    }

    // ── 두 출처가 같은 말을 한다 ─────────────────────────────────────────────

    /// <summary>
    /// 패널의 윗줄(리포트 집계)과 아랫줄(로그 집계)이 같은 판매를 세고 있는지.
    ///
    /// 여기가 갈라지면 패널 안에서 숫자가 맞지 않는데, 각 숫자는 자기 쿼리 안에서
    /// 앞뒤가 맞으므로 어느 쪽이 틀렸는지 화면만 보고는 알 수 없다.
    /// </summary>
    [Fact]
    public async Task ThePanelsTwoSourcesCountTheSameSales()
    {
        Sell(AwareGroupCodes.Access, skipReason: null);
        Sell(AwareGroupCodes.Access, skipReason: null);
        Sell(AwareGroupCodes.Watch, CounsellingService.SkipReasonPharmacist);
        Sell(AwareGroupCodes.Reserve, CounsellingService.SkipReasonPrintFailed);
        Sell(AwareGroupCodes.Unmatched, CounsellingService.SkipReasonUnmatched);

        var report = await ReportAsync();
        var metrics = await MetricsAsync();

        // 화면을 가르기 전 요약 카드가 쓰던 값들. 그대로여야 한다.
        Assert.Equal(4, report.AntibioticSaleCount);
        Assert.Equal(2, report.CounsellingPrintedCount);
        Assert.Equal(50m, report.CounsellingPrintRatePercent);

        // 새로 더한 이유별 건수가 그 값과 앞뒤가 맞는다.
        Assert.Equal(report.AntibioticSaleCount, metrics.AntibioticSaleLines);
        Assert.Equal(report.CounsellingPrintedCount, metrics.PrintedCount);
        Assert.Equal(
            report.AntibioticSaleCount,
            metrics.PrintedCount + metrics.SkippedCount);
        Assert.Equal(
            metrics.SkippedCount,
            metrics.PharmacistSkippedCount + metrics.PrintFailedCount);
    }

    /// <summary>
    /// 항생제를 팔지 않은 기간에는 출력률이 "해당 없음"이고, 이유별 건수도 전부 0이다.
    /// 0%로 내보내면 "안내를 하나도 안 줬다"로 읽히는데 실제로는 줄 일이 없었다.
    /// </summary>
    [Fact]
    public async Task AnEmptyPeriodHasNoPrintRateAtAll()
    {
        var report = await ReportAsync();
        var metrics = await MetricsAsync();

        Assert.Null(report.CounsellingPrintRatePercent);
        Assert.Equal(0, metrics.AntibioticSaleLines);
        Assert.Equal(0, metrics.PharmacistSkippedCount);
        Assert.Equal(0, metrics.PrintFailedCount);
        Assert.Equal(0, metrics.UnmatchedCount);
    }

    /// <summary>
    /// 기간 밖의 판매는 들어오지 않는다. 패널이 기간 선택을 따라가지 않으면
    /// 위의 표·그래프와 다른 구간을 말하게 된다.
    /// </summary>
    [Fact]
    public async Task SalesOutsideThePeriodDoNotReachThePanel()
    {
        Sell(AwareGroupCodes.Access, CounsellingService.SkipReasonPrintFailed);

        // 9월 1일 — 고른 기간(8월)의 바로 다음 날.
        var outside = new DateTimeOffset(new DateTime(2026, 9, 1, 9, 0, 0)).ToUnixTimeMilliseconds();
        Execute($"""
            INSERT INTO Counselling_Log
                (log_id, transaction_id, product_id, atc_code, aware_group,
                 printed, skip_reason, locale, created_at)
            VALUES
                ('log-outside', 't0', 'p1', NULL, '{AwareGroupCodes.Access}',
                 0, '{CounsellingService.SkipReasonPrintFailed}', '', {outside});
            """);

        var metrics = await MetricsAsync();

        Assert.Equal(1, metrics.PrintFailedCount);
    }
}
