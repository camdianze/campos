using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 낱개 판매가는 계산대에서 실제로 주고받는 돈이라 소수점 두 자리를 넘길 수 없다.
/// 세 자리 이상이 저장되면 영수증과 판매 이력에 통화로 낼 수 없는 금액이 찍히고,
/// 상품 화면과 임포트가 같은 ProductService를 지나므로 규칙은 여기 한 곳에만 있으면 된다.
/// </summary>
public class ProductPricePrecisionTests
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

    private static (ProductService Service, FakeProductRepository Repository) CreateService()
    {
        var repository = new FakeProductRepository();
        return (new ProductService(repository, new FakeBarcodeSequenceRepository()), repository);
    }

    private static Product CreateProduct(decimal? unitSellingPrice) => new()
    {
        ProductId = string.Empty,
        CreatedAt = 0,
        ProductName = "Amoxil 500mg Capsule",
        GenericName = "Amoxicillin",
        Unit = "Capsule",
        UnitsPerBox = 100,
        CostPrice = 3.00m,
        SellingPrice = 4.53m,
        UnitSellingPrice = unitSellingPrice,
        SafetyStockLevel = 10,
        Status = EntityStatus.Active
    };

    [Theory]
    [InlineData(0.045)]      // 박스가를 낱개 수로 나눈 값이 그대로 들어온 경우
    [InlineData(0.4533)]
    [InlineData(1.001)]
    public async Task SaveProductAsync_RejectsMoreThanTwoDecimals(decimal unitSellingPrice)
    {
        var (service, repository) = CreateService();

        var result = await service.SaveProductAsync(CreateProduct(unitSellingPrice), isNewProduct: true);

        Assert.False(result.IsSuccess);
        Assert.Equal("Loose unit price can have at most 2 decimal places.", result.Message);
        Assert.Empty(repository.Saved);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(1.5)]
    [InlineData(12)]
    [InlineData(0.50)]       // 끝자리 0은 세 자리가 아니다
    public async Task SaveProductAsync_AcceptsTwoDecimalsOrFewer(decimal unitSellingPrice)
    {
        var (service, _) = CreateService();

        var result = await service.SaveProductAsync(CreateProduct(unitSellingPrice), isNewProduct: true);

        Assert.True(result.IsSuccess, result.Message);
    }

    /// <summary>낱개가는 선택 항목이다. 비워 둔 상품이 이 규칙에 걸리면 안 된다.</summary>
    [Fact]
    public async Task SaveProductAsync_AllowsNoLooseUnitPrice()
    {
        var (service, _) = CreateService();

        var result = await service.SaveProductAsync(CreateProduct(null), isNewProduct: true);

        Assert.True(result.IsSuccess, result.Message);
    }
}
