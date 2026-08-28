using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Products;

public interface IProductPhotoService
{
    Task<ProductPhoto?> GetAsync(string productId);

    /// <summary>고른 파일의 원본 바이트를 받아 줄이고 저장한다.</summary>
    Task<ProductPhotoResult> SaveAsync(string productId, byte[] fileBytes);

    Task<ProductPhotoResult> RemoveAsync(string productId);
}

/// <summary>
/// 상품 사진 저장/삭제. 다른 서비스와 같이 예외 대신 결과 객체로 실패를 알린다.
/// </summary>
public class ProductPhotoService : IProductPhotoService
{
    /// <summary>
    /// 저장할 때 긴 변을 이 크기로 줄인다. 상세 화면에서 보는 용도라 이 이상은 쓸 데가 없고,
    /// 원본 3MB짜리를 그대로 넣으면 상품 300개에 DB가 1GB가 된다.
    /// </summary>
    public const int MaxEdgePixels = 800;

    /// <summary>
    /// 받아 줄 원본 파일 크기 한도. 이미지로 읽어 보기 전에 먼저 거른다 —
    /// 수십 MB짜리를 디코딩하다 메모리를 다 쓰는 쪽이 더 나쁘다.
    /// </summary>
    public const int MaxSourceBytes = 20 * 1024 * 1024;

    private readonly IProductRepository _productRepository;
    private readonly IPhotoEncoder _photoEncoder;

    public ProductPhotoService(IProductRepository productRepository, IPhotoEncoder photoEncoder)
    {
        _productRepository = productRepository;
        _photoEncoder = photoEncoder;
    }

    public async Task<ProductPhoto?> GetAsync(string productId)
    {
        try
        {
            return await _productRepository.GetPhotoAsync(productId);
        }
        catch (Exception)
        {
            // 사진을 못 읽는다고 상세 화면이 열리지 않으면 안 된다. 사진 없는 상태로 연다.
            return null;
        }
    }

    public async Task<ProductPhotoResult> SaveAsync(string productId, byte[] fileBytes)
    {
        if (fileBytes.Length == 0)
        {
            return ProductPhotoResult.Failure("The selected file is empty.");
        }

        if (fileBytes.Length > MaxSourceBytes)
        {
            return ProductPhotoResult.Failure(
                $"The image is too large. Use a file under {MaxSourceBytes / (1024 * 1024)} MB.");
        }

        var encoded = _photoEncoder.Encode(fileBytes, MaxEdgePixels);

        if (encoded is null)
        {
            return ProductPhotoResult.Failure("The selected file could not be read as an image.");
        }

        var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        try
        {
            await _productRepository.SavePhotoAsync(productId, encoded, updatedAt);
        }
        catch (Exception)
        {
            return ProductPhotoResult.Failure("The photo could not be saved.");
        }

        return ProductPhotoResult.Success(new ProductPhoto(encoded, updatedAt));
    }

    public async Task<ProductPhotoResult> RemoveAsync(string productId)
    {
        try
        {
            await _productRepository.SavePhotoAsync(productId, photo: null, updatedAt: null);
        }
        catch (Exception)
        {
            return ProductPhotoResult.Failure("The photo could not be removed.");
        }

        return ProductPhotoResult.Success(photo: null);
    }
}

/// <summary>사진 저장/삭제 결과. 성공하면 화면이 그대로 쓸 수 있는 사진이 함께 온다.</summary>
public class ProductPhotoResult
{
    private ProductPhotoResult(bool isSuccess, string? message, ProductPhoto? photo)
    {
        IsSuccess = isSuccess;
        Message = message;
        Photo = photo;
    }

    public bool IsSuccess { get; }
    public string? Message { get; }

    /// <summary>저장 후의 사진. 지웠으면 null이다.</summary>
    public ProductPhoto? Photo { get; }

    public static ProductPhotoResult Success(ProductPhoto? photo) => new(true, null, photo);
    public static ProductPhotoResult Failure(string message) => new(false, message, null);
}
