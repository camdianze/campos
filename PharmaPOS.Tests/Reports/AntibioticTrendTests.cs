using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Reports;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Reports;

/// <summary>
/// 항생제 판매 추이 집계. 실제 SQLite에 대고 돈다 — 달 구분과 빈 달 채우기는
/// 날짜 계산과 SQL이 함께 만드는 결과라 가짜 저장소로는 확인할 수 없다.
/// </summary>
public class AntibioticTrendTests : IDisposable
{
    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ReportRepository _repository;

    private int _rowCounter;

    public AntibioticTrendTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-trend-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();

        Execute("""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('f1', 'Test Pharmacy', 'KH', 'PP', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('u1', 'f1', 'tester', 'hash', 'Administrator', 'Active', 0);

            INSERT INTO Product_Master
                (product_id, product_name, unit, cost_price, selling_price,
                 safety_stock_level, status, created_at)
            VALUES ('p1', 'Amoxicillin 500mg', 'tablet', 1.0, 2.0, 10, 'Active', 0);
            """);

        _repository = new ReportRepository(_connectionFactory);
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

    /// <summary>
    /// 항생제 한 줄을 판다. 시각은 현지 날짜로 준다 — 이 앱의 날짜 경계가 현지 자정
    /// 기준이라, 집계가 같은 기준으로 묶이는지 확인하려면 심는 쪽도 같아야 한다.
    /// </summary>
    private void SellAntibiotic(DateTime localSoldAt, string awareGroup, int quantity)
    {
        var id = "t" + _rowCounter++;
        var time = new DateTimeOffset(localSoldAt).ToUnixTimeMilliseconds();

        Execute($"""
            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 payment_method, total_amount, reason, transaction_time)
            VALUES
                ('{id}', 'f1', 'p1', 'u1', 'StockOut',
                 'B1', 0, {quantity}, 2.0, 'Cash', {quantity * 2.0}, NULL, {time});

            INSERT INTO Counselling_Log
                (log_id, transaction_id, product_id, atc_code, aware_group,
                 printed, skip_reason, locale, created_at)
            VALUES
                ('log-{id}', '{id}', 'p1', NULL, '{awareGroup}', 1, NULL, '', {time});
            """);
    }

    private static readonly DateTime EndMonth = new(2026, 8, 1);

    private async Task<IReadOnlyList<AntibioticTrendPoint>> TrendAsync(int months = 12) =>
        await _repository.GetAntibioticTrendAsync("f1", EndMonth, months);

    [Fact]
    public async Task Trend_ReturnsExactlyTheRequestedNumberOfMonths()
    {
        var trend = await TrendAsync();

        Assert.Equal(12, trend.Count);
    }

    /// <summary>
    /// 마지막 칸이 기간이 끝나는 달이고, 첫 칸이 그로부터 11개월 전이다.
    /// 창이 밀리면 "이번 달"이 그래프 가운데 놓여 추이를 잘못 읽게 된다.
    /// </summary>
    [Fact]
    public async Task Trend_EndsAtTheGivenMonthAndSpansBackwards()
    {
        var trend = await TrendAsync();

        Assert.Equal(new DateTime(2025, 9, 1), trend[0].Month);
        Assert.Equal(new DateTime(2026, 8, 1), trend[^1].Month);
    }

    /// <summary>
    /// 판매가 없던 달도 0으로 남아야 한다. 빼 버리면 가로 간격이 달마다 달라져서
    /// 비어 있는 달과 이어지는 달을 눈으로 구분할 수 없다.
    /// </summary>
    [Fact]
    public async Task Trend_FillsMonthsWithoutSalesWithZero()
    {
        SellAntibiotic(new DateTime(2026, 8, 10, 9, 0, 0), AwareGroupCodes.Access, 5);

        var trend = await TrendAsync();

        Assert.Equal(5, trend[^1].TotalQuantity);
        Assert.All(trend.Take(11), point => Assert.Equal(0, point.TotalQuantity));
    }

