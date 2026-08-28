using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Import;

public interface IPhotoImportService
{
    Task<PhotoImportPlan> PlanAsync(IImportPhotoSource source);

    Task<ImportApplyResult> ApplyAsync(PhotoImportPlan plan, IImportPhotoSource source);
}

/// <summary>
/// 폴더에 모아 둔 사진을 상품에 붙인다.
///
/// 파일명을 바코드로 두는 이유: 현장에서 사진을 찍고 파일명만 바코드로 바꾸면 되므로,
/// 조사 시트에 경로를 200줄 적는 것보다 실수가 적다. 시트를 손볼 필요도 없다.
///
/// 상품·재고 임포트와 달리 <b>같은 폴더를 다시 넣는 것을 막지 않는다.</b>
/// 그 차단은 재고가 두 배가 되는 것을 막으려는 장치인데, 사진은 덮어쓰기라 쌓이지 않는다.
/// 사진을 다시 찍어 넣는 것은 정상적인 사용이다.
/// </summary>
public class PhotoImportService : IPhotoImportService
{
    /// <summary>
    /// 받아 줄 확장자. Windows가 별도 코덱 없이 언제나 읽어 주는 것만 넣는다.
    /// 현장 서식은 .jpg를 쓰도록 안내한다 — 카메라·휴대폰이 전부 만들고, 사진에서 가장 작다.
    /// </summary>
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp"];

