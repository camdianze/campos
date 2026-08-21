using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Reports;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;

namespace PharmaPOS.Tests.Reports;

/// <summary>
/// 월별 순매출 집계. 실제 SQLite에 대고 돈다 — 환불 상계와 달 구분은
/// 날짜 계산과 SQL이 함께 만드는 결과라 가짜 저장소로는 확인할 수 없다.
/// </summary>
public class SalesTrendTests : IDisposable
{
    private static readonly DateTime EndMonth = new(2026, 8, 1);

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ReportRepository _repository;

    private int _rowCounter;

    public SalesTrendTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-sales-trend-tests", Guid.NewGuid().ToString());
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
            VALUES ('p1', 'Paracetamol 500mg', 'tablet', 1.0, 2.0, 10, 'Active', 0);
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
    /// 판매 한 줄. 시각은 현지 날짜로 준다 — 이 앱의 날짜 경계가 현지 자정 기준이라,
    /// 집계가 같은 기준으로 묶이는지 확인하려면 심는 쪽도 같아야 한다.
    /// </summary>
    private string Sell(DateTime localSoldAt, decimal amount)
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
                 'B1', 0, 1, {amount}, 'Cash', {amount}, NULL, {time});
            """);

        return id;
    }

    /// <summary>환불 한 줄. 수량과 금액이 음수로 쌓인다.</summary>
    private void Refund(DateTime localRefundedAt, decimal amount, string originalId)
    {
        var id = "r" + _rowCounter++;
        var time = new DateTimeOffset(localRefundedAt).ToUnixTimeMilliseconds();

        Execute($"""
            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 payment_method, total_amount, reason, transaction_time,
                 related_transaction_id)
            VALUES
                ('{id}', 'f1', 'p1', 'u1', 'Refund',
                 'B1', 0, -1, {amount}, 'Cash', {-amount}, 'returned', {time}, '{originalId}');
            """);
    }

    private async Task<IReadOnlyList<SalesTrendPoint>> TrendAsync() =>
        await _repository.GetSalesTrendAsync("f1", EndMonth, 12);

    [Fact]
    public async Task Trend_SpansTwelveMonthsEndingAtTheGivenMonth()
    {
        var trend = await TrendAsync();

        Assert.Equal(12, trend.Count);
        Assert.Equal(new DateTime(2025, 9, 1), trend[0].Month);
        Assert.Equal(new DateTime(2026, 8, 1), trend[^1].Month);
    }

    /// <summary>
    /// 항생제 추이와 같은 창이어야 한다. 두 그래프가 나란히 놓이므로
    /// 가로축이 한 칸이라도 어긋나면 비교가 되지 않는다.
    /// </summary>
    [Fact]
    public async Task Trend_UsesTheSameWindowAsTheAntibioticChart()
    {
        var sales = await TrendAsync();
        var antibiotics = await _repository.GetAntibioticTrendAsync("f1", EndMonth, 12);

        Assert.Equal(
            antibiotics.Select(p => p.Month),
            sales.Select(p => p.Month));
    }

    [Fact]
    public async Task Trend_FillsMonthsWithoutSalesWithZero()
    {
        Sell(new DateTime(2026, 8, 10, 9, 0, 0), 42.50m);

        var trend = await TrendAsync();

        Assert.Equal(42.50m, trend[^1].Amount);
        Assert.All(trend.Take(11), point => Assert.Equal(0m, point.Amount));
    }

    /// <summary>
    /// 환불은 음수로 쌓여 있어 함께 더하면 저절로 상계된다. 판매 행만 더하면
    /// 총매출이 되어 위 요약 카드의 Sales Amount와 어긋난다.
    /// </summary>
    [Fact]
    public async Task Trend_NetsRefundsOffAgainstSales()
    {
        var sale = Sell(new DateTime(2026, 8, 10, 9, 0, 0), 100m);
        Refund(new DateTime(2026, 8, 12, 9, 0, 0), 30m, sale);

        Assert.Equal(70m, (await TrendAsync())[^1].Amount);
    }

    /// <summary>환불은 또 한 번의 판매가 아니다. 건수는 판매 행만 센다.</summary>
    [Fact]
    public async Task Trend_DoesNotCountRefundsAsTransactions()
    {
        var sale = Sell(new DateTime(2026, 8, 10, 9, 0, 0), 100m);
        Refund(new DateTime(2026, 8, 12, 9, 0, 0), 30m, sale);

        Assert.Equal(1, (await TrendAsync())[^1].TransactionCount);
    }

    /// <summary>
    /// 한 장바구니에 여러 줄이 담기면 Stock_Transaction은 여러 행이지만 거래는 하나다.
    /// 판매 내역 화면과 같은 기준("판매 시각 + 판매자")으로 센다.
    /// </summary>
    [Fact]
    public async Task Trend_CountsOneCartAsOneTransaction()
    {
        var soldAt = new DateTime(2026, 8, 10, 9, 0, 0);

        Sell(soldAt, 10m);
        Sell(soldAt, 20m);
        Sell(soldAt, 30m);

        var august = (await TrendAsync())[^1];

        Assert.Equal(60m, august.Amount);
        Assert.Equal(1, august.TransactionCount);
    }

    [Fact]
    public async Task Trend_KeepsEachMonthSeparate()
    {
        Sell(new DateTime(2026, 6, 30, 23, 30, 0), 7m);
        Sell(new DateTime(2026, 7, 1, 0, 30, 0), 9m);

        var trend = await TrendAsync();

        // 2025-09가 0번이므로 2026-06은 9번, 2026-07은 10번이다.
        Assert.Equal(7m, trend[9].Amount);
        Assert.Equal(9m, trend[10].Amount);
    }

    [Fact]
    public async Task Trend_IgnoresSalesOutsideTheWindow()
    {
        Sell(new DateTime(2025, 8, 31, 12, 0, 0), 50m);
        Sell(new DateTime(2026, 9, 1, 12, 0, 0), 60m);

        Assert.All(await TrendAsync(), point => Assert.Equal(0m, point.Amount));
    }
}
