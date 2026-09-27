using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 낱개 판매는 세 가지가 한 묶음이다 — 켬, 박스당 개수, 낱개가.
///
/// 전에는 낱개가를 비워 둘 수 있었고, 그러면 앱이 계산대에서 박스가를 개수로 나눠
/// 값을 지어냈다. 그 값은 원가에 가까운 숫자이지 파는 가격이 아니다 — 박스를 헐어
/// 낱개로 파는 데에는 마진이 붙고, 얼마를 붙일지는 약국이 정한다. 화면 어디에도
/// "이 가격은 앱이 지어낸 것"이라는 표시가 없어서, 계산대에서는 약국이 정한 가격과
/// 구별되지 않았다.
///
/// 그래서 지어내는 쪽을 없애고, 값이 없는 상태 자체를 만들 수 없게 했다.
/// <b>박스당 개수와 낱개 판매는 별개 값이다</b> — 한 박스에 30정이 들었다는 것과
/// 그 박스를 헐어 판다는 것은 다른 사실이고, 앞은 제조사가 뒤는 약국이 정한다.
/// </summary>
public class DerivedLoosePriceTests
{
    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Saved { get; } = new();

        public Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, EntityStatus? statusFilter)
            => Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>());
        public Task<Product?> GetByIdAsync(string productId) => Task.FromResult<Product?>(null);
        public Task<bool> BarcodeInUseAsync(string code, string? excludeProductId = null)
            => Task.FromResult(false);
        public Task InsertAsync(Product product) { Saved.Add(product); return Task.CompletedTask; }
        public Task UpdateAsync(Product product) { Saved.Add(product); return Task.CompletedTask; }
        public Task<bool> UpdateWithUnitsPerBoxChangeAsync(Product p, int previous, string userId)
            => Task.FromResult(true);
        public Task DeactivateAsync(string productId) => Task.CompletedTask;
        public Task<ProductPhoto?> GetPhotoAsync(string productId) => Task.FromResult<ProductPhoto?>(null);
        public Task SavePhotoAsync(string productId, byte[]? photo, long? updatedAt) => Task.CompletedTask;
    }

    private sealed class FakeBarcodeSequenceRepository : IInternalBarcodeSequenceRepository
    {
        public Task<string> GetNextInternalBarcodeAsync() => Task.FromResult("INT-00000001");
    }

    private readonly FakeProductRepository _repository = new();

    private async Task<ProductSaveResult> SaveAsync(Product product) =>
        await new ProductService(_repository, new FakeBarcodeSequenceRepository())
            .SaveProductAsync(product, isNewProduct: true, userId: "user-1");

    private static Product Product30() => new()
    {
        ProductId = string.Empty,
        CreatedAt = 0,
        ProductName = "Amoxil 500mg Capsule",
        GenericName = "Amoxicillin",
        DosageForm = DosageForm.Capsule,
        Unit = "Capsule",
        Manufacturer = "Maker A",
        UnitsPerBox = 30,
        CostPrice = 7m,
        SellingPrice = 10m,
        SafetyStockLevel = 0,
        Status = EntityStatus.Active
    };

    // ── 값을 지어내지 않는다 ────────────────────────────────────────────────

    /// <summary>낱개가는 저장된 값이 전부다. 없으면 없는 것이다.</summary>
    [Fact]
    public void ThereIsNoPriceUntilSomebodySetsOne()
    {
        var product = Product30();

        Assert.Null(product.UnitSellingPrice);
    }

    /// <summary>
    /// 한 박스에 30정이 들었다고 낱개로 파는 것은 아니다. 박스째로만 파는 상품도 흔하다.
    /// </summary>
    [Fact]
    public void ABoxOfThirtyIsNotBySSellingLoose()
    {
        var product = Product30();

        Assert.True(product.IsBoxedProduct);      // 포장: 한 박스 30정
        Assert.False(product.SellsLooseUnits);    // 판매: 박스째로만
        Assert.Null(product.UnitBarcode);         // 찍을 일이 없으니 만들지 않는다
    }

    // ── 저장 규칙 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SellingLooseWithoutAPrice_IsRefused()
    {
        var product = Product30();
        product.SellsLooseUnits = true;

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Contains("price of one loose unit", result.Message);
    }

    [Fact]
    public async Task SellingLooseWithoutABoxCount_IsRefused()
    {
        var product = Product30();
        product.UnitsPerBox = 1;
        product.SellsLooseUnits = true;
        product.UnitSellingPrice = 0.5m;

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Contains("how many are in one box", result.Message);
    }

    [Fact]
    public async Task SellingLooseWithBothOfThem_Saves()
    {
        var product = Product30();
        product.SellsLooseUnits = true;
        product.UnitSellingPrice = 0.5m;

        var result = await SaveAsync(product);

        Assert.True(result.IsSuccess, result.Message);

        var saved = Assert.Single(_repository.Saved);
        Assert.True(saved.SellsLooseUnits);
        Assert.Equal(0.5m, saved.UnitSellingPrice);
        Assert.Equal("INT-00000001-EA", saved.UnitBarcode);   // 낱개 바코드가 생긴다
    }

    /// <summary>박스째로만 파는 상품은 박스 구성만 남고 낱개 값은 버려진다.</summary>
    [Fact]
    public async Task NotSellingLoose_KeepsTheBoxCountAndDropsThePrice()
    {
        var product = Product30();
        product.SellsLooseUnits = false;
        product.UnitSellingPrice = 0.5m;

        var result = await SaveAsync(product);

        Assert.True(result.IsSuccess, result.Message);

        var saved = Assert.Single(_repository.Saved);
        Assert.Equal(30, saved.UnitsPerBox);     // 포장은 사실이라 남는다
        Assert.Null(saved.UnitSellingPrice);
        Assert.Null(saved.UnitBarcode);
    }

    /// <summary>낱개가는 네 자리까지. 리엘로 정해진 가격을 담기 위한 것이다.</summary>
    [Theory]
    [InlineData(0.125)]      // 500리엘
    [InlineData(0.0025)]     // 10리엘
    [InlineData(0.5)]
    public async Task AFourDecimalLoosePrice_IsAccepted(decimal price)
    {
        var product = Product30();
        product.SellsLooseUnits = true;
        product.UnitSellingPrice = price;

        Assert.True((await SaveAsync(product)).IsSuccess);
    }

    [Fact]
    public async Task AFiveDecimalLoosePrice_IsRefused()
    {
        var product = Product30();
        product.SellsLooseUnits = true;
        product.UnitSellingPrice = 0.45333m;

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Contains("4 decimal places", result.Message);
    }

    // ── 원가는 여전히 나눈다 ────────────────────────────────────────────────

    /// <summary>
    /// 원가는 계산해도 된다. 값을 지어내는 것이 아니라 <b>이미 치른 돈</b>을 개수로
    /// 배분하는 것이라, 낱개로 헐어 판다고 매입 단가가 달라지지 않는다.
    /// 나눈 값은 낱개가와 같은 자리에서 끊는다.
    /// </summary>
    [Fact]
    public void UnitCost_IsStillDividedAndStopsAtFourDecimals()
    {
        Assert.Equal(0.2333m, Product30().UnitCostPrice);   // 7 / 30
    }

    // ── 계산대가 집는 단가 ──────────────────────────────────────────────────
    //
    // 이 판단이 계산대의 가격칸을 채우고 재고 차감 단위를 정한다. 화면 안에 두었을 때
    // 두 번 틀렸다 — 한 번은 낱개 판매를 끈 상품까지 낱개가(null)를 집어서 가격칸이
    // 빈 채로 떴고, 한 번은 박스 구분이 없는 상품을 박스로 세려 했다.
    // 둘 다 오류 없이 지나가고, 계산대에서는 "담기가 안 된다"로만 보인다.

    /// <summary>낱개로 팔지 않는 상품은 어느 단위로 고르든 판매가다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AProductThatIsNotSoldLoose_AlwaysUsesTheSellingPrice(bool sellingLoose)
    {
        var product = Product30();      // 한 박스 30정, 낱개 판매 꺼짐

        Assert.Equal(10m, product.PriceForSaleUnit(sellingLoose));
    }

    /// <summary>낱개로 고르면 낱개가, 박스로 고르면 박스가.</summary>
    [Fact]
    public void AProductSoldLoose_UsesThePriceForTheChosenUnit()
    {
        var product = Product30();
        product.SellsLooseUnits = true;
        product.UnitSellingPrice = 0.5m;

        Assert.Equal(0.5m, product.PriceForSaleUnit(sellingLoose: true));
        Assert.Equal(10m, product.PriceForSaleUnit(sellingLoose: false));
    }

    /// <summary>박스 구분이 없는 상품에는 낱개도 박스도 없다. 언제나 판매가다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AProductWithNoBoxSplit_AlwaysUsesTheSellingPrice(bool sellingLoose)
    {
        var product = Product30();
        product.UnitsPerBox = 1;

        Assert.Equal(10m, product.PriceForSaleUnit(sellingLoose));
        Assert.False(product.CountsInBoxes(sellingLoose));   // 박스로 셀 것이 없다
    }

    /// <summary>
    /// 수량을 박스로 세는지. 박스로 세면 한 줄이 UnitsPerBox만큼 재고를 가져간다.
    /// </summary>
    [Fact]
    public void CountsInBoxes_TakesBothPackagingAndTheChosenUnit()
    {
        var boxOnly = Product30();                       // 30정 포장, 낱개 판매 꺼짐
        Assert.True(boxOnly.CountsInBoxes(sellingLoose: false));
        Assert.True(boxOnly.CountsInBoxes(sellingLoose: true));   // 낱개로 팔 수 없다

        var loose = Product30();
        loose.SellsLooseUnits = true;
        loose.UnitSellingPrice = 0.5m;
        Assert.True(loose.CountsInBoxes(sellingLoose: false));
        Assert.False(loose.CountsInBoxes(sellingLoose: true));
    }

    // ── 줄 금액 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 단가는 네 자리를 가질 수 있어도 <b>금액</b>은 통화 단위여야 한다. 영수증은 이
    /// 값을 0.00으로 찍으므로, 끊지 않으면 원장과 종이가 어긋난다.
    /// </summary>
    [Fact]
    public void LineTotal_IsRoundedToCents()
    {
        Assert.Equal(2.33m, Line(unitPrice: 0.3333m, quantity: 7).LineTotal);
        Assert.Equal(1.67m, Line(unitPrice: 0.3333m, quantity: 5).LineTotal);
        Assert.Equal(9.06m, Line(unitPrice: 4.53m, quantity: 2).LineTotal);
    }

    private static SaleLineItem Line(decimal unitPrice, int quantity) => new()
    {
        ProductId = "p1",
        ProductName = "Amoxil 500mg Capsule",
        InventoryId = "inv-1",
        BatchNumber = "B2401",
        ExpiryDate = 0,
        Quantity = quantity,
        UnitPrice = unitPrice,
        CostPrice = 0m
    };
}
