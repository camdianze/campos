using System.Globalization;
using Microsoft.Data.Sqlite;
using CamPOS.Application.Inventory;
using CamPOS.DataAccess.Database;
using CamPOS.DataAccess.Repositories;
using CamPOS.Domain.Entities;
using CamPOS.Domain.Enums;

namespace CamPOS.Tests.Inventory;

/// <summary>
/// 원장에 남는 "그때의 원가와 환율".
///
/// 이 두 값의 실패는 전부 조용하다. 적지 않으면 나중에 분석하는 쪽이 현재 원가로 과거
/// 마진을 내고 오늘 환율로 작년 매출을 환산하는데, 양쪽 다 그럴듯한 숫자가 나와서
/// 틀린 줄을 모른다. 게다가 지나간 거래에는 채워 넣을 원본이 없으므로 나중에 고칠 수도
/// 없다 — 그래서 "값이 들어간다"가 아니라 <b>"나중에 바꿔도 그때 값이 남아 있다"</b>를
/// 확인한다.
/// </summary>
public class LedgerPriceSnapshotTests : IDisposable
{
    private const string FacilityId = "facility-1";
    private const string ProductId = "product-1";
    private const string UserId = "user-1";
    private const string BatchNumber = "B2401";

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;

    public LedgerPriceSnapshotTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "campos-price-snapshot-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();

