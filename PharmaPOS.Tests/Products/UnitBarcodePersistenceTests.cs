using Microsoft.Data.Sqlite;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 낱개 바코드가 실제로 SQLite에 저장되고 다시 읽히는지, 그리고 그 값으로 검색이
/// 되는지 본다. 매핑은 서수(ordinal)로 손으로 하므로, 컬럼을 하나 더하면서 자리를
/// 잘못 세면 엉뚱한 칸이 바코드로 읽히고도 예외 없이 지나간다.
///
/// 컬럼을 이미 만들어진 DB에 뒤늦게 붙이는 경로(ApplyMigrations)도 함께 본다.
/// 거기서 실패하면 새로 설치한 PC에서만 되고 쓰던 약국에서는 앱이 열리지 않는다.
/// </summary>
public class UnitBarcodePersistenceTests : IDisposable
{
    private readonly string _directory;
    private readonly string _databasePath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ProductRepository _repository;

    public UnitBarcodePersistenceTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-unit-barcode-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _databasePath = Path.Combine(_directory, "test.db");
        _connectionFactory = new SqliteConnectionFactory(_databasePath);
        new DatabaseInitializer(_connectionFactory).Initialize();

        _repository = new ProductRepository(_connectionFactory);
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

    private static Product Boxed(string productId) => new()
    {
        ProductId = productId,
        ProductName = "Amoxil 500mg Capsule",
        Unit = "Capsule",
        Manufacturer = "Maker A",
        UnitsPerBox = 30,
        CostPrice = 3.00m,
        SellingPrice = 9.00m,
        SafetyStockLevel = 10,
        Status = EntityStatus.Active,
        CreatedAt = 0
    };

    [Fact]
    public async Task UnitBarcode_SurvivesInsertAndRead()
    {
        var product = Boxed("p-1");
        product.Barcode = "8801111111111";
        product.UnitBarcodeOverride = "8802222222222";

        await _repository.InsertAsync(product);

        var stored = await _repository.GetByIdAsync("p-1");

        Assert.NotNull(stored);
        Assert.Equal("8802222222222", stored.UnitBarcodeOverride);
        Assert.Equal("8802222222222", stored.UnitBarcode);

        // 바로 옆 칸을 잘못 세지 않았는지. 서수 매핑이 밀리면 여기가 먼저 틀어진다.
        Assert.Equal("8801111111111", stored.Barcode);
        Assert.Equal("Amoxil 500mg Capsule", stored.ProductName);
        Assert.Equal(30, stored.UnitsPerBox);
    }

    [Fact]
    public async Task UnitBarcode_SurvivesUpdate()
    {
        var product = Boxed("p-1");
        await _repository.InsertAsync(product);

        product.UnitBarcodeOverride = "8802222222222";
        await _repository.UpdateAsync(product);

        var stored = await _repository.GetByIdAsync("p-1");
        Assert.Equal("8802222222222", stored!.UnitBarcodeOverride);
    }

    /// <summary>낱개 코드를 찍었을 때 그 상품이 검색으로 잡혀야 계산대까지 간다.</summary>
    [Fact]
    public async Task ScanningTheUnitBarcode_FindsTheProduct()
    {
        var product = Boxed("p-1");
        product.UnitBarcodeOverride = "8802222222222";
        await _repository.InsertAsync(product);

        var results = await _repository.SearchAsync("8802222222222", EntityStatus.Active);

        Assert.Equal("p-1", Assert.Single(results).ProductId);
    }

    /// <summary>
    /// 저장돼 있지 않은 낱개 코드 — 내부 바코드 + "-EA" — 도 이미 쓰이는 코드로 본다.
    /// 그 값은 어느 칸에도 통째로 들어 있지 않아 SQL을 잘못 쓰면 조용히 통과한다.
    /// </summary>
    [Fact]
    public async Task GeneratedLooseCodeOfAnotherProduct_CountsAsInUse()
    {
        var product = Boxed("p-1");
        product.InternalBarcode = "INT-00000007";
        await _repository.InsertAsync(product);

        Assert.True(await _repository.BarcodeInUseAsync("INT-00000007-EA"));
        Assert.True(await _repository.BarcodeInUseAsync("INT-00000007"));

        // 자기 자신을 고칠 때는 걸리지 않아야 한다.
        Assert.False(await _repository.BarcodeInUseAsync("INT-00000007-EA", excludeProductId: "p-1"));
    }

    /// <summary>
    /// 소분하지 않는 상품(units_per_box = 1)에는 낱개 코드가 없다.
    /// 그 상품의 "내부 바코드 + -EA"는 스캔되지 않으므로 막을 이유도 없다.
    /// </summary>
    [Fact]
    public async Task GeneratedLooseCodeOfANonBoxedProduct_IsNotInUse()
    {
        var product = Boxed("p-1");
        product.UnitsPerBox = 1;
        product.InternalBarcode = "INT-00000007";
        await _repository.InsertAsync(product);

        Assert.False(await _repository.BarcodeInUseAsync("INT-00000007-EA"));
    }

    /// <summary>
    /// 컬럼이 없는 기존 DB에 마이그레이션이 붙는지. 컬럼과 인덱스를 지운 뒤
    /// 초기화를 다시 돌려, 쓰던 약국의 DB가 열리는 경로를 그대로 밟는다.
    /// </summary>
    [Fact]
    public async Task ExistingDatabaseWithoutTheColumn_GetsItOnStartup()
    {
        using (var connection = _connectionFactory.CreateOpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                DROP INDEX IF EXISTS idx_product_unit_barcode;
                ALTER TABLE Product_Master DROP COLUMN unit_barcode;
                """;
            command.ExecuteNonQuery();
        }

        // 여기서 던지면 약국 PC에서 앱이 아예 열리지 않는다는 뜻이다.
        new DatabaseInitializer(_connectionFactory).Initialize();

        var product = Boxed("p-1");
        product.UnitBarcodeOverride = "8802222222222";
        await _repository.InsertAsync(product);

        Assert.Equal("8802222222222", (await _repository.GetByIdAsync("p-1"))!.UnitBarcodeOverride);
    }
}