    [Fact]
    public async Task Trend_SplitsQuantityByAwareGroup()
    {
        SellAntibiotic(new DateTime(2026, 8, 5, 9, 0, 0), AwareGroupCodes.Access, 200);
        SellAntibiotic(new DateTime(2026, 8, 6, 9, 0, 0), AwareGroupCodes.Watch, 3);
        SellAntibiotic(new DateTime(2026, 8, 7, 9, 0, 0), AwareGroupCodes.Reserve, 1);
        SellAntibiotic(new DateTime(2026, 8, 8, 9, 0, 0), AwareGroupCodes.NotRecommended, 2);

        var august = (await TrendAsync())[^1];

        Assert.Equal(200, august.AccessQuantity);
        Assert.Equal(3, august.WatchQuantity);
        Assert.Equal(1, august.ReserveQuantity);
        Assert.Equal(2, august.NotRecommendedQuantity);
        Assert.Equal(206, august.TotalQuantity);
    }

    [Fact]
    public async Task Trend_KeepsEachMonthSeparate()
    {
        SellAntibiotic(new DateTime(2026, 6, 30, 23, 30, 0), AwareGroupCodes.Access, 7);
        SellAntibiotic(new DateTime(2026, 7, 1, 0, 30, 0), AwareGroupCodes.Access, 9);

        var trend = await TrendAsync();

        // 2025-09가 0번이므로 2026-06은 9번, 2026-07은 10번이다.
        Assert.Equal(7, trend[9].TotalQuantity);
        Assert.Equal(9, trend[10].TotalQuantity);
    }

    /// <summary>창 밖의 판매는 어느 칸에도 들어오지 않는다.</summary>
    [Fact]
    public async Task Trend_IgnoresSalesOutsideTheWindow()
    {
        SellAntibiotic(new DateTime(2025, 8, 31, 12, 0, 0), AwareGroupCodes.Access, 50);
        SellAntibiotic(new DateTime(2026, 9, 1, 12, 0, 0), AwareGroupCodes.Access, 60);

        var trend = await TrendAsync();

        Assert.All(trend, point => Assert.Equal(0, point.TotalQuantity));
    }

    /// <summary>
    /// UNMATCHED는 항생제로 판정된 것이 아니라 판정하지 못한 것이다.
    /// 표와 ACCESS 비중이 이미 제외하고 있으므로 그래프도 같아야 한다 —
    /// 규칙이 갈라지면 표의 합과 그래프의 합이 달라진다.
    /// </summary>
    [Fact]
    public async Task Trend_ExcludesUnmatchedJustLikeTheTable()
    {
        SellAntibiotic(new DateTime(2026, 8, 5, 9, 0, 0), AwareGroupCodes.Unmatched, 500);
        SellAntibiotic(new DateTime(2026, 8, 5, 9, 0, 0), AwareGroupCodes.Access, 4);

        var august = (await TrendAsync())[^1];

        Assert.Equal(4, august.TotalQuantity);
    }

    /// <summary>
    /// 환불 행(음수)은 빼지 않는다. 이 그래프가 세는 것은 매출이 아니라
    /// "항생제가 손님 손에 몇 번 나갔는가"이고, 돈을 돌려줬다고 해서 이미 나간
    /// 항생제가 없던 일이 되지는 않는다 — 표와 같은 규칙이다.
    /// </summary>
    [Fact]
    public async Task Trend_CountsGrossJustLikeTheTable()
    {
        SellAntibiotic(new DateTime(2026, 8, 5, 9, 0, 0), AwareGroupCodes.Access, 10);

        Execute("""
            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 payment_method, total_amount, reason, transaction_time,
                 related_transaction_id)
            VALUES
                ('refund-1', 'f1', 'p1', 'u1', 'Refund',
                 'B1', 0, -10, 2.0, 'Cash', -20.0, 'returned', 1786000000000, 't0');
            """);

        var august = (await TrendAsync())[^1];

        Assert.Equal(10, august.TotalQuantity);
    }
}
