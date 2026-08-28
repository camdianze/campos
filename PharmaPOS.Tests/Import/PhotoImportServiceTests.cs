using PharmaPOS.Application.Import;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Import;

/// <summary>
/// 사진 폴더를 상품에 붙이는 규칙.
///
/// 여기를 테스트하는 이유: 매칭이 어긋나면 <b>엉뚱한 약에 다른 약 사진이 붙는다.</b>
/// 화면에서는 그럴듯해 보이고, 계산대에서 사진을 보고 약을 고르는 순간에야 드러난다.
/// </summary>
public class PhotoImportServiceTests
{
    private sealed class FakePhotoSource : IImportPhotoSource
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        public List<string> ReadFiles { get; } = new();
        public HashSet<string> Unreadable { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string fileName, byte[]? bytes = null) => _files[fileName] = bytes ?? [1, 2, 3];

        public IReadOnlyList<string> FileNames => _files.Keys.ToList();

        public byte[] Read(string fileName)
        {
            if (Unreadable.Contains(fileName))
            {
                throw new IOException("locked");
            }

            ReadFiles.Add(fileName);
            return _files[fileName];
        }
    }

    private sealed class FakePhotoService : IProductPhotoService
    {
        public Dictionary<string, ProductPhoto> Saved { get; } = new(StringComparer.Ordinal);
        public HashSet<string> FailingProductIds { get; } = new(StringComparer.Ordinal);

        public Task<ProductPhoto?> GetAsync(string productId) =>
            Task.FromResult(Saved.TryGetValue(productId, out var photo) ? photo : null);

        public Task<ProductPhotoResult> SaveAsync(string productId, byte[] fileBytes)
        {
            if (FailingProductIds.Contains(productId))
            {
                return Task.FromResult(ProductPhotoResult.Failure("The photo could not be saved."));
            }

            var photo = new ProductPhoto(fileBytes, 123);
            Saved[productId] = photo;
            return Task.FromResult(ProductPhotoResult.Success(photo));
        }

        public Task<ProductPhotoResult> RemoveAsync(string productId)
        {
            Saved.Remove(productId);
            return Task.FromResult(ProductPhotoResult.Success(null));
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = new();

        public Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, EntityStatus? statusFilter)
            => Task.FromResult<IReadOnlyList<Product>>(Products.ToList());

        public Task<Product?> GetByIdAsync(string productId)
            => Task.FromResult(Products.FirstOrDefault(p => p.ProductId == productId));
        public Task<bool> BarcodeExistsAsync(string barcode, string? excludeProductId = null)
            => Task.FromResult(false);
        public Task<bool> InternalBarcodeExistsAsync(string internalBarcode, string? excludeProductId = null)
            => Task.FromResult(false);
        public Task InsertAsync(Product product) => Task.CompletedTask;
        public Task UpdateAsync(Product product) => Task.CompletedTask;
        public Task DeactivateAsync(string productId) => Task.CompletedTask;
        public Task<ProductPhoto?> GetPhotoAsync(string productId) => Task.FromResult<ProductPhoto?>(null);
        public Task SavePhotoAsync(string productId, byte[]? photo, long? updatedAt) => Task.CompletedTask;
    }

    private static Product Make(
        string id, string name, string? barcode = null, string? internalBarcode = null, int unitsPerBox = 1) => new()
    {
        ProductId = id,
        ProductName = name,
        Barcode = barcode,
        InternalBarcode = internalBarcode,
        Unit = "Tablet",
        CostPrice = 100,
        SellingPrice = 200,
        SafetyStockLevel = 1,
        UnitsPerBox = unitsPerBox,
        Status = EntityStatus.Active,
        CreatedAt = 0
    };

    private static (PhotoImportService Service, FakeProductRepository Repo, FakePhotoService Photos) Build()
    {
        var repo = new FakeProductRepository();
        var photos = new FakePhotoService();
        return (new PhotoImportService(repo, photos), repo, photos);
    }

    /// <summary>유통사 바코드가 파일명이면 그 상품에 붙는다.</summary>
    [Fact]
    public async Task PlanAsync_MatchesByManufacturerBarcode()
    {
        var (service, repo, _) = Build();
        repo.Products.Add(Make("p1", "Amoxicillin", barcode: "8801234567890"));

        var source = new FakePhotoSource();
        source.Add("8801234567890.jpg");

        var plan = await service.PlanAsync(source);

        Assert.Equal("p1", Assert.Single(plan.Matches).Product.ProductId);
        Assert.Empty(plan.UnmatchedFiles);
    }

    /// <summary>유통사 바코드가 없는 상품은 내부 바코드로 붙는다.</summary>
    [Fact]
    public async Task PlanAsync_MatchesByInternalBarcode()
    {
        var (service, repo, _) = Build();
        repo.Products.Add(Make("p1", "Amoxicillin", internalBarcode: "INT-00000146"));

        var source = new FakePhotoSource();
        source.Add("INT-00000146.png");

        var plan = await service.PlanAsync(source);

        Assert.Equal("p1", Assert.Single(plan.Matches).Product.ProductId);
    }

    /// <summary>
    /// 낱개 라벨을 그대로 파일명으로 쓴 경우도 받아 준다.
    /// 사진은 상품의 것이지 판매 단위의 것이 아니라, 같은 상품에 붙어야 한다.
    /// </summary>
    [Fact]
    public async Task PlanAsync_AcceptsTheLooseUnitSuffix()
    {
        var (service, repo, _) = Build();
        repo.Products.Add(Make("p1", "Amoxicillin", internalBarcode: "INT-00000146", unitsPerBox: 30));

        var source = new FakePhotoSource();
        source.Add("INT-00000146-EA.jpg");

        var plan = await service.PlanAsync(source);

        Assert.Equal("p1", Assert.Single(plan.Matches).Product.ProductId);
    }

    /// <summary>바코드가 없는 상품도 있어야 한다. 그때는 상품명으로 붙인다.</summary>
    [Fact]
    public async Task PlanAsync_FallsBackToProductName()
    {
        var (service, repo, _) = Build();
        repo.Products.Add(Make("p1", "Amoxicillin 500mg"));

        var source = new FakePhotoSource();
        source.Add("  amoxicillin 500MG .jpg");

        var plan = await service.PlanAsync(source);

        Assert.Equal("p1", Assert.Single(plan.Matches).Product.ProductId);
    }

    /// <summary>바코드가 이름보다 우선한다. 이름은 겹칠 수 있어도 바코드는 유일하다.</summary>
    [Fact]
    public async Task PlanAsync_PrefersBarcodeOverName()
    {
        var (service, repo, _) = Build();
        repo.Products.Add(Make("byName", "8801234567890"));
        repo.Products.Add(Make("byBarcode", "Amoxicillin", barcode: "8801234567890"));

        var source = new FakePhotoSource();
        source.Add("8801234567890.jpg");

        var plan = await service.PlanAsync(source);

        Assert.Equal("byBarcode", Assert.Single(plan.Matches).Product.ProductId);
    }

    /// <summary>짝을 못 찾은 파일은 따로 알린다. 조용히 넘기면 왜 안 들어갔는지 알 수 없다.</summary>
    [Fact]
    public async Task PlanAsync_ReportsFilesWithNoMatchingProduct()
    {
        var (service, repo, _) = Build();
        repo.Products.Add(Make("p1", "Amoxicillin", barcode: "8801234567890"));

        var source = new FakePhotoSource();
        source.Add("9999999999999.jpg");

        var plan = await service.PlanAsync(source);

        Assert.Empty(plan.Matches);
        Assert.Equal("9999999999999.jpg", Assert.Single(plan.UnmatchedFiles));
    }

    /// <summary>
    /// 아이폰 기본 형식(HEIC)은 읽지 못한다. 폴더가 가득 차 있는데 아무 일도 없으면
    /// 원인을 찾을 수 없으므로 이유를 함께 남긴다.
    /// </summary>
    [Fact]
    public async Task PlanAsync_ExplainsFormatsItCannotRead()
    {
        var (service, repo, _) = Build();
        repo.Products.Add(Make("p1", "Amoxicillin", barcode: "8801234567890"));

        var source = new FakePhotoSource();
        source.Add("8801234567890.heic");

        var plan = await service.PlanAsync(source);

        Assert.Empty(plan.Matches);
        var skipped = Assert.Single(plan.SkippedFiles);
        Assert.Contains("HEIC", skipped.Reason);
        Assert.Contains("Most Compatible", skipped.Reason);
    }

    /// <summary>이미 사진이 있으면 덮어쓴다는 사실을 미리 센다. 되돌릴 수 없는 동작이다.</summary>
    [Fact]
    public async Task PlanAsync_CountsPhotosItWillReplace()
    {
        var (service, repo, photos) = Build();
        repo.Products.Add(Make("p1", "A", barcode: "111"));
        repo.Products.Add(Make("p2", "B", barcode: "222"));
        await photos.SaveAsync("p1", [9]);

        var source = new FakePhotoSource();
        source.Add("111.jpg");
        source.Add("222.jpg");

        var plan = await service.PlanAsync(source);

        Assert.Equal(1, plan.ReplaceCount);
        Assert.Equal(1, plan.NewCount);
    }

    /// <summary>한 상품에 사진 두 장이 걸리면 어느 쪽이 맞는지 알 수 없다. 둘째부터 막는다.</summary>
    [Fact]
    public async Task PlanAsync_RejectsTwoFilesClaimingTheSameProduct()
    {
        var (service, repo, _) = Build();
        repo.Products.Add(Make("p1", "Amoxicillin", barcode: "8801234567890"));

        var source = new FakePhotoSource();
        source.Add("8801234567890.jpg");
        source.Add("Amoxicillin.jpg");

        var plan = await service.PlanAsync(source);

        Assert.Single(plan.Matches);
        Assert.Single(plan.Issues);
    }

    /// <summary>계획에 든 파일만 읽는다. 폴더를 통째로 메모리에 올리지 않는다.</summary>
    [Fact]
    public async Task ApplyAsync_ReadsOnlyTheMatchedFiles()
    {
        var (service, repo, photos) = Build();
        repo.Products.Add(Make("p1", "Amoxicillin", barcode: "111"));

        var source = new FakePhotoSource();
        source.Add("111.jpg", [7, 7]);
        source.Add("nobody.jpg");

        var plan = await service.PlanAsync(source);
        var result = await service.ApplyAsync(plan, source);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(["111.jpg"], source.ReadFiles);
        Assert.Equal([7, 7], photos.Saved["p1"].Bytes);
    }

    /// <summary>한 장이 실패해도 나머지는 들어가야 한다. 200장 중 한 장 때문에 멈추면 안 된다.</summary>
    [Fact]
    public async Task ApplyAsync_KeepsGoingWhenOneFileFails()
    {
        var (service, repo, photos) = Build();
        repo.Products.Add(Make("p1", "A", barcode: "111"));
        repo.Products.Add(Make("p2", "B", barcode: "222"));
        photos.FailingProductIds.Add("p1");

        var source = new FakePhotoSource();
        source.Add("111.jpg");
        source.Add("222.jpg");

        var plan = await service.PlanAsync(source);
        var result = await service.ApplyAsync(plan, source);

        Assert.Equal(1, result.SuccessCount);
        Assert.Single(result.Failures);
        Assert.True(photos.Saved.ContainsKey("p2"));
    }

    /// <summary>파일을 읽지 못해도 예외가 아니라 실패 한 줄로 남는다.</summary>
    [Fact]
    public async Task ApplyAsync_ReportsUnreadableFilesInsteadOfThrowing()
    {
        var (service, repo, _) = Build();
        repo.Products.Add(Make("p1", "A", barcode: "111"));

        var source = new FakePhotoSource();
        source.Add("111.jpg");
        source.Unreadable.Add("111.jpg");

        var plan = await service.PlanAsync(source);
        var result = await service.ApplyAsync(plan, source);

        Assert.Equal(0, result.SuccessCount);
        Assert.Contains("could not be read", Assert.Single(result.Failures).Reason);
    }
}
