using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// Product Master 테이블에 대한 데이터 접근을 추상화한 인터페이스.
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// 검색어(상품명/성분명/바코드/내부바코드)와 상태 필터로 상품 목록을 조회한다.
    /// searchTerm이 빈 문자열이면 전체 조회, statusFilter가 null이면 상태 무관 전체 조회.
    /// </summary>
    Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, EntityStatus? statusFilter);

    /// <summary>
    /// product_id로 단일 상품을 조회한다. 존재하지 않으면 null을 반환한다.
    /// </summary>
    Task<Product?> GetByIdAsync(string productId);

    /// <summary>
    /// 제조사 바코드가 이미 등록되어 있는지 확인한다.
    /// excludeProductId를 지정하면(수정 시 자기 자신 제외), 그 상품은 검사에서 제외한다.
    /// </summary>
    Task<bool> BarcodeExistsAsync(string barcode, string? excludeProductId = null);

    /// <summary>
    /// 내부 바코드가 이미 등록되어 있는지 확인한다.
    /// </summary>
    Task<bool> InternalBarcodeExistsAsync(string internalBarcode, string? excludeProductId = null);

    /// <summary>
    /// 신규 상품을 저장한다.
    /// </summary>
    Task InsertAsync(Product product);

    /// <summary>
    /// 기존 상품 정보를 갱신한다 (product_id 기준).
    /// </summary>
    Task UpdateAsync(Product product);

    /// <summary>
    /// 상품을 비활성화한다 (물리 삭제 아님, status = Inactive로 변경).
    /// </summary>
    Task DeactivateAsync(string productId);

    /// <summary>
    /// 상품 사진. 없으면 null.
    /// 목록 조회와 갈라 둔 이유: 사진은 장당 수백 KB라 상품 목록에 딸려 오면 검색이 느려진다.
    /// </summary>
    Task<ProductPhoto?> GetPhotoAsync(string productId);

    /// <summary>사진을 넣거나 바꾼다. photo가 null이면 지운다.</summary>
    Task SavePhotoAsync(string productId, byte[]? photo, long? updatedAt);
}