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
    /// 이 코드가 어느 상품에든 이미 쓰이고 있는지 확인한다 — 제조사 바코드, 내부 바코드,
    /// 낱개 바코드 <b>셋 모두</b>를 본다.
    ///
    /// 컬럼별로 따로 보면 안 된다. 스캐너는 어느 칸에 들어 있는 값인지 모르고 찍으므로,
    /// A상품의 내부 바코드와 B상품의 제조사 바코드가 같으면 그 코드를 찍었을 때
    /// 어느 쪽이 잡히는지 계산대에서 알 수 없다. 바코드를 손으로 입력할 수 있게 되면서
    /// 그 상태를 만들 수 있게 됐다.
    ///
    /// excludeProductId를 지정하면(수정 시 자기 자신 제외), 그 상품은 검사에서 제외한다.
    /// </summary>
    Task<bool> BarcodeInUseAsync(string code, string? excludeProductId = null);

    /// <summary>
    /// 신규 상품을 저장한다.
    /// </summary>
    Task InsertAsync(Product product);

    /// <summary>
    /// 기존 상품 정보를 갱신한다 (product_id 기준).
    /// </summary>
    Task UpdateAsync(Product product);

    /// <summary>
    /// 박스당 개수가 바뀐 상품을 갱신하면서, 그 상품의 모든 배치 재고를 같은 트랜잭션 안에서
    /// 다시 센다. 재고의 <b>개수</b>는 그대로 두고 그 개수가 가리키는 것을 바꾼다 — 박스 구분이
    /// 없던 상품의 10개는 낱개 판매를 켜는 순간 10박스가 되고, 낱개 총량은 10 × 박스당 개수가 된다.
    /// 배치마다 조정(Adjustment) 원장 행을 남겨 stock_before/after 사슬이 끊기지 않게 한다.
    ///
    /// 낱개 판매를 끄는데(새 값 1) 헐어 놓은 낱개가 남은 배치가 있으면 아무것도 쓰지 않고
    /// false를 돌려준다 — 그 낱개는 박스로 셀 수 없어 조용히 사라질 것이기 때문이다.
    /// </summary>
    Task<bool> UpdateWithUnitsPerBoxChangeAsync(Product product, int previousUnitsPerBox, string userId);

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