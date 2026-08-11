using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Inventory;

/// <summary>
/// 환불의 통합 테스트. 실제 SQLite 파일에 판매를 하나 만들고 그것을 환불한다.
///
/// 여기만 테스트를 두는 이유: 환불은 돈과 재고를 동시에 되돌리는 유일한 흐름이고,
/// 틀려도 화면에는 아무 표시가 나지 않는다 — 초과 환불이 통과하거나 재고가 두 번
/// 늘어나도 다음 실사 전까지 아무도 모른다.
/// </summary>
public class RefundTests : IDisposable
{
    private const string FacilityId = "facility-1";
    private const string UserId = "user-1";
    private const string ProductId = "product-1";
    private const string BatchNumber = "B-001";

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SaleService _saleService;
    private readonly RefundService _refundService;

    public RefundTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-refund-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();

        SeedFacilityUserAndProduct();

        _saleService = new SaleService(new SaleRepository(_connectionFactory));
        _refundService = new RefundService(new RefundRepository(_connectionFactory));
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

    // ── 시나리오 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refund_RestoresStockAndRecordsNegativeLedgerRow()
    {
        var sale = await SellAsync(quantity: 3, unitPrice: 1000m);

        var result = await RefundAsync(sale, quantity: 2, returnToStock: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(2000m, result.RefundedAmount);

        // 10개 들여 3개 팔았고 2개가 돌아왔다.
        Assert.Equal(9, GetCurrentQuantity());

        var (quantity, totalAmount) = GetRefundRow(sale.TransactionId);
        Assert.Equal(-2, quantity);
        Assert.Equal(-2000m, totalAmount);
    }

    [Fact]
    public async Task Refund_WithoutReturnToStock_LeavesStockUntouched()
    {
        var sale = await SellAsync(quantity: 3, unitPrice: 1000m);

        var result = await RefundAsync(sale, quantity: 3, returnToStock: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, GetCurrentQuantity());

        // 재고가 왜 안 늘었는지는 사유에만 남는다.
        Assert.Contains("not returned to stock", GetRefundReason(sale.TransactionId));
    }

    [Fact]
    public async Task Refund_CannotExceedRemainingQuantity()
    {
        var sale = await SellAsync(quantity: 3, unitPrice: 1000m);

        Assert.True((await RefundAsync(sale, quantity: 2, returnToStock: true)).IsSuccess);

        var second = await RefundAsync(sale, quantity: 2, returnToStock: true);

        Assert.False(second.IsSuccess);
        Assert.Equal(9, GetCurrentQuantity());
    }

    [Fact]
    public async Task Refund_WithoutMemo_Succeeds()
    {
        var sale = await SellAsync(quantity: 1, unitPrice: 1000m);

        var result = await _refundService.RefundAsync(
            FacilityId, UserId, sale.TransactionTime, UserId,
            new[] { new RefundLineRequest { TransactionId = sale.TransactionId, Quantity = 1 } },
            reason: "   ",
            returnToStock: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, GetCurrentQuantity());
    }

    /// <summary>
    /// 같은 판매 줄이 한 요청에 두 번 실려 오면, 하나씩은 한도 안이어도 합치면 넘는다.
    /// </summary>
    [Fact]
    public async Task Refund_RejectsDuplicateLinesThatExceedTheSaleTogether()
    {
        var sale = await SellAsync(quantity: 3, unitPrice: 1000m);

        var result = await _refundService.RefundAsync(
            FacilityId, UserId, sale.TransactionTime, UserId,
            new[]
            {
                new RefundLineRequest { TransactionId = sale.TransactionId, Quantity = 2 },
                new RefundLineRequest { TransactionId = sale.TransactionId, Quantity = 2 }
            },
            reason: "Duplicate line",
            returnToStock: true);

        Assert.False(result.IsSuccess);
        Assert.Equal(7, GetCurrentQuantity());
    }

    [Fact]
    public async Task GetRefundableLines_ReportsWhatIsLeft()
    {
        var sale = await SellAsync(quantity: 5, unitPrice: 1000m);

        await RefundAsync(sale, quantity: 2, returnToStock: true);

        var lines = await _refundService.GetRefundableLinesAsync(FacilityId, sale.TransactionTime, UserId);

        var line = Assert.Single(lines);
        Assert.Equal(5, line.SoldQuantity);
        Assert.Equal(2, line.RefundedQuantity);
        Assert.Equal(3, line.RemainingQuantity);
    }

    [Fact]
    public async Task Refund_RecreatesBatchThatWasDeletedAfterSellingOut()
    {
        var sale = await SellAsync(quantity: 10, unitPrice: 1000m);

        // 다 팔린 배치를 화면에서 지운 상황.
        ExecuteSql("DELETE FROM Inventory WHERE current_quantity = 0;");

        var result = await RefundAsync(sale, quantity: 4, returnToStock: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, GetCurrentQuantity());
    }

    // ── 도우미 ──────────────────────────────────────────────────────────────

    /// <summary>재고 10개를 깔고 quantity개를 판다. 판매된 줄 하나를 돌려준다.</summary>
    private async Task<(string TransactionId, long TransactionTime)> SellAsync(
        int quantity, decimal unitPrice)
    {
        var inventoryId = Guid.NewGuid().ToString();

        ExecuteSql($"""
            INSERT INTO Inventory
                (inventory_id, facility_id, product_id, batch_number, expiry_date,
                 current_quantity, box_quantity, unit_quantity, updated_at)
            VALUES
                ('{inventoryId}', '{FacilityId}', '{ProductId}', '{BatchNumber}', 0,
                 10, 0, 10, 0);
            """);

        var cart = new List<SaleLineItem>
        {
            new()
            {
                ProductId = ProductId,
                ProductName = "Test Product",
                InventoryId = inventoryId,
                BatchNumber = BatchNumber,
                ExpiryDate = 0,
                Quantity = quantity,
                UnitPrice = unitPrice,
                CostPrice = 0m
            }
        };

        var result = await _saleService.ConfirmSaleAsync(
            FacilityId, UserId, cart, PaymentMethod.Cash, cashTendered: 100000m, notes: null);

        Assert.True(result.IsSuccess);

        var line = result.ConfirmedLines.Single();
        return (line.TransactionId, GetTransactionTime(line.TransactionId));
    }

    private Task<RefundResult> RefundAsync(
        (string TransactionId, long TransactionTime) sale, int quantity, bool returnToStock)
    {
        return _refundService.RefundAsync(
            FacilityId, UserId, sale.TransactionTime, UserId,
            new[] { new RefundLineRequest { TransactionId = sale.TransactionId, Quantity = quantity } },
            reason: "Customer returned the item",
            returnToStock: returnToStock);
    }

    private void SeedFacilityUserAndProduct()
    {
        ExecuteSql($"""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('{FacilityId}', 'Test Pharmacy', 'KR', 'Test', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('{UserId}', '{FacilityId}', 'tester', 'hash', 'FacilityStaff', 'Active', 0);

            INSERT INTO Product_Master
                (product_id, product_name, unit, cost_price, selling_price,
                 safety_stock_level, status, created_at, units_per_box)
            VALUES
                ('{ProductId}', 'Test Product', 'Tablet', 500, 1000, 5, 'Active', 0, 1);
            """);
    }

    private void ExecuteSql(string sql)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private T QueryScalar<T>(string sql)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }

    private int GetCurrentQuantity() => QueryScalar<int>($"""
        SELECT COALESCE(SUM(current_quantity), 0) FROM Inventory
        WHERE facility_id = '{FacilityId}' AND product_id = '{ProductId}';
        """);

    private long GetTransactionTime(string transactionId) => QueryScalar<long>($"""
        SELECT transaction_time FROM Stock_Transaction WHERE transaction_id = '{transactionId}';
        """);

    private (int Quantity, decimal TotalAmount) GetRefundRow(string originalTransactionId)
    {
        var quantity = QueryScalar<int>($"""
            SELECT quantity FROM Stock_Transaction
            WHERE related_transaction_id = '{originalTransactionId}' AND transaction_type = 'Refund';
            """);

        var totalAmount = QueryScalar<double>($"""
            SELECT total_amount FROM Stock_Transaction
            WHERE related_transaction_id = '{originalTransactionId}' AND transaction_type = 'Refund';
            """);

        return (quantity, (decimal)totalAmount);
    }

    private string GetRefundReason(string originalTransactionId) => QueryScalar<string>($"""
        SELECT reason FROM Stock_Transaction
        WHERE related_transaction_id = '{originalTransactionId}' AND transaction_type = 'Refund';
        """);
}
