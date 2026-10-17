using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 상품의 박스당 개수가 바뀌면 재고가 따라가야 한다. 실제 SQLite에 대고 돌린다.
///
/// 이 앱에서 재고의 개수는 언제나 "파는 단위"의 개수다. 박스 구분이 없던 상품의 10은
/// 열 개의 통이고, 낱개 판매를 켜서 통 하나가 30정이 되면 그 10은 10박스(300정)이지
/// 낱개 10정이 아니다. 재고를 그대로 두면 있던 재고가 박스당 개수만큼 줄어든 채로
/// 보이는데, 실제로 그렇게 됐던 버그다 — CSV로 10을 넣고 낱개 판매를 켜니 낱개 10개가 됐다.
/// </summary>
public class UnitsPerBoxChangeTests : IDisposable
{
    private const string FacilityId = "fac-1";
    private const string UserId = "user-1";
    private const string ProductId = "prod-1";

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ProductService _service;
    private readonly ProductRepository _products;

    public UnitsPerBoxChangeTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-unitsperbox-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();
        SeedFacilityAndUser();

        _products = new ProductRepository(_connectionFactory);
        _service = new ProductService(_products, new FixedBarcodeSequence());
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

    private sealed class FixedBarcodeSequence : IInternalBarcodeSequenceRepository
    {
        public Task<string> GetNextInternalBarcodeAsync() => Task.FromResult("INT-00000001");
    }

