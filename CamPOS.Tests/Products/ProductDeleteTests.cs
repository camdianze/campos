using Microsoft.Data.Sqlite;
using CamPOS.Application.Products;
using CamPOS.Application.Repositories;
using CamPOS.DataAccess.Database;
using CamPOS.DataAccess.Repositories;
using CamPOS.Domain.Entities;
using CamPOS.Domain.Enums;

namespace CamPOS.Tests.Products;

/// <summary>
/// 상품 삭제. 실제 SQLite에 대고 돌린다.
///
/// 이 기능의 전부는 <b>지울 수 있는 범위를 좁게 잡은 것</b>이다. 판매·입고·조정과
/// 상담 기록은 전부 product_id로 상품을 가리키므로, 이력이 있는 상품을 지우면
/// 지난 기록에 이름 없는 줄이 남는다. 그것은 되돌릴 수 없고, 알아차릴 때쯤이면
/// 왜 그런지 아는 사람도 없다.
///
/// 그래서 지우기는 "애초에 있어서는 안 됐던 것"에 대한 답이고, "앞으로 안 보이게"에
/// 대한 답은 Inactive다. 거부할 때 그렇게 말해 준다.
/// </summary>
public class ProductDeleteTests : IDisposable
{
    private const string FacilityId = "fac-1";
    private const string UserId = "user-1";
    private const string ProductId = "prod-1";

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ProductRepository _products;
    private readonly ProductService _service;

    public ProductDeleteTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-delete-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();
        SeedFacilityAndUser();

        _products = new ProductRepository(_connectionFactory);
        _service = new ProductService(_products, new FixedBarcodeSequence());
    }

    // ── 지울 수 있는 때 ─────────────────────────────────────────────────

    [Fact]
    public async Task AProductThatWasNeverUsedIsDeleted()
    {
        await _products.InsertAsync(NewProduct());

        var result = await _service.DeleteProductAsync(ProductId);

        Assert.True(result.IsSuccess);
        Assert.Null(await _products.GetByIdAsync(ProductId));
    }

    /// <summary>
    /// 수량 0인 배치 행은 같이 지운다. 빈 선반 자리일 뿐 이력이 아니고,
    /// 남겨 두면 외래키가 걸려 삭제 자체가 실패한다.
    /// </summary>
    [Fact]
    public async Task EmptyBatchRowsGoWithIt()
    {
        await _products.InsertAsync(NewProduct());
        InsertBatch("inv-1", "B1", current: 0);

        Assert.True((await _service.DeleteProductAsync(ProductId)).IsSuccess);
        Assert.Equal(0, CountInventoryRows());
    }

    // ── 지울 수 없는 때 ─────────────────────────────────────────────────

    /// <summary>이 테스트가 이 기능의 이유다.</summary>
    [Fact]
    public async Task AProductWithLedgerRowsIsRefusedAndKept()
    {
        await _products.InsertAsync(NewProduct());
        InsertLedgerRow();

        var result = await _service.DeleteProductAsync(ProductId);

        Assert.False(result.IsSuccess);
        Assert.NotNull(await _products.GetByIdAsync(ProductId));
    }

    /// <summary>
    /// 거부로 끝내지 않고 무엇을 하라고 말한다. "지울 수 없습니다"만 보면 다음
    /// 할 일이 없어지고, 그 상품은 목록에 영원히 남는다.
    /// </summary>
    [Fact]
    public async Task TheRefusalPointsAtInactive()
    {
        await _products.InsertAsync(NewProduct());
        InsertLedgerRow();

        var result = await _service.DeleteProductAsync(ProductId);

        Assert.Contains("Inactive", result.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 입고가 늘 원장을 남기므로 이 상태는 보통 생기지 않는다. 그래도 막는 것은,
    /// 그 가정이 깨진 자리에서 재고 숫자가 조용히 사라지는 쪽이 더 나쁘기 때문이다.
    /// </summary>
    [Fact]
    public async Task AProductStillHoldingStockIsRefused()
    {
        await _products.InsertAsync(NewProduct());
        InsertBatch("inv-1", "B1", current: 7);

        var result = await _service.DeleteProductAsync(ProductId);

        Assert.False(result.IsSuccess);
        Assert.NotNull(await _products.GetByIdAsync(ProductId));
        Assert.Equal(1, CountInventoryRows());
    }

    [Fact]
    public async Task DeletingSomethingThatIsAlreadyGoneSaysSoRatherThanThrowing()
    {
        var result = await _service.DeleteProductAsync("no-such-product");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task AnEmptyIdIsRefused()
    {
        Assert.False((await _service.DeleteProductAsync("   ")).IsSuccess);
    }

    // ── 받침대 ──────────────────────────────────────────────────────────

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

    private static Product NewProduct() => new()
    {
        ProductId = ProductId,
        ProductName = "Amoxil 500mg Capsule",
        GenericName = "Amoxicillin",
        DosageForm = DosageForm.Capsule,
        Unit = "Capsule",
        Manufacturer = "Maker A",
        UnitsPerBox = 1,
        CostPrice = 3.00m,
        SellingPrice = 4.53m,
        SafetyStockLevel = 5,
        Status = EntityStatus.Active,
        CreatedAt = 0,
    };

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

    private void InsertBatch(string inventoryId, string batch, int current)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Inventory
                (inventory_id, facility_id, product_id, batch_number, expiry_date,
                 current_quantity, box_quantity, unit_quantity, updated_at)
            VALUES ($id, $facility, $product, $batch, 0, $current, 0, $current, 0);
            """;
        command.Parameters.AddWithValue("$id", inventoryId);
        command.Parameters.AddWithValue("$facility", FacilityId);
        command.Parameters.AddWithValue("$product", ProductId);
        command.Parameters.AddWithValue("$batch", batch);
        command.Parameters.AddWithValue("$current", current);
        command.ExecuteNonQuery();
    }

    private void InsertLedgerRow()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Stock_Transaction
                (transaction_id, facility_id, product_id, user_id, transaction_type,
                 batch_number, expiry_date, quantity, transaction_time)
            VALUES ('t-1', $facility, $product, $user, 'StockIn', 'B1', 0, 10, 0);
            """;
        command.Parameters.AddWithValue("$facility", FacilityId);
        command.Parameters.AddWithValue("$product", ProductId);
        command.Parameters.AddWithValue("$user", UserId);
        command.ExecuteNonQuery();
    }

    private int CountInventoryRows()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Inventory WHERE product_id = $product;";
        command.Parameters.AddWithValue("$product", ProductId);

        return Convert.ToInt32(command.ExecuteScalar());
    }
}
