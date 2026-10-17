using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 상품 수정이 실제 SQLite까지 닿는지. 가짜 저장소로는 확인할 수 없는 구간이다.
///
/// 이 테스트가 생긴 이유: "임포트로 unit이 안 바뀐다"는 보고를 쫓다가, 임포트 규칙은
/// 가짜 저장소로 검증돼 있는데 <b>그 뒤 UPDATE 문이 그 칸을 정말 쓰는지는 아무도
/// 확인하지 않고 있었다</b>는 것을 알았다. UPDATE에서 컬럼 하나가 빠지면 계획도
/// 적용도 성공으로 끝나고 값만 그대로 남는다 — 화면에 실패가 없으니 알 방법이 없다.
/// </summary>
public class ProductUpdatePersistenceTests : IDisposable
{
    private const string ProductId = "prod-1";

    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ProductRepository _products;
    private readonly ProductService _service;

    public ProductUpdatePersistenceTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-product-update-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();

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
            // 임시 폴더가 남는 것은 결과에 영향을 주지 않는다.
        }
    }

    private sealed class FixedBarcodeSequence : IInternalBarcodeSequenceRepository
    {
        public Task<string> GetNextInternalBarcodeAsync() => Task.FromResult("INT-00000001");
    }

    private static Product Amoxil() => new()
    {
        ProductId = ProductId,
        ProductName = "Amoxil 500mg Capsule",
        GenericName = "Amoxicillin",
        Strength = "500mg",
        DosageForm = DosageForm.Capsule,
        Unit = "Tablet",
        UnitsPerBox = 1,
        CostPrice = 3.00m,
        SellingPrice = 4.53m,
        SafetyStockLevel = 10,
        Manufacturer = "Maker A",
        CountryOfOrigin = "KH",
        AtcCode = "J01CA04",
        Status = EntityStatus.Active,
        CreatedAt = 0
    };

    private async Task<Product> SeedAsync()
    {
        var product = Amoxil();
        await _products.InsertAsync(product);
        return product;
    }

    private async Task<Product> ReadBackAsync() =>
        (await _products.GetByIdAsync(ProductId))!;

    /// <summary>보고된 증상 그대로: 단위만 바꿔 저장한다.</summary>
    [Fact]
    public async Task Unit_SurvivesAnUpdate()
    {
        var product = await SeedAsync();
        product.Unit = "Capsule";

        var result = await _service.SaveProductAsync(product, isNewProduct: false, userId: "u1");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("Capsule", (await ReadBackAsync()).Unit);
    }

    /// <summary>
    /// 임포트가 고칠 수 있는 칸을 한꺼번에 확인한다. UPDATE 문에서 빠진 컬럼이 있으면
    /// 여기서 걸린다 — 한 칸씩 테스트를 쓰지 않는 이유는, 빠진 칸이 어느 것일지
    /// 모르는 상태에서 찾으려는 검사이기 때문이다.
    /// </summary>
    [Fact]
    public async Task EveryImportableColumn_SurvivesAnUpdate()
    {
        var product = await SeedAsync();

        product.Unit = "Bottle";
        product.ProductName = "Amoxil 250mg Syrup";
        product.GenericName = "Amoxicillin trihydrate";
        product.Strength = "250mg/5ml";
        product.DosageForm = DosageForm.Syrup;
        product.Barcode = "8801234567890";
        product.CostPrice = 5.25m;
        product.SellingPrice = 7.50m;
        product.SafetyStockLevel = 42;
        product.Manufacturer = "Maker B";
        product.CountryOfOrigin = "VN";
        product.AtcCode = "J01CA04";
        product.IsCombination = true;
        product.Status = EntityStatus.Inactive;

        var result = await _service.SaveProductAsync(product, isNewProduct: false, userId: "u1");
        Assert.True(result.IsSuccess, result.Message);

        var stored = await ReadBackAsync();

        Assert.Equal("Bottle", stored.Unit);
        Assert.Equal("Amoxil 250mg Syrup", stored.ProductName);
        Assert.Equal("Amoxicillin trihydrate", stored.GenericName);
        Assert.Equal("250mg/5ml", stored.Strength);
        Assert.Equal(DosageForm.Syrup, stored.DosageForm);
        Assert.Equal("8801234567890", stored.Barcode);
        Assert.Equal(5.25m, stored.CostPrice);
        Assert.Equal(7.50m, stored.SellingPrice);
        Assert.Equal(42, stored.SafetyStockLevel);
        Assert.Equal("Maker B", stored.Manufacturer);
        Assert.Equal("VN", stored.CountryOfOrigin);
        Assert.Equal("J01CA04", stored.AtcCode);
        Assert.True(stored.IsCombination);
        Assert.Equal(EntityStatus.Inactive, stored.Status);
    }

    /// <summary>
    /// 박스당 개수가 바뀌는 수정은 다른 UPDATE 경로(UpdateWithUnitsPerBoxChangeAsync)를
    /// 탄다. 그쪽에서도 같은 칸들이 써져야 한다 — 한쪽만 고치면 낱개 판매를 켜는
    /// 수정에서만 단위가 안 바뀌는, 찾기 어려운 상태가 된다.
    /// </summary>
    [Fact]
    public async Task Unit_SurvivesAnUpdateThatAlsoChangesTheBoxSize()
    {
        var product = await SeedAsync();

        product.Unit = "Capsule";
        product.UnitsPerBox = 30;
        product.UnitSellingPrice = 0.20m;

        var result = await _service.SaveProductAsync(product, isNewProduct: false, userId: "u1");
        Assert.True(result.IsSuccess, result.Message);

        var stored = await ReadBackAsync();

        Assert.Equal("Capsule", stored.Unit);
        Assert.Equal(30, stored.UnitsPerBox);
    }
}
