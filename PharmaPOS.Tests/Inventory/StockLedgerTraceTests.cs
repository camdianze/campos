using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Inventory;

/// <summary>
/// 원장에 남는 차감 전후 재고.
///
/// 여기를 실제 DB로 테스트하는 이유: 이 값의 쓸모는 전적으로 <b>어디서 읽었는가</b>에 달려 있다.
/// before + quantity = after 로 계산해 넣으면 그 식은 언제나 참이라 아무것도 못 잡는다.
/// 그래서 "Inventory에 실제로 저장된 값과 같은가"를 확인한다.
/// </summary>
public class StockLedgerTraceTests : IDisposable
{
    private const string FacilityId = "facility-1";
    private const string ProductId = "product-1";
    private const string UserId = "user-1";
    private const string BatchNumber = "B2401";

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;

    public StockLedgerTraceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "pharmapos-ledger-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();

        SeedFacilityUserAndProduct();
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
            // 파일이 아직 잡혀 있어도 임시 폴더라 남겨 두면 그만이다.
        }
    }

    private void SeedFacilityUserAndProduct()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('{FacilityId}', 'F', 'KH', 'D', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('{UserId}', '{FacilityId}', 'u', 'h', 'Administrator', 'Active', 0);

            INSERT INTO Product_Master
                (product_id, product_name, unit, cost_price, selling_price, safety_stock_level,
                 status, created_at, is_combination, units_per_box)
            VALUES ('{ProductId}', 'Test Product', 'Tablet', 500, 1000, 5, 'Active', 0, 0, 1);
            """;
        command.ExecuteNonQuery();
    }

    private static StockTransaction Ledger(TransactionType type, int quantity, long time) => new()
    {
        TransactionId = Guid.NewGuid().ToString(),
        FacilityId = FacilityId,
        ProductId = ProductId,
        UserId = UserId,
        TransactionType = type,
        BatchNumber = BatchNumber,
        ExpiryDate = 4102444800000,
        Quantity = quantity,
        SellingPriceAtTransaction = type == TransactionType.StockIn ? null : 1000,
        PaymentMethod = type == TransactionType.StockIn ? null : "Cash",
        TotalAmount = type == TransactionType.StockIn ? null : quantity * 1000,
        TransactionTime = time
    };

    private (long? Before, long? After) ReadTrace(string transactionId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT stock_before, stock_after FROM Stock_Transaction WHERE transaction_id = $id;";
        command.Parameters.AddWithValue("$id", transactionId);

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        return (reader.IsDBNull(0) ? null : reader.GetInt64(0),
                reader.IsDBNull(1) ? null : reader.GetInt64(1));
    }

    private long ReadCurrentQuantity()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT current_quantity FROM Inventory WHERE batch_number = $b;";
        command.Parameters.AddWithValue("$b", BatchNumber);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    private string ReadInventoryId()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT inventory_id FROM Inventory WHERE batch_number = $b;";
        command.Parameters.AddWithValue("$b", BatchNumber);

        return (string)command.ExecuteScalar()!;
    }

    private async Task<string> StockInAsync(int quantity, long time = 1000)
    {
        var ledger = Ledger(TransactionType.StockIn, quantity, time);
        await new StockInRepository(_connectionFactory).SaveStockInAsync(ledger, boxQuantity: 0, unitQuantity: quantity);
        return ledger.TransactionId;
    }

    /// <summary>
    /// 배치가 처음 생기는 입고. 그 전에는 재고 행 자체가 없으므로 before는 비어 있어야 한다 —
    /// 0으로 적으면 "재고가 0이던 배치"와 "없던 배치"가 같아 보인다.
    /// </summary>
    [Fact]
    public async Task StockIn_LeavesBeforeEmptyWhenTheBatchIsNew()
    {
        var transactionId = await StockInAsync(100);

        var (before, after) = ReadTrace(transactionId);

        Assert.Null(before);
        Assert.Equal(100, after);
        Assert.Equal(after, ReadCurrentQuantity());
    }

    [Fact]
    public async Task StockIn_RecordsTheBalanceOnBothSides()
    {
        await StockInAsync(100);
        var secondId = await StockInAsync(50, time: 2000);

        var (before, after) = ReadTrace(secondId);

        Assert.Equal(100, before);
        Assert.Equal(150, after);
        Assert.Equal(after, ReadCurrentQuantity());
    }

    /// <summary>판매가 차감한 결과가 실제 재고와 같아야 한다.</summary>
    [Fact]
    public async Task Sale_RecordsTheBalanceItActuallyLeftBehind()
    {
        await StockInAsync(100);

        var ledger = Ledger(TransactionType.StockOut, 30, time: 3000);

        var saved = await new SaleRepository(_connectionFactory).SaveSaleAsync(new[]
        {
            new SaleLineForSave
            {
                InventoryId = ReadInventoryId(),
                IsBoxSale = false,
                BoxCount = 0,
                UnitsPerBox = 1,
                Transaction = ledger
            }
        });

        Assert.True(saved);

        var (before, after) = ReadTrace(ledger.TransactionId);

        Assert.Equal(100, before);
        Assert.Equal(70, after);
        Assert.Equal(after, ReadCurrentQuantity());
    }

    /// <summary>
    /// 조정은 실사 값으로 재고를 덮어쓴다. 차이(quantity)만 보고는 어느 수준에서
    /// 어느 수준으로 옮겼는지 알 수 없어서, 이 두 값이 특히 쓸모 있다.
    /// </summary>
    [Fact]
    public async Task Adjustment_RecordsTheLevelBeforeAndAfterTheRecount()
    {
        await StockInAsync(100);

        var ledger = Ledger(TransactionType.Adjustment, -8, time: 4000);

        var saved = await new AdjustmentRepository(_connectionFactory).SaveAdjustmentAsync(
            ledger, ReadInventoryId(), BatchNumber,
            expectedCurrentQuantity: 100, physicalCount: 92,
            physicalBoxCount: 0, physicalUnitCount: 92);

        Assert.True(saved);

        var (before, after) = ReadTrace(ledger.TransactionId);

        Assert.Equal(100, before);
        Assert.Equal(92, after);
        Assert.Equal(after, ReadCurrentQuantity());
    }

    /// <summary>
    /// 재고를 되돌리지 않는 환불은 before와 after가 같아야 한다.
    /// 그 자체가 "돈만 돌려주고 재고는 그대로"라는 사실을 파일에서 읽히게 한다.
    /// </summary>
    [Fact]
    public async Task Refund_WithoutReturningStock_LeavesTheBalanceUnchanged()
    {
        await StockInAsync(100);

        var sale = Ledger(TransactionType.StockOut, 10, time: 3000);
        await new SaleRepository(_connectionFactory).SaveSaleAsync(new[]
        {
            new SaleLineForSave
            {
                InventoryId = ReadInventoryId(),
                IsBoxSale = false,
                BoxCount = 0,
                UnitsPerBox = 1,
                Transaction = sale
            }
        });

        var refund = Ledger(TransactionType.Refund, -4, time: 5000);
        refund.TotalAmount = -4000;
        refund.RelatedTransactionId = sale.TransactionId;

        var refunded = await new RefundRepository(_connectionFactory).SaveRefundAsync(new[]
        {
            new RefundLineForSave
            {
                RefundQuantity = 4,
                UnitsPerBox = 1,
                ReturnToStock = false,
                Transaction = refund
            }
        });

        Assert.True(refunded);

        var (before, after) = ReadTrace(refund.TransactionId);

        Assert.Equal(90, before);
        Assert.Equal(90, after);
        Assert.Equal(90, ReadCurrentQuantity());
    }

    /// <summary>재고를 되돌리는 환불은 되돌아온 만큼 늘어난 값이 적혀야 한다.</summary>
    [Fact]
    public async Task Refund_ReturningStock_RecordsTheIncrease()
    {
        await StockInAsync(100);

        var sale = Ledger(TransactionType.StockOut, 10, time: 3000);
        await new SaleRepository(_connectionFactory).SaveSaleAsync(new[]
        {
            new SaleLineForSave
            {
                InventoryId = ReadInventoryId(),
                IsBoxSale = false,
                BoxCount = 0,
                UnitsPerBox = 1,
                Transaction = sale
            }
        });

        var refund = Ledger(TransactionType.Refund, -4, time: 5000);
        refund.TotalAmount = -4000;
        refund.RelatedTransactionId = sale.TransactionId;

        await new RefundRepository(_connectionFactory).SaveRefundAsync(new[]
        {
            new RefundLineForSave
            {
                RefundQuantity = 4,
                UnitsPerBox = 1,
                ReturnToStock = true,
                Transaction = refund
            }
        });

        var (before, after) = ReadTrace(refund.TransactionId);

        Assert.Equal(90, before);
        Assert.Equal(94, after);
        Assert.Equal(after, ReadCurrentQuantity());
    }

    /// <summary>
    /// 이어지는 거래들의 체인이 끊기지 않아야 한다. 앞 줄의 after와 다음 줄의 before가
    /// 어긋나는 지점을 찾는 것이 이 값들의 전부다.
    /// </summary>
    [Fact]
    public async Task ConsecutiveTransactions_FormAnUnbrokenChain()
    {
        var first = await StockInAsync(100);
        var second = await StockInAsync(20, time: 2000);

        var sale = Ledger(TransactionType.StockOut, 30, time: 3000);
        await new SaleRepository(_connectionFactory).SaveSaleAsync(new[]
        {
            new SaleLineForSave
            {
                InventoryId = ReadInventoryId(),
                IsBoxSale = false,
                BoxCount = 0,
                UnitsPerBox = 1,
                Transaction = sale
            }
        });

        var firstTrace = ReadTrace(first);
        var secondTrace = ReadTrace(second);
        var saleTrace = ReadTrace(sale.TransactionId);

        Assert.Equal(firstTrace.After, secondTrace.Before);
        Assert.Equal(secondTrace.After, saleTrace.Before);
        Assert.Equal(saleTrace.After, ReadCurrentQuantity());
    }
}
