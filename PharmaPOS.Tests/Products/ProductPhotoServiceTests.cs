using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 상품 사진 저장 규칙.
///
/// 여기를 테스트하는 이유: 사진은 DB에 그대로 들어가고, 그 DB를 통째로 복사하는 것이 백업이다.
/// 크기 제한이 조용히 풀리면 상품 300개에 DB가 1GB가 되고 백업부터 무너진다.
/// </summary>
public class ProductPhotoServiceTests
{
    private const string ProductId = "product-1";

    private sealed class FakePhotoEncoder : IPhotoEncoder
    {
        public bool ReturnsNull { get; set; }
        public int? LastMaxEdge { get; private set; }
        public byte[] Encoded { get; set; } = [1, 2, 3];

        public byte[]? Encode(byte[] source, int maxEdgePixels)
        {
            LastMaxEdge = maxEdgePixels;
            return ReturnsNull ? null : Encoded;
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public byte[]? SavedPhoto { get; private set; }
        public long? SavedUpdatedAt { get; private set; }
        public bool SaveWasCalled { get; private set; }
        public bool ShouldThrow { get; set; }

        public Task SavePhotoAsync(string productId, byte[]? photo, long? updatedAt)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("db down");
            }

            SaveWasCalled = true;
            SavedPhoto = photo;
            SavedUpdatedAt = updatedAt;
            return Task.CompletedTask;
        }

        public Task<ProductPhoto?> GetPhotoAsync(string productId)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("db down");
            }

            return Task.FromResult<ProductPhoto?>(
                SavedPhoto is null ? null : new ProductPhoto(SavedPhoto, SavedUpdatedAt ?? 0));
        }

        // 이 테스트가 쓰지 않는 나머지.
        public Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, EntityStatus? statusFilter)
            => Task.FromResult<IReadOnlyList<Product>>([]);
        public Task<Product?> GetByIdAsync(string productId) => Task.FromResult<Product?>(null);
        public Task<bool> BarcodeExistsAsync(string barcode, string? excludeProductId = null)
            => Task.FromResult(false);
        public Task<bool> InternalBarcodeExistsAsync(string internalBarcode, string? excludeProductId = null)
            => Task.FromResult(false);
        public Task InsertAsync(Product product) => Task.CompletedTask;
        public Task UpdateAsync(Product product) => Task.CompletedTask;
        public Task DeactivateAsync(string productId) => Task.CompletedTask;
    }

    private static (ProductPhotoService Service, FakeProductRepository Repo, FakePhotoEncoder Encoder) Build()
    {
        var repo = new FakeProductRepository();
        var encoder = new FakePhotoEncoder();
        return (new ProductPhotoService(repo, encoder), repo, encoder);
    }

    /// <summary>저장한 사진에는 갱신 시각이 함께 남아야 한다. 화면이 그 값으로 날짜를 찍는다.</summary>
    [Fact]
    public async Task SaveAsync_StoresEncodedBytesWithAnUpdatedTime()
    {
        var (service, repo, _) = Build();

        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = await service.SaveAsync(ProductId, [9, 9, 9, 9]);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], repo.SavedPhoto);
        Assert.NotNull(repo.SavedUpdatedAt);
        Assert.True(repo.SavedUpdatedAt >= before);
        Assert.Equal(repo.SavedUpdatedAt, result.Photo!.UpdatedAt);
    }

    /// <summary>원본이 아니라 줄인 결과가 저장돼야 한다. 인코더에 넘기는 한도도 함께 확인한다.</summary>
    [Fact]
    public async Task SaveAsync_ShrinksBeforeStoring()
    {
        var (service, repo, encoder) = Build();

        await service.SaveAsync(ProductId, [9, 9, 9, 9]);

        Assert.Equal(ProductPhotoService.MaxEdgePixels, encoder.LastMaxEdge);
        Assert.NotEqual(4, repo.SavedPhoto!.Length);
    }

    /// <summary>
    /// 큰 파일은 이미지로 읽어 보기 전에 거른다. 수십 MB짜리를 디코딩하다
    /// 메모리를 다 쓰면 화면이 통째로 죽는다.
    /// </summary>
    [Fact]
    public async Task SaveAsync_RejectsOversizedFilesWithoutDecoding()
    {
        var (service, repo, encoder) = Build();

        var tooBig = new byte[ProductPhotoService.MaxSourceBytes + 1];

        var result = await service.SaveAsync(ProductId, tooBig);

        Assert.False(result.IsSuccess);
        Assert.Contains("too large", result.Message);
        Assert.Null(encoder.LastMaxEdge);
        Assert.False(repo.SaveWasCalled);
    }

    [Fact]
    public async Task SaveAsync_RejectsEmptyFiles()
    {
        var (service, repo, _) = Build();

        var result = await service.SaveAsync(ProductId, []);

        Assert.False(result.IsSuccess);
        Assert.False(repo.SaveWasCalled);
    }

    /// <summary>이미지가 아닌 파일은 저장까지 가지 않는다.</summary>
    [Fact]
    public async Task SaveAsync_ReportsWhenTheFileIsNotAnImage()
    {
        var (service, repo, encoder) = Build();
        encoder.ReturnsNull = true;

        var result = await service.SaveAsync(ProductId, [9, 9]);

        Assert.False(result.IsSuccess);
        Assert.Contains("could not be read", result.Message);
        Assert.False(repo.SaveWasCalled);
    }

    /// <summary>지우면 사진과 시각이 함께 비어야 한다. 시각만 남으면 "없는 사진의 갱신일"이 된다.</summary>
    [Fact]
    public async Task RemoveAsync_ClearsBothThePhotoAndItsTimestamp()
    {
        var (service, repo, _) = Build();
        await service.SaveAsync(ProductId, [9, 9]);

        var result = await service.RemoveAsync(ProductId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Photo);
        Assert.Null(repo.SavedPhoto);
        Assert.Null(repo.SavedUpdatedAt);
    }

    /// <summary>사진을 못 읽는다고 상세 화면이 열리지 않으면 안 된다.</summary>
    [Fact]
    public async Task GetAsync_ReturnsNullWhenTheDatabaseFails()
    {
        var (service, repo, _) = Build();
        repo.ShouldThrow = true;

        Assert.Null(await service.GetAsync(ProductId));
    }

    /// <summary>저장 실패는 예외가 아니라 메시지로 돌아온다.</summary>
    [Fact]
    public async Task SaveAsync_ReportsStorageFailureInsteadOfThrowing()
    {
        var (service, repo, _) = Build();
        repo.ShouldThrow = true;

        var result = await service.SaveAsync(ProductId, [9, 9]);

        Assert.False(result.IsSuccess);
        Assert.Contains("could not be saved", result.Message);
    }
}
