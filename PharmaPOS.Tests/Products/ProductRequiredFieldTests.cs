using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 상품 저장의 필수 항목: 상품명, 제형, 성분명(약일 때), 단위, 판매가.
///
/// 성분명이 약에만 필수인 것이 이 규칙의 핵심이다. 항생제 판별이 성분명으로 되므로
/// 약에서 비면 복약안내가 조용히 빠진다. 그런데 붕대·채혈관·폐기물 봉투에는 성분명이
/// 없다 — 실제 재고의 1/4이 그런 것들이라, 무조건 요구하면 그 상품들은 수정할 때마다
/// 저장이 막힌다. 제형 Other가 "약이 아니다"라는 표시다.
/// </summary>
public class ProductRequiredFieldTests
{
    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Saved { get; } = new();

        public Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, EntityStatus? statusFilter)
            => Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>());
        public Task<Product?> GetByIdAsync(string productId) => Task.FromResult<Product?>(null);
        public Task<bool> BarcodeExistsAsync(string barcode, string? excludeProductId = null)
            => Task.FromResult(false);
        public Task<bool> InternalBarcodeExistsAsync(string internalBarcode, string? excludeProductId = null)
            => Task.FromResult(false);
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
        public Task<string> GetNextInternalBarcodeAsync() => Task.FromResult("INT-00000001");
    }

    private static ProductService CreateService() =>
        new(new FakeProductRepository(), new FakeBarcodeSequenceRepository());

    private static Product Medicine() => new()
    {
        ProductId = string.Empty,
        CreatedAt = 0,
        ProductName = "Amoxil 500mg Capsule",
        GenericName = "Amoxicillin",
        DosageForm = DosageForm.Capsule,
        Unit = "Capsule",
        Manufacturer = "Maker A",
        UnitsPerBox = 1,
        CostPrice = 3.00m,
        SellingPrice = 4.53m,
        SafetyStockLevel = 10,
        Status = EntityStatus.Active
    };

    private static async Task<ProductSaveResult> SaveAsync(Product product) =>
        await CreateService().SaveProductAsync(product, isNewProduct: true, userId: "user-1");

    [Fact]
    public async Task CompleteMedicine_Saves()
    {
        var result = await SaveAsync(Medicine());
        Assert.True(result.IsSuccess, result.Message);
    }

    /// <summary>
    /// 제조사는 상품을 가르는 값이라 필수다. 약국에는 이름이 같고 만든 곳이 다른
    /// 상품이 흔한데, 비어 있으면 임포트가 둘을 한 상품으로 보고 재고와 가격을 섞는다.
    /// </summary>
    [Fact]
    public async Task MissingManufacturer_IsRefused()
    {
        var product = Medicine();
        product.Manufacturer = "   ";

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Equal("Please enter the manufacturer.", result.Message);
    }

    [Fact]
    public async Task MissingDosageForm_IsRefused()
    {
        var product = Medicine();
        product.DosageForm = null;

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Equal("Please select the dosage form.", result.Message);
    }

    [Fact]
    public async Task Medicine_WithoutGenericName_IsRefused()
    {
        var product = Medicine();
        product.GenericName = "   ";

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("Please enter the generic name.", result.Message);
    }

    /// <summary>붕대에는 성분명이 없다. 제형 Other가 그 사실을 말하고, 그러면 비워 둘 수 있다.</summary>
    [Fact]
    public async Task NonMedicine_SavesWithoutGenericName()
    {
        var product = Medicine();
        product.ProductName = "3M Micropore Paper Tape 1in";
        product.GenericName = null;
        product.DosageForm = DosageForm.Other;
        product.Unit = "Roll";

        var result = await SaveAsync(product);

        Assert.True(result.IsSuccess, result.Message);
    }

    /// <summary>원가는 선택이다. 모르면 0으로 두고, 그때는 저가 판매 경고만 뜨지 않는다.</summary>
    [Fact]
    public async Task ZeroCostPrice_IsAllowedAndDoesNotTriggerTheLowPriceWarning()
    {
        var product = Medicine();
        product.CostPrice = 0;

        var result = await SaveAsync(product);

        Assert.True(result.IsSuccess, result.Message);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task NegativeCostPrice_IsRefused()
    {
        var product = Medicine();
        product.CostPrice = -1;

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Equal("Cost price cannot be negative.", result.Message);
    }

    [Fact]
    public async Task ZeroSafetyStock_IsAllowed()
    {
        var product = Medicine();
        product.SafetyStockLevel = 0;

        var result = await SaveAsync(product);

        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task SellingPrice_StaysRequired()
    {
        var product = Medicine();
        product.SellingPrice = 0;

        var result = await SaveAsync(product);

        Assert.False(result.IsSuccess);
        Assert.Equal("Selling price must be greater than zero.", result.Message);
    }

    /// <summary>
    /// 바코드는 앞뒤 공백 없이 저장돼야 한다. 붙여넣기나 시트에서 공백이 따라오면
    /// 검색은 되는데 스캔 즉시 담기는 안 되는 상태가 된다 — 내부 바코드는 앱이 만들어
    /// 늘 깨끗하고 유통사 바코드만 그렇게 되니, "외부 바코드만 안 된다"로 보인다.
    /// </summary>
    [Fact]
    public async Task Barcode_IsStoredWithoutSurroundingWhitespace()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository, new FakeBarcodeSequenceRepository());
        var product = Medicine();
        product.Barcode = "  8806433062927 ";

        var result = await service.SaveProductAsync(product, isNewProduct: true, userId: "user-1");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("8806433062927", Assert.Single(repository.Saved).Barcode);
    }

    [Fact]
    public async Task WhitespaceOnlyBarcode_IsStoredAsNull()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository, new FakeBarcodeSequenceRepository());
        var product = Medicine();
        product.Barcode = "   ";

        var result = await service.SaveProductAsync(product, isNewProduct: true, userId: "user-1");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(Assert.Single(repository.Saved).Barcode);
    }
}
