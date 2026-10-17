using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Reports;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;

namespace PharmaPOS.Tests.Reports;

/// <summary>
/// 매출 달력이 읽는 일별 집계. 실제 SQLite에 대고 돈다.
///
/// 이 집계가 지켜야 할 것은 하나다: <b>달력 칸을 다 더한 값이 그 달을 기간으로 고른
/// 요약 카드의 Sales Amount와 같아야 한다.</b> 달력은 그 카드 바로 아래에 놓이므로,
/// 어긋나면 같은 화면의 두 숫자 중 어느 쪽이 맞는지 알 수 없게 된다. 그래서
/// 환불 상계·건수 기준·날짜 경계가 월별 추이와 글자 그대로 같아야 한다.
/// </summary>
public class DailySalesTests : IDisposable
{
    private static readonly DateTime Month = new(2026, 8, 1);

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ReportRepository _repository;

    private int _rowCounter;

    public DailySalesTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-daily-sales-tests", Guid.NewGuid().ToString());
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
    /// 판매 한 줄. 시각은 현지 날짜로 준다 — 이 앱의 날짜 경계가 현지 자정이라,
    /// 집계가 같은 기준으로 묶이는지 보려면 심는 쪽도 같아야 한다.
    /// </summary>
    private string Sell(DateTime localSoldAt, decimal amount, string userId = "u1")
    {
        var id = "t" + _rowCounter++;
        var time = new DateTimeOffset(localSoldAt).ToUnixTimeMilliseconds();

        Execute($"""
            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 payment_method, total_amount, reason, transaction_time)
            VALUES
                ('{id}', 'f1', 'p1', '{userId}', 'StockOut',
                 'B1', 0, 1, {amount}, 'Cash', {amount}, NULL, {time});
            """);

        return id;
    }

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

    private async Task<IReadOnlyList<DailySalesPoint>> DaysAsync() =>
        await _repository.GetDailySalesAsync("f1", Month);

    private async Task<DailySalesPoint> DayAsync(int day) =>
        (await DaysAsync()).Single(p => p.Date.Day == day);

    [Fact]
    public async Task Days_CoverTheWholeMonth()
    {
        var days = await DaysAsync();

        Assert.Equal(31, days.Count);
        Assert.Equal(new DateTime(2026, 8, 1), days[0].Date);
        Assert.Equal(new DateTime(2026, 8, 31), days[^1].Date);
    }

    /// <summary>판매가 없던 날도 칸은 있어야 한다. 빠지면 달력에 구멍이 난다.</summary>
    [Fact]
    public async Task Days_WithoutSales_AreZeroRatherThanMissing()
    {
        Sell(new DateTime(2026, 8, 10, 9, 0, 0), 42.50m);

        var days = await DaysAsync();

        Assert.Equal(42.50m, (await DayAsync(10)).Amount);
        Assert.All(days.Where(d => d.Date.Day != 10), d =>
        {
            Assert.Equal(0m, d.Amount);
            Assert.False(d.HasSales);
        });
    }

    /// <summary>같은 날의 여러 판매는 그 칸에 합쳐진다.</summary>
    [Fact]
    public async Task Day_SumsEverySaleOnThatDate()
    {
        Sell(new DateTime(2026, 8, 10, 9, 0, 0), 10m);
        Sell(new DateTime(2026, 8, 10, 18, 30, 0), 15m);

        var day = await DayAsync(10);

        Assert.Equal(25m, day.Amount);
        Assert.Equal(2, day.TransactionCount);
    }

    /// <summary>
    /// 환불은 음수로 쌓여 있어 함께 더하면 상계된다. 판매 행만 더하면 총매출이 되어
    /// 요약 카드와 달력의 합이 어긋난다.
    /// </summary>
    [Fact]
    public async Task Day_NetsRefundsOffAgainstSales()
    {
        var sale = Sell(new DateTime(2026, 8, 10, 9, 0, 0), 100m);
        Refund(new DateTime(2026, 8, 10, 15, 0, 0), 30m, sale);

        Assert.Equal(70m, (await DayAsync(10)).Amount);
    }

    /// <summary>환불이 다음 날 이뤄졌으면 그 날 칸에서 빠진다. 판매한 날로 돌아가지 않는다.</summary>
    [Fact]
    public async Task Refund_LandsOnTheDayItWasIssued()
    {
        var sale = Sell(new DateTime(2026, 8, 10, 9, 0, 0), 100m);
        Refund(new DateTime(2026, 8, 12, 9, 0, 0), 30m, sale);

        Assert.Equal(100m, (await DayAsync(10)).Amount);
        Assert.Equal(-30m, (await DayAsync(12)).Amount);
    }

    /// <summary>환불은 또 한 번의 판매가 아니다. 건수는 판매 행만 센다.</summary>
    [Fact]
    public async Task Day_DoesNotCountRefundsAsTransactions()
    {
        var sale = Sell(new DateTime(2026, 8, 12, 9, 0, 0), 100m);
        Refund(new DateTime(2026, 8, 12, 15, 0, 0), 30m, sale);

        Assert.Equal(1, (await DayAsync(12)).TransactionCount);
    }

    /// <summary>
    /// 한 장바구니에 여러 줄이 담기면 행은 여럿이지만 거래는 하나다.
    /// 판매 내역 화면과 같은 기준("판매 시각 + 판매자")으로 센다.
    /// </summary>
    [Fact]
    public async Task Day_CountsOneCartAsOneTransaction()
    {
        var soldAt = new DateTime(2026, 8, 10, 9, 0, 0);
        Sell(soldAt, 10m);
        Sell(soldAt, 15m);

        var day = await DayAsync(10);

        Assert.Equal(25m, day.Amount);
        Assert.Equal(1, day.TransactionCount);
    }