    private void SeedFacilityAndUser()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
            VALUES ('{FacilityId}', 'F', 'KH', 'D', 'Pharmacy', 'Active');

            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ('{UserId}', '{FacilityId}', 'admin', 'h', 'Administrator', 'Active', 0);
            """;
        command.ExecuteNonQuery();
    }

    private static Product Amoxil(int unitsPerBox) => new()
    {
        ProductId = ProductId,
        ProductName = "Amoxil 500mg Capsule",
        GenericName = "Amoxicillin",
        DosageForm = DosageForm.Capsule,
        Unit = "Capsule",
        Manufacturer = "Maker A",
        UnitsPerBox = unitsPerBox,
        UnitSellingPrice = unitsPerBox > 1 ? 0.20m : null,
        CostPrice = 3.00m,
        SellingPrice = 4.53m,
        SafetyStockLevel = 5,
        Status = EntityStatus.Active,
        CreatedAt = 0
    };

    private async Task InsertProductAsync(int unitsPerBox) =>
        await _products.InsertAsync(Amoxil(unitsPerBox));

    private void InsertBatch(string inventoryId, string batch, int current, int boxes, int units)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Inventory
                (inventory_id, facility_id, product_id, batch_number, expiry_date,
                 current_quantity, box_quantity, unit_quantity, updated_at)
            VALUES ($id, $facility, $product, $batch, 0, $current, $boxes, $units, 0);
            """;
        command.Parameters.AddWithValue("$id", inventoryId);
        command.Parameters.AddWithValue("$facility", FacilityId);
        command.Parameters.AddWithValue("$product", ProductId);
        command.Parameters.AddWithValue("$batch", batch);
        command.Parameters.AddWithValue("$current", current);
        command.Parameters.AddWithValue("$boxes", boxes);
        command.Parameters.AddWithValue("$units", units);
        command.ExecuteNonQuery();
    }

    private (int Total, int Boxes, int Units) ReadInventory(string inventoryId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT current_quantity, box_quantity, unit_quantity
            FROM Inventory WHERE inventory_id = $id;
            """;
        command.Parameters.AddWithValue("$id", inventoryId);

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    private List<(int Quantity, string Reason, long? Before, long? After, string Batch)> ReadLedger()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT quantity, COALESCE(reason, ''), stock_before, stock_after, batch_number
            FROM Stock_Transaction
            WHERE transaction_type = 'Adjustment'
            ORDER BY batch_number;
            """;

        var rows = new List<(int, string, long?, long?, string)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add((reader.GetInt32(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt64(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.GetString(4)));
        }
        return rows;
    }

    private async Task<int> ReadUnitsPerBoxAsync() =>
        (await _products.GetByIdAsync(ProductId))!.UnitsPerBox;

    /// <summary>CSV로 10을 넣은 상품(낱개 10 = 통 10개)에 낱개 판매(30정)를 켜면 10박스 300정이다.</summary>
    [Fact]
    public async Task EnablingLooseSale_TurnsEveryCountedItemIntoABox()
    {
        await InsertProductAsync(unitsPerBox: 1);
        InsertBatch("inv-1", "B1", current: 10, boxes: 0, units: 10);

        var result = await _service.SaveProductAsync(Amoxil(30), isNewProduct: false, UserId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(30, await ReadUnitsPerBoxAsync());
        Assert.Equal((300, 10, 0), ReadInventory("inv-1"));
    }

    /// <summary>모든 배치가 함께 바뀐다. 한 배치만 바뀌면 같은 상품 안에서 단위가 섞인다.</summary>
    [Fact]
    public async Task EnablingLooseSale_RecountsEveryBatch()
    {
        await InsertProductAsync(unitsPerBox: 1);
        InsertBatch("inv-1", "B1", current: 10, boxes: 0, units: 10);
        InsertBatch("inv-2", "B2", current: 4, boxes: 0, units: 4);

        await _service.SaveProductAsync(Amoxil(30), isNewProduct: false, UserId);

        Assert.Equal((300, 10, 0), ReadInventory("inv-1"));
        Assert.Equal((120, 4, 0), ReadInventory("inv-2"));
    }

    /// <summary>
    /// 재고 사슬(stock_before/after)이 끊기지 않도록 배치마다 조정 원장 행을 남긴다.
    /// 재고가 움직인 것이 아니라 세는 단위가 바뀐 것이므로 이유에 그렇게 적는다.
    /// </summary>
    [Fact]
    public async Task Recount_LeavesAnAdjustmentRowPerBatchWithTheChainIntact()
    {
        await InsertProductAsync(unitsPerBox: 1);
        InsertBatch("inv-1", "B1", current: 10, boxes: 0, units: 10);
        InsertBatch("inv-2", "B2", current: 4, boxes: 0, units: 4);

        await _service.SaveProductAsync(Amoxil(30), isNewProduct: false, UserId);

        var ledger = ReadLedger();
        Assert.Equal(2, ledger.Count);

        var (quantity, reason, before, after, batch) = ledger[0];
        Assert.Equal("B1", batch);
        Assert.Equal(290, quantity);
        Assert.Equal(10, before);
        Assert.Equal(300, after);
        Assert.Contains("Units per box changed 1 → 30", reason);
        Assert.Contains("10 box(es)", reason);

        Assert.Equal((116, 4L, 120L), (ledger[1].Quantity, ledger[1].Before, ledger[1].After));
    }

    /// <summary>박스당 개수만 바뀌면(30 → 20) 박스와 낱개는 그대로고 총량만 다시 계산된다.</summary>
    [Fact]
    public async Task ChangingBoxSize_KeepsBoxesAndLooseUnits()
    {
        await InsertProductAsync(unitsPerBox: 30);
        InsertBatch("inv-1", "B1", current: 65, boxes: 2, units: 5);

        var result = await _service.SaveProductAsync(Amoxil(20), isNewProduct: false, UserId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal((45, 2, 5), ReadInventory("inv-1"));
        Assert.Equal((-20, 65L, 45L), (ReadLedger()[0].Quantity, ReadLedger()[0].Before, ReadLedger()[0].After));
    }

    /// <summary>낱개 판매를 끄면 박스가 다시 파는 단위다. 2박스 = 2개.</summary>
    [Fact]
    public async Task DisablingLooseSale_KeepsTheBoxCount()
    {
        await InsertProductAsync(unitsPerBox: 30);
        InsertBatch("inv-1", "B1", current: 60, boxes: 2, units: 0);

        var result = await _service.SaveProductAsync(Amoxil(1), isNewProduct: false, UserId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(1, await ReadUnitsPerBoxAsync());
        Assert.Equal((2, 0, 2), ReadInventory("inv-1"));
    }

    /// <summary>
    /// 헐어 놓은 낱개가 남은 채로 낱개 판매를 끄면 그 낱개는 셀 자리가 없다.
    /// 버리느니 거절하고, 상품도 재고도 그대로 둔다.
    /// </summary>
    [Fact]
    public async Task DisablingLooseSale_WithLooseUnitsInStock_IsRefusedAndNothingChanges()
    {
        await InsertProductAsync(unitsPerBox: 30);
        InsertBatch("inv-1", "B1", current: 65, boxes: 2, units: 5);

        var result = await _service.SaveProductAsync(Amoxil(1), isNewProduct: false, UserId);

        Assert.False(result.IsSuccess);
        Assert.Contains("Loose units are still in stock", result.Message);
        Assert.Equal(30, await ReadUnitsPerBoxAsync());
        Assert.Equal((65, 2, 5), ReadInventory("inv-1"));
        Assert.Empty(ReadLedger());
    }

    /// <summary>박스당 개수가 그대로면 재고를 건드리지 않고 원장에도 아무것도 남지 않는다.</summary>
    [Fact]
    public async Task EditWithoutBoxSizeChange_LeavesStockAlone()
    {
        await InsertProductAsync(unitsPerBox: 30);
        InsertBatch("inv-1", "B1", current: 65, boxes: 2, units: 5);

        var edited = Amoxil(30);
        edited.SellingPrice = 5.00m;
        var result = await _service.SaveProductAsync(edited, isNewProduct: false, UserId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal((65, 2, 5), ReadInventory("inv-1"));
        Assert.Empty(ReadLedger());
    }
}
