using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 손으로 적어 넣는 바코드.
///
/// 내부 바코드는 오랫동안 자동 발급만 됐고 화면에서는 읽기 전용이었다. 그런데 약국이
/// 이미 쓰던 코드가 있거나, 낱개(블리스터 한 알, 한 포)에 제조사가 코드를 따로 찍어 둔
/// 상품이 있다. 그런 상품에 앱이 다른 번호를 발급하면 쓰지 않는 라벨을 한 장 더 붙여야
/// 한다 — 붙이지 않으면 찍어도 안 잡히고, 붙이면 한 상품에 코드가 둘이 된다.
///
/// 직접 입력을 열면 자동 발급만 되던 시절에는 있을 수 없던 상태가 생긴다. 여기서
/// 막는 것은 전부 그것들이다: 접미사·접두사 충돌, 다른 상품과 같은 코드, 한 상품
/// 안에서 박스와 낱개가 같은 코드.
/// </summary>
public class ManualBarcodeTests
{
    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Existing { get; } = new();
        public List<Product> Saved { get; } = new();

        public Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, EntityStatus? statusFilter)
            => Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>());
        public Task<Product?> GetByIdAsync(string productId)
            => Task.FromResult(Existing.FirstOrDefault(p => p.ProductId == productId));

        // 실제 SQL과 같은 규칙: 세 칸을 모두 보고, 저장돼 있지 않은 "내부 바코드 + -EA"도 본다.
        public Task<bool> BarcodeInUseAsync(string code, string? excludeProductId = null)
            => Task.FromResult(Existing.Any(p =>
                p.ProductId != excludeProductId
                && (p.Barcode == code || p.InternalBarcode == code || p.UnitBarcode == code)));

        public Task InsertAsync(Product product) { Saved.Add(product); return Task.CompletedTask; }
        public Task UpdateAsync(Product product) { Saved.Add(product); return Task.CompletedTask; }
        public Task<bool> UpdateWithUnitsPerBoxChangeAsync(Product product, int previousUnitsPerBox, string userId)
            => Task.FromResult(true);
        public Task DeactivateAsync(string productId) => Task.CompletedTask;
        public Task<ProductPhoto?> GetPhotoAsync(string productId) => Task.FromResult<ProductPhoto?>(null);
        public Task SavePhotoAsync(string productId, byte[]? photo, long? updatedAt) => Task.CompletedTask;
    }

    private sealed class FakeBarcodeSequenceRepository : IInternalBarcodeSequenceRepository
    {
        public int Calls { get; private set; }

        public Task<string> GetNextInternalBarcodeAsync()
        {
            Calls++;
            return Task.FromResult($"{Product.GeneratedBarcodePrefix}{Calls:D8}");
        }
    }

    private readonly FakeProductRepository _repository = new();
    private readonly FakeBarcodeSequenceRepository _sequence = new();

    private ProductService Service() => new(_repository, _sequence);

    private static Product Boxed() => new()
    {
        ProductId = string.Empty,
        CreatedAt = 0,
        ProductName = "Amoxil 500mg Capsule",
        GenericName = "Amoxicillin",
        DosageForm = DosageForm.Capsule,
        Unit = "Capsule",
        Manufacturer = "Maker A",
        UnitsPerBox = 30,
        CostPrice = 3.00m,
        SellingPrice = 9.00m,
        SafetyStockLevel = 10,
        Status = EntityStatus.Active
    };

    private async Task<ProductSaveResult> SaveAsync(Product product) =>
        await Service().SaveProductAsync(product, isNewProduct: true, userId: "user-1");

    // ── 내부 바코드를 직접 적는 경우 ────────────────────────────────────

    /// <summary>적어 넣은 값은 그대로 저장된다. 자동 발급이 그 위에 덮이면 안 된다.</summary>
    [Fact]
    public async Task TypedInternalBarcode_IsKept_AndNothingIsGenerated()
    {
        var product = Boxed();
        product.InternalBarcode = "8801234567890";

        var result = await SaveAsync(product);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("8801234567890", Assert.Single(_repository.Saved).InternalBarcode);
        Assert.Equal(0, _sequence.Calls);
    }

    /// <summary>비워 두면 종전대로 발급된다.</summary>
    [Fact]
    public async Task EmptyInternalBarcode_StillGeneratesOne()
    {
        var result = await SaveAsync(Boxed());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("INT-00000001", Assert.Single(_repository.Saved).InternalBarcode);
    }

    /// <summary>
    /// -EA로 끝나는 내부 바코드는 막는다. 낱개 코드가 "내부 바코드 + -EA"이므로,
    /// 이런 값을 허용하면 어떤 상품의 낱개 코드가 다른 상품의 박스 코드와 같아진다.
    /// </summary>
    [Fact]
    public async Task InternalBarcodeEndingInEa_IsRefused()
    {
        var product = Boxed();
        product.InternalBarcode = "8801234567890-EA";

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Contains("-EA", result.Message);
    }

    /// <summary>
    /// INT- 번호대는 채번에만 맡긴다. 사람이 앞질러 적어 두면 채번이 언젠가 그 번호에
    /// 닿아 저장이 거절되는데, 그때 원인은 몇 달 전에 만들어진 셈이라 현장에서 알 수 없다.
    /// </summary>
    [Fact]
    public async Task InternalBarcodeUsingTheGeneratedPrefix_IsRefused()
    {
        var product = Boxed();
        product.InternalBarcode = "INT-00000200";

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Contains("INT-", result.Message);
    }

    /// <summary>
    /// 다른 상품이 <b>제조사</b> 바코드로 쓰고 있는 값도 막는다. 스캐너는 그 코드가
    /// 어느 칸에 들어 있는지 모르고 찍으므로, 칸별로 따로 보면 찍었을 때 어느 상품이
    /// 잡히는지 알 수 없는 상태가 만들어진다.
    /// </summary>
    [Fact]
    public async Task InternalBarcodeTakenByAnotherProductsManufacturerBarcode_IsRefused()
    {
        _repository.Existing.Add(new Product
        {
            ProductId = "other",
            ProductName = "Other",
            Unit = "Box",
            Barcode = "8801234567890",
            CostPrice = 1m,
            SellingPrice = 2m,
            SafetyStockLevel = 0,
            Status = EntityStatus.Active,
            CreatedAt = 0
        });

        var product = Boxed();
        product.InternalBarcode = "8801234567890";

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.Message);
    }

    // ── 낱개에 인쇄된 바코드 ────────────────────────────────────────────

    /// <summary>적어 넣은 낱개 코드가 그대로 낱개 바코드가 된다.</summary>
    [Fact]
    public async Task TypedUnitBarcode_BecomesTheUnitBarcode()
    {
        var product = Boxed();
        product.Barcode = "8801111111111";
        product.UnitBarcodeOverride = "8802222222222";

        var result = await SaveAsync(product);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("8802222222222", Assert.Single(_repository.Saved).UnitBarcode);
    }

    /// <summary>
    /// 낱개 코드를 적어 넣었으면 낱개를 가리킬 수단이 이미 있으므로 내부 바코드를
    /// 발급하지 않는다. 발급하면 쓰지 않는 번호가 상품마다 하나씩 쌓인다.
    /// </summary>
    [Fact]
    public async Task TypedUnitBarcode_MakesTheGeneratedInternalBarcodeUnnecessary()
    {
        var product = Boxed();
        product.Barcode = "8801111111111";
        product.UnitBarcodeOverride = "8802222222222";

        await SaveAsync(product);

        Assert.Equal(0, _sequence.Calls);
        Assert.Null(Assert.Single(_repository.Saved).InternalBarcode);
    }

    /// <summary>적지 않으면 종전대로 내부 바코드 + "-EA"다.</summary>
    [Fact]
    public async Task WithoutATypedUnitBarcode_TheSuffixedInternalBarcodeIsStillUsed()
    {
        var result = await SaveAsync(Boxed());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("INT-00000001-EA", Assert.Single(_repository.Saved).UnitBarcode);
    }

    /// <summary>
    /// 소분하지 않는 상품에 적힌 낱개 코드는 버린다 — 낱개가와 같은 규칙이다.
    /// 남겨 두면 소분을 껐는데도 스캔되는 코드가 남는다.
    /// </summary>
    [Fact]
    public async Task UnitBarcodeOnAProductThatIsNotSoldLoose_IsDiscarded()
    {
        var product = Boxed();
        product.UnitsPerBox = 1;
        product.UnitBarcodeOverride = "8802222222222";

        var result = await SaveAsync(product);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(Assert.Single(_repository.Saved).UnitBarcodeOverride);
    }

    /// <summary>
    /// 박스와 낱개가 같은 코드면 한 번 찍어서 어느 쪽을 파는지 정할 수 없다.
    /// 같은 상품 안의 일이라 중복 조회로는 걸리지 않으므로 따로 본다.
    /// </summary>
    [Fact]
    public async Task UnitBarcodeEqualToItsOwnBoxBarcode_IsRefused()
    {
        var product = Boxed();
        product.Barcode = "8801111111111";
        product.UnitBarcodeOverride = "8801111111111";

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Contains("must differ", result.Message);
    }

    /// <summary>
    /// 다른 상품이 라벨로 뽑아 쓰는 "내부 바코드 + -EA"와 같은 값도 막는다.
    /// 그 값은 DB 어느 칸에도 통째로 들어 있지 않아 놓치기 쉽다.
    /// </summary>
    [Fact]
    public async Task UnitBarcodeClashingWithAnotherProductsGeneratedLooseCode_IsRefused()
    {
        _repository.Existing.Add(new Product
        {
            ProductId = "other",
            ProductName = "Other",
            Unit = "Tablet",
            UnitsPerBox = 10,
            InternalBarcode = "INT-00000007",
            CostPrice = 1m,
            SellingPrice = 2m,
            SafetyStockLevel = 0,
            Status = EntityStatus.Active,
            CreatedAt = 0
        });

        var product = Boxed();
        product.Barcode = "8801111111111";
        product.UnitBarcodeOverride = "INT-00000007-EA";

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Contains("already registered", result.Message);
    }

    /// <summary>
    /// 라벨로 인쇄할 수 없는 글자는 막는다. Code 128-B가 담지 못하는 값이 들어가면
    /// 저장은 되고 라벨을 뽑을 때에야 거절되는데, 그때는 상품이 이미 등록된 뒤다.
    /// </summary>
    [Fact]
    public async Task BarcodeThatCannotBePrinted_IsRefused()
    {
        var product = Boxed();
        product.InternalBarcode = "아목시실린";

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot be printed", result.Message);
    }
}