    /// <summary>
    /// 읽지 못하는 형식과 그 안내. 특히 HEIC는 아이폰 기본 저장 형식이라
    /// "사진이 잔뜩 든 폴더인데 한 장도 안 들어간다"는 상황이 그대로 생긴다.
    /// </summary>
    private static readonly Dictionary<string, string> KnownUnsupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".heic"] = "HEIC is an iPhone format Windows cannot read here. "
                        + "Set the iPhone camera to Most Compatible, or convert the folder to JPG.",
            [".heif"] = "HEIF cannot be read here. Convert the folder to JPG.",
            [".webp"] = "WebP cannot be read here. Convert the file to JPG.",
            [".gif"] = "GIF is not used for product photos. Convert the file to JPG.",
            [".tif"] = "TIFF files are very large. Convert the file to JPG.",
            [".tiff"] = "TIFF files are very large. Convert the file to JPG."
        };

    private readonly IProductRepository _productRepository;
    private readonly IProductPhotoService _photoService;

    public PhotoImportService(IProductRepository productRepository, IProductPhotoService photoService)
    {
        _productRepository = productRepository;
        _photoService = photoService;
    }

    public async Task<PhotoImportPlan> PlanAsync(IImportPhotoSource source)
    {
        var products = await _productRepository.SearchAsync(string.Empty, statusFilter: null);

        var matches = new List<PhotoImportMatch>();
        var unmatched = new List<string>();
        var issues = new List<ImportIssue>();
        var skipped = new List<ImportIssue>();

        // 파일명이 같은 상품을 두 번 가리키면 어느 쪽이 맞는지 알 수 없다.
        var claimedProductIds = new Dictionary<string, string>(StringComparer.Ordinal);

        var lineNumber = 0;

        foreach (var fileName in source.FileNames)
        {
            lineNumber++;

            var extension = Path.GetExtension(fileName);

            if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                // 왜 안 들어갔는지 말해 준다. 조용히 넘기면 폴더가 가득 차 있는데도
                // "넣을 사진이 없습니다"만 보인다.
                skipped.Add(new ImportIssue(lineNumber,
                    $"{fileName}: " + (KnownUnsupportedExtensions.TryGetValue(extension, out var reason)
                        ? reason
                        : "Not an image this import can read. Use JPG.")));
                continue;
            }

            var key = Path.GetFileNameWithoutExtension(fileName).Trim();

            if (key.Length == 0)
            {
                continue;
            }

            var candidates = FindProducts(products, key);

            if (candidates.Count == 0)
            {
                unmatched.Add(fileName);
                continue;
            }

            if (candidates.Count > 1)
            {
                issues.Add(new ImportIssue(lineNumber,
                    $"{fileName}: matches {candidates.Count} products. Rename it to a barcode."));
                continue;
            }

            var product = candidates[0];

            if (claimedProductIds.TryGetValue(product.ProductId, out var alreadyUsedFile))
            {
                issues.Add(new ImportIssue(lineNumber,
                    $"{fileName}: {product.ProductName} already takes its photo from {alreadyUsedFile}."));
                continue;
            }

            claimedProductIds[product.ProductId] = fileName;

            var existing = await _photoService.GetAsync(product.ProductId);

            matches.Add(new PhotoImportMatch(fileName, product, ReplacesExistingPhoto: existing is not null));
        }

        return new PhotoImportPlan
        {
            Matches = matches,
            UnmatchedFiles = unmatched,
            Issues = issues,
            SkippedFiles = skipped,
            TotalFiles = source.FileNames.Count
        };
    }

    public async Task<ImportApplyResult> ApplyAsync(PhotoImportPlan plan, IImportPhotoSource source)
    {
        var successCount = 0;
        var failures = new List<ImportIssue>();
        var lineNumber = 0;

        foreach (var match in plan.Matches)
        {
            lineNumber++;

            byte[] bytes;

            try
            {
                // 한 장씩 읽는다. 폴더를 통째로 메모리에 올리면 사진 300장에 1GB가 된다.
                bytes = source.Read(match.FileName);
            }
            catch (Exception)
            {
                failures.Add(new ImportIssue(lineNumber, $"{match.FileName}: the file could not be read."));
                continue;
            }

            // 줄이고 다시 압축하는 규칙은 화면에서 한 장씩 넣을 때와 같은 것을 쓴다.
            var result = await _photoService.SaveAsync(match.Product.ProductId, bytes);

            if (!result.IsSuccess)
            {
                failures.Add(new ImportIssue(lineNumber, $"{match.FileName}: {result.Message}"));
                continue;
            }

            successCount++;
        }

        return new ImportApplyResult
        {
            SuccessCount = successCount,
            Failures = failures
        };
    }

    /// <summary>
    /// 파일명으로 상품을 찾는다. 바코드가 먼저다 — 이름은 겹칠 수 있어도 바코드는 유일하다.
    ///
    /// 낱개용 접미사(-EA)를 떼고 한 번 더 보는 이유: 라벨을 뽑아 둔 그대로 파일명을
    /// 적는 경우가 있는데, 사진은 상품의 것이지 판매 단위의 것이 아니다.
    /// 이름까지 받아 주는 이유: 바코드가 없는 상품에도 사진을 붙일 수 있어야 하고,
    /// 상품 임포트가 이미 이름으로 상품을 알아본다.
    /// </summary>
    private static List<Product> FindProducts(IReadOnlyList<Product> products, string key)
    {
        var byBarcode = products
            .Where(p => Matches(p.Barcode, key) || Matches(p.InternalBarcode, key))
            .ToList();

        if (byBarcode.Count > 0)
        {
            return byBarcode;
        }

        if (key.EndsWith(Product.UnitBarcodeSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var withoutSuffix = key[..^Product.UnitBarcodeSuffix.Length];

            var byUnitBarcode = products
                .Where(p => Matches(p.InternalBarcode, withoutSuffix) || Matches(p.Barcode, withoutSuffix))
                .ToList();

            if (byUnitBarcode.Count > 0)
            {
                return byUnitBarcode;
            }
        }

        return products
            .Where(p => InitialImportColumns.ProductNameComparer.Equals(
                InitialImportColumns.NormalizeProductName(p.ProductName),
                InitialImportColumns.NormalizeProductName(key)))
            .ToList();
    }

    private static bool Matches(string? value, string key) =>
        !string.IsNullOrWhiteSpace(value) && string.Equals(value.Trim(), key, StringComparison.OrdinalIgnoreCase);
}
