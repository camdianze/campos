using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;

namespace PharmaPOS.Tests.Inventory;

/// <summary>
/// 재고 이력 화면. 이 화면의 값어치는 입고와 조정이 한 목록에 시간순으로 섞여 나온다는
/// 데 있다 — 종류별로 갈라 놓으면 한 배치의 중간 줄이 빠져서, 앞 줄의 After와
/// 다음 줄의 Before가 어긋나는 지점을 어느 목록에서도 짚을 수 없다.
/// </summary>
public class StockHistoryTests : IDisposable
{
    private const string FacilityId = "fac-1";
    private const string UserId = "user-1";
    private const string ProductId = "prod-1";
    private const string BatchNumber = "B2401";

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly StockHistoryRepository _repository;

    public StockHistoryTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-stock-history-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();
        SeedFacility();

        _repository = new StockHistoryRepository(_connectionFactory);
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
            // 임시 폴더가 남는 것은 테스트 결과에 영향을 주지 않는다.
        }
    }

    private void SeedFacility()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('{FacilityId}', 'F', 'KH', 'D', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('{UserId}', '{FacilityId}', 'pharmacist', 'h', 'Administrator', 'Active', 0);

            INSERT INTO Product_Master
                (product_id, product_name, unit, cost_price, selling_price, safety_stock_level,
                 status, created_at, is_combination, units_per_box)
            VALUES ('{ProductId}', 'Amoxil 500mg Capsule', 'Capsule', 3, 4.53, 5, 'Active', 0, 0, 100);
            """;
        command.ExecuteNonQuery();
    }

    private void InsertTransaction(
        string transactionId, string type, long time, int quantity,
        long? stockBefore, long? stockAfter,
        string? reason = null, string? paymentMethod = null, long expiryDate = 0)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, reason, payment_method,
                 stock_before, stock_after, transaction_time)
            VALUES ($id, $facility, $product, $user, $type, $batch, $expiry, $quantity,
                    $reason, $payment, $before, $after, $time);
            """;
        command.Parameters.AddWithValue("$id", transactionId);
        command.Parameters.AddWithValue("$facility", FacilityId);
        command.Parameters.AddWithValue("$product", ProductId);
        command.Parameters.AddWithValue("$user", UserId);
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$batch", BatchNumber);
        command.Parameters.AddWithValue("$expiry", expiryDate);
        command.Parameters.AddWithValue("$quantity", quantity);
        command.Parameters.AddWithValue("$reason", (object?)reason ?? DBNull.Value);
        command.Parameters.AddWithValue("$payment", (object?)paymentMethod ?? DBNull.Value);
        command.Parameters.AddWithValue("$before", (object?)stockBefore ?? DBNull.Value);
        command.Parameters.AddWithValue("$after", (object?)stockAfter ?? DBNull.Value);
        command.Parameters.AddWithValue("$time", time);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 한 배치에 입고 → 조정 → 입고가 차례로 일어났고, 마지막 입고의 Before가
    /// 앞 조정의 After와 8만큼 어긋난 상태. 실제로 추적하는 상황 그대로다.
    /// </summary>
    private void SeedBrokenChain()
    {
        InsertTransaction("t1", "StockIn", 1_000, 500, 120, 620, expiryDate: 1_800_000_000_000);
        InsertTransaction("t2", "Adjustment", 2_000, -12, 620, 608, reason: "Damaged");
        InsertTransaction("t3", "StockIn", 3_000, 200, 600, 800, expiryDate: 1_800_000_000_000);
    }

    [Fact]
    public async Task All_ReturnsStockInAndAdjustmentInOneTimeline()
    {
        SeedBrokenChain();

        var rows = await _repository.SearchAsync(
            FacilityId, null, null, string.Empty,
            StockHistoryService.TransactionTypesFor(StockHistoryFilter.All));

        // 시간 내림차순이므로 뒤집어서 장부 순서대로 읽는다.
        var chain = rows.Reverse().ToList();

        Assert.Equal(new[] { "Stock In", "Adjustment", "Stock In" }, chain.Select(r => r.TypeText));

        // 끊긴 지점: 두 번째 줄의 After(608)와 세 번째 줄의 Before(600)가 다르다.
        Assert.Equal(chain[0].StockAfter, chain[1].StockBefore);
        Assert.NotEqual(chain[1].StockAfter, chain[2].StockBefore);
    }

    /// <summary>
    /// 갈라 놓으면 왜 못 찾는지를 못박아 둔다. 입고만 보면 620→600 사이의
    /// 조정 줄이 통째로 빠져서, 남은 두 줄만으로는 어긋남을 판단할 수 없다.
    /// </summary>
    [Fact]
    public async Task StockInOnly_HidesTheAdjustmentThatBreaksTheChain()
    {
        SeedBrokenChain();

        var rows = await _repository.SearchAsync(
            FacilityId, null, null, string.Empty,
            StockHistoryService.TransactionTypesFor(StockHistoryFilter.StockIn));

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("Stock In", r.TypeText));
        Assert.DoesNotContain(rows, r => r.TypeText == "Adjustment");
    }

    [Fact]
    public async Task SaleFilter_IncludesRefunds()
    {
        InsertTransaction("t1", "StockOut", 1_000, 10, 100, 90, paymentMethod: "Cash");
        InsertTransaction("t2", "Refund", 2_000, -4, 90, 94, reason: "Wrong item");
        InsertTransaction("t3", "Adjustment", 3_000, -1, 94, 93, reason: "Damaged");

        var rows = await _repository.SearchAsync(
            FacilityId, null, null, string.Empty,
            StockHistoryService.TransactionTypesFor(StockHistoryFilter.Sale));

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.TypeText == "Sale");
        Assert.Contains(rows, r => r.TypeText == "Refund");
        Assert.DoesNotContain(rows, r => r.TypeText == "Adjustment");
    }

    [Fact]
    public async Task MissingStockColumns_StayNullRatherThanZero()
    {
        // 재고 추적이 생기기 전에 쌓인 거래. 0으로 읽으면 "그때 재고가 0이었다"가 된다.
        InsertTransaction("t1", "Adjustment", 1_000, -3, null, null, reason: "Old record");

        var rows = await _repository.SearchAsync(
            FacilityId, null, null, string.Empty,
            StockHistoryService.TransactionTypesFor(StockHistoryFilter.All));

        Assert.Null(rows[0].StockBefore);
        Assert.Null(rows[0].StockAfter);
    }

    [Fact]
    public async Task SearchTerm_MatchesBatchNumber()
    {
        SeedBrokenChain();

        var found = await _repository.SearchAsync(
            FacilityId, null, null, BatchNumber,
            StockHistoryService.TransactionTypesFor(StockHistoryFilter.All));
        var missing = await _repository.SearchAsync(
            FacilityId, null, null, "NOPE",
            StockHistoryService.TransactionTypesFor(StockHistoryFilter.All));

        Assert.Equal(3, found.Count);
        Assert.Empty(missing);
    }

    /// <summary>
    /// 날짜 범위는 Unix epoch 밀리초로 비교해야 한다. 이 값에 SQLite의 DATE()를 쓰면
    /// 율리우스일로 해석돼서 어떤 범위를 넣어도 걸러지지 않는다.
    /// </summary>
    [Fact]
    public async Task DateRange_FiltersOnEpochMilliseconds()
    {
        var day1 = new DateTimeOffset(new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var day2 = new DateTimeOffset(new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        InsertTransaction("t1", "StockIn", day1, 50, 0, 50);
        InsertTransaction("t2", "StockIn", day2, 50, 50, 100);

        var rows = await _repository.SearchAsync(
            FacilityId, day1 - 1, day1 + 1, string.Empty,
            StockHistoryService.TransactionTypesFor(StockHistoryFilter.All));

        Assert.Single(rows);
        Assert.Equal("t1", rows[0].TransactionId);
    }

    /// <summary>
    /// 종류마다 붙는 값이 달라서 Detail 한 칸에 모은다. 무엇이 들어가는지 고정해 둔다.
    /// </summary>
    [Theory]
    [InlineData("StockIn", null, null, "Exp —")]
    [InlineData("Adjustment", "Damaged", null, "Damaged")]
    [InlineData("StockOut", null, "Cash", "Cash")]
    [InlineData("Refund", "Wrong item", "Cash", "Wrong item")]
    public void Detail_CarriesWhateverThatTypeHas(
        string type, string? reason, string? paymentMethod, string expected)
    {
        var line = new StockHistoryLineItem
        {
            TransactionId = "t1",
            ProductId = ProductId,
            ProductName = "Amoxil 500mg Capsule",
            BatchNumber = BatchNumber,
            ExpiryDate = 0,
            Quantity = 1,
            TransactionType = type,
            Reason = reason,
            PaymentMethod = paymentMethod,
            Username = "pharmacist",
            TransactionTime = 0
        };

        Assert.Equal(expected, line.Detail);
    }

    /// <summary>expiry_date 0은 1970년이 아니라 "유효기간 모름"이다.</summary>
    [Fact]
    public void Detail_ShowsUnknownExpiryRatherThan1970()
    {
        var line = new StockHistoryLineItem
        {
            TransactionId = "t1",
            ProductId = ProductId,
            ProductName = "Amoxil 500mg Capsule",
            BatchNumber = BatchNumber,
            ExpiryDate = 0,
            Quantity = 1,
            TransactionType = "StockIn",
            Username = "pharmacist",
            TransactionTime = 0
        };

        Assert.DoesNotContain("1970", line.Detail);
    }
}
