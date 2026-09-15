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
        UnitsPerBox = 1,
        CostPrice = 3.00m,
        SellingPrice = 4.53m,
        SafetyStockLevel = 10,
        Status = EntityStatus.Active
    };

    private static async Task<ProductSaveResult> SaveAsync(Product product) =>
        await CreateService().SaveProductAsync(product, isNewProduct: true);

    [Fact]
    public async Task CompleteMedicine_Saves()
    {
        var result = await SaveAsync(Medicine());
        Assert.True(result.IsSuccess, result.Message);
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
}