    /// <summary>같은 시각이라도 판매자가 다르면 다른 거래다.</summary>
    [Fact]
    public async Task Day_CountsSameInstantByDifferentUsersSeparately()
    {
        Execute("""
            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('u2', 'f1', 'tester2', 'hash', 'Pharmacist', 'Active', 0);
            """);

        var soldAt = new DateTime(2026, 8, 10, 9, 0, 0);
        Sell(soldAt, 10m);
        Sell(soldAt, 15m, userId: "u2");

        Assert.Equal(2, (await DayAsync(10)).TransactionCount);
    }

    /// <summary>
    /// 자정 직전·직후의 판매가 제 날짜 칸에 들어가야 한다. UTC로 묶으면 시간대에 따라
    /// 옆 날짜로 넘어간다 — 이 앱의 날짜 경계는 전부 현지 자정이다.
    /// </summary>
    [Fact]
    public async Task Days_UseLocalMidnightAsTheBoundary()
    {
        Sell(new DateTime(2026, 8, 10, 23, 59, 30), 10m);
        Sell(new DateTime(2026, 8, 11, 0, 0, 30), 20m);

        Assert.Equal(10m, (await DayAsync(10)).Amount);
        Assert.Equal(20m, (await DayAsync(11)).Amount);
    }

    /// <summary>옆 달의 판매가 딸려 들어오면 안 된다. 말일 끝과 1일 시작이 경계다.</summary>
    [Fact]
    public async Task Days_ExcludeTheNeighbouringMonths()
    {
        Sell(new DateTime(2026, 7, 31, 23, 59, 30), 10m);
        Sell(new DateTime(2026, 9, 1, 0, 0, 30), 20m);
        Sell(new DateTime(2026, 8, 1, 0, 0, 30), 5m);
        Sell(new DateTime(2026, 8, 31, 23, 59, 30), 7m);

        var days = await DaysAsync();

        Assert.Equal(5m, days[0].Amount);
        Assert.Equal(7m, days[^1].Amount);
        Assert.Equal(12m, days.Sum(d => d.Amount));
    }

    /// <summary>
    /// 이 테스트가 이 파일의 핵심이다. 달력 칸의 합과, 그 달을 기간으로 고른
    /// 요약 카드의 매출액이 같아야 한다. 규칙이 갈리면 여기서 깨진다.
    /// </summary>
    [Fact]
    public async Task DayTotals_AgreeWithTheMonthsSalesAmountCard()
    {
        var sale = Sell(new DateTime(2026, 8, 3, 9, 0, 0), 120m);
        Sell(new DateTime(2026, 8, 3, 11, 0, 0), 30m);
        Refund(new DateTime(2026, 8, 20, 14, 0, 0), 45m, sale);
        Sell(new DateTime(2026, 8, 31, 20, 0, 0), 60m);

        var days = await DaysAsync();

        var range = ReportRange.Create(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));
        var (current, _) = await _repository.GetSalesTotalsAsync("f1", range);

        Assert.Equal(current.Amount, days.Sum(d => d.Amount));
        Assert.Equal(current.TransactionCount, days.Sum(d => d.TransactionCount));
    }

    /// <summary>
    /// 같은 달이면 월별 추이의 그 달 값과도 같아야 한다. 달력과 12개월 그래프는
    /// 한 자리에서 토글로 바뀌므로, 둘이 다르면 버튼 하나에 총액이 달라진다.
    /// </summary>
    [Fact]
    public async Task DayTotals_AgreeWithTheMonthlyTrendBar()
    {
        var sale = Sell(new DateTime(2026, 8, 3, 9, 0, 0), 120m);
        Refund(new DateTime(2026, 8, 20, 14, 0, 0), 45m, sale);

        var days = await DaysAsync();
        var trend = await _repository.GetSalesTrendAsync("f1", Month, 12);
        var august = trend.Single(p => p.Month == Month);

        Assert.Equal(august.Amount, days.Sum(d => d.Amount));
        Assert.Equal(august.TransactionCount, days.Sum(d => d.TransactionCount));
    }

    /// <summary>다른 약국의 판매는 들어오지 않는다.</summary>
    [Fact]
    public async Task Days_AreScopedToTheFacility()
    {
        var otherTime = new DateTimeOffset(new DateTime(2026, 8, 10, 9, 0, 0)).ToUnixTimeMilliseconds();

        Execute($"""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('f2', 'Other Pharmacy', 'KH', 'PP', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('u9', 'f2', 'other', 'hash', 'Administrator', 'Active', 0);

            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, selling_price_at_transaction,
                 payment_method, total_amount, reason, transaction_time)
            VALUES
                ('other-1', 'f2', 'p1', 'u9', 'StockOut', 'B1', 0, 1, 99, 'Cash', 99, NULL, {otherTime});
            """);

        Sell(new DateTime(2026, 8, 10, 9, 0, 0), 10m);

        Assert.Equal(10m, (await DayAsync(10)).Amount);
    }

    /// <summary>칸에 적는 글자. 좁은 칸이라 반올림하고, 판매가 없는 날은 비워 둔다.</summary>
    [Fact]
    public async Task AmountLabel_IsRoundedAndBlankWhenThereWereNoSales()
    {
        Sell(new DateTime(2026, 8, 10, 9, 0, 0), 1234.56m);

        Assert.Equal("1,235", (await DayAsync(10)).AmountLabel);
        Assert.Equal(string.Empty, (await DayAsync(11)).AmountLabel);
    }
}