        Seed(costPrice: 2.5m);
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
            // 임시 폴더라 남아도 그만이다.
        }
    }

    // ── 거들기 ──────────────────────────────────────────────────────────────

    private void Seed(decimal costPrice)
    {
        Execute($"""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('{FacilityId}', 'F', 'KH', 'D', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('{UserId}', '{FacilityId}', 'u', 'h', 'Administrator', 'Active', 0);

            INSERT INTO Product_Master
                (product_id, product_name, unit, cost_price, selling_price, safety_stock_level,
                 status, created_at, is_combination, units_per_box)
            VALUES ('{ProductId}', 'Test Product', 'Tablet',
                    {costPrice.ToString(CultureInfo.InvariantCulture)}, 4.0, 5, 'Active', 0, 0, 1);
            """);
    }

    private void Execute(string sql)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>환율 설정을 저장한다. ReceiptSettingsService가 쓰는 것과 같은 모양이다.</summary>
    private void SetExchangeRate(string storedValue)
    {
        Execute($"""
            INSERT OR REPLACE INTO App_Setting (setting_key, setting_value, value_type, updated_at)
            VALUES ('currency.rate', '{storedValue}', 'Number', 0);
            """);
    }

    private void SetCostPrice(decimal costPrice)
    {
        Execute($"""
            UPDATE Product_Master
            SET cost_price = {costPrice.ToString(CultureInfo.InvariantCulture)}
            WHERE product_id = '{ProductId}';
            """);
    }

    private (decimal? Cost, decimal? Rate) ReadSnapshot(string transactionId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT cost_price_at_transaction, exchange_rate_at_transaction
            FROM Stock_Transaction WHERE transaction_id = $id;
            """;
        command.Parameters.AddWithValue("$id", transactionId);

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        return (reader.IsDBNull(0) ? null : (decimal)reader.GetDouble(0),
                reader.IsDBNull(1) ? null : (decimal)reader.GetDouble(1));
    }

    private string ReadInventoryId()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT inventory_id FROM Inventory WHERE batch_number = $b;";
        command.Parameters.AddWithValue("$b", BatchNumber);

        return (string)command.ExecuteScalar()!;
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
        SellingPriceAtTransaction = type == TransactionType.StockIn ? null : 4.0m,
        PaymentMethod = type == TransactionType.StockIn ? null : "Cash",
        TotalAmount = type == TransactionType.StockIn ? null : quantity * 4.0m,
        TransactionTime = time
    };

    private async Task<string> StockInAsync(int quantity, long time = 1000)
    {
        var ledger = Ledger(TransactionType.StockIn, quantity, time);
        await new StockInRepository(_connectionFactory)
            .SaveStockInAsync(ledger, boxQuantity: 0, unitQuantity: quantity);

        return ledger.TransactionId;
    }

    private async Task<string> SellAsync(int quantity, long time = 3000)
    {
        var ledger = Ledger(TransactionType.StockOut, quantity, time);

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

        return ledger.TransactionId;
    }

    // ── 판매 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ASale_RecordsTheCostAndRateInEffectAtThatMoment()
    {
        SetExchangeRate("4100");
        await StockInAsync(100);

        var (cost, rate) = ReadSnapshot(await SellAsync(10));

        Assert.Equal(2.5m, cost);
        Assert.Equal(4100m, rate);
    }

    /// <summary>
    /// 이 테스트가 이 기능의 전부다. 상품 마스터의 원가는 매입가가 오르면 덮어써지므로,
    /// 그 값으로 과거 마진을 내면 틀린 숫자가 나온다 — 그럴듯해 보여서 틀린 줄을 모른다.
    /// </summary>
    [Fact]
    public async Task RaisingTheCostLater_LeavesTheOldSaleAlone()
    {
        SetExchangeRate("4100");
        await StockInAsync(100);

        var firstSale = await SellAsync(10, time: 3000);

        SetCostPrice(3.8m);
        SetExchangeRate("4200");

        var secondSale = await SellAsync(10, time: 4000);

        Assert.Equal((2.5m, 4100m), ReadSnapshot(firstSale));
        Assert.Equal((3.8m, 4200m), ReadSnapshot(secondSale));
    }

    /// <summary>
    /// 원가 0은 "공짜"가 아니라 "모른다"이다 — 종이 장부만 쓰던 약국의 상품이 그렇게
    /// 저장되어 있고(임포트 규칙: Empty = 0), 0을 그대로 적으면 마진이 100%로 계산된다.
    /// NULL이면 집계에서 눈에 띄므로 읽는 쪽이 모른다는 것을 안다.
    /// </summary>
    [Fact]
    public async Task AProductWithNoPurchasePrice_RecordsUnknownRatherThanZero()
    {
        SetCostPrice(0m);
        SetExchangeRate("4100");

        await StockInAsync(100);

        var (cost, rate) = ReadSnapshot(await SellAsync(10));

        Assert.Null(cost);
        Assert.Equal(4100m, rate);
    }

    /// <summary>환율을 설정하지 않은 약국도 있다. 0으로 적으면 나눗셈이 터진다.</summary>
    [Fact]
    public async Task NoExchangeRateConfigured_RecordsUnknownRatherThanZero()
    {
        await StockInAsync(100);

        var (cost, rate) = ReadSnapshot(await SellAsync(10));

        Assert.Equal(2.5m, cost);
        Assert.Null(rate);
    }

    /// <summary>
    /// 환율은 App_Setting에 글자로 들어 있고 InvariantCulture로 쓰인다. 읽을 때 PC 지역
    /// 설정을 따르면 소수점이 천 단위 구분자로 읽혀 4100.5가 41005가 된다 — 양수이고
    /// 자릿수도 그럴듯해서 어떤 검사도 걸러내지 못한다.
    /// </summary>
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("id-ID")]
    public async Task TheRateIsReadTheSameWayItWasWritten(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);

        try
        {
            SetExchangeRate("4100.5");
            await StockInAsync(100);

            Assert.Equal(4100.5m, ReadSnapshot(await SellAsync(10)).Rate);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ── 환불 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 환불은 그 판매를 되돌리는 것이므로, 수량과 금액이 음수로 상쇄되듯 원가와 환율도
    /// 같은 값이라야 상쇄된다. 환불 시점의 현재 원가를 읽으면 그 사이 매입가가 오른
    /// 상품의 마진이 환불 한 건으로 뒤틀린다.
    /// </summary>
    [Fact]
    public async Task ARefund_CarriesTheOriginalSalesValuesSoTheyCancelOut()
    {
        SetExchangeRate("4100");
        await StockInAsync(100);

        var saleId = await SellAsync(10);

        // 환불하기 전에 매입가와 환율이 둘 다 바뀌었다.
        SetCostPrice(3.8m);
        SetExchangeRate("4200");

        var refund = Ledger(TransactionType.Refund, -4, time: 5000);
        refund.RelatedTransactionId = saleId;
        refund.TotalAmount = -16.0m;

        var saved = await new RefundRepository(_connectionFactory).SaveRefundAsync(new[]
        {
            new RefundLineForSave
            {
                Transaction = refund,
                RefundQuantity = 4,
                ReturnToStock = true,
                UnitsPerBox = 1
            }
        });

        Assert.True(saved);
        Assert.Equal((2.5m, 4100m), ReadSnapshot(refund.TransactionId));
    }

    // ── 입고와 조정 ─────────────────────────────────────────────────────────

    [Fact]
    public async Task StockIn_RecordsWhatTheBatchCostOnArrival()
    {
        SetExchangeRate("4100");

        Assert.Equal((2.5m, 4100m), ReadSnapshot(await StockInAsync(100)));
    }

    /// <summary>
    /// 폐기 손실을 세려면 조정에도 원가가 있어야 한다. 조정 행에는 금액 열이 비어
    /// 있으므로 버린 재고가 얼마어치였는지를 아는 자리가 여기 말고 없다.
    /// </summary>
    [Fact]
    public async Task AnAdjustment_RecordsTheCostSoWriteOffsCanBeValued()
    {
        SetExchangeRate("4100");
        await StockInAsync(100);

        var ledger = Ledger(TransactionType.Adjustment, -8, time: 4000);
        ledger.SellingPriceAtTransaction = null;
        ledger.PaymentMethod = null;
        ledger.TotalAmount = null;
        ledger.Reason = "Expired";

        var saved = await new AdjustmentRepository(_connectionFactory).SaveAdjustmentAsync(
            ledger, ReadInventoryId(), BatchNumber,
            expectedCurrentQuantity: 100, physicalCount: 92,
            physicalBoxCount: 0, physicalUnitCount: 92);

        Assert.True(saved);
        Assert.Equal(2.5m, ReadSnapshot(ledger.TransactionId).Cost);
    }
}
