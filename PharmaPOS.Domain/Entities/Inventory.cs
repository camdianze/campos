namespace PharmaPOS.Domain.Entities;

/// <summary>
/// PRD의 Inventory 테이블에 대응하는 엔티티.
/// 읽기 최적화용 캐시 테이블 — Stock_Transaction으로부터 계산된 값이며,
/// 별도의 진실 공급원이 아니다 (엔지니어링 노트).
/// </summary>
public class Inventory
{
    /// <summary>
    /// 유효기간을 모르는 배치의 expiry_date 값.
    ///
    /// 컬럼이 NOT NULL이라 "없음"을 NULL로 표현할 수 없어 0을 쓴다. 수기로 관리하던 약국의
    /// 초기 재고에는 유효기간이 남아 있지 않은 경우가 흔하고, 그 배치를 아예 못 넣게 하면
    /// 초기 임포트 자체가 막힌다. 0인 배치는 만료 알림과 만료 판매 차단에서 제외된다 —
    /// 모르는 날짜를 1970-01-01로 읽어 전량이 만료 처리되는 쪽이 훨씬 나쁘다.
    /// </summary>
    public const long NoExpiryDate = 0L;

    public required string InventoryId { get; set; }

    public required string FacilityId { get; set; }

    public required string ProductId { get; set; }

    public required string BatchNumber { get; set; }

    public required long ExpiryDate { get; set; }

    /// <summary>
    /// 낱개 기준 총 재고. 박스/낱개 상품이라도 이 값이 재고의 진실이며,
    /// 아래 두 값은 그 총량을 "안 헐린 박스 / 헐어 놓은 낱개"로 나눠 놓은 것이다.
    /// 항상 CurrentQuantity = BoxQuantity × units_per_box + UnitQuantity 를 만족한다.
    /// </summary>
    public required int CurrentQuantity { get; set; }

    /// <summary>아직 뜯지 않은 박스 수. units_per_box가 1인 상품은 늘 0이다.</summary>
    public int BoxQuantity { get; set; }

    /// <summary>헐어 놓은 낱개 수. units_per_box가 1인 상품은 여기에 전량이 들어간다.</summary>
    public int UnitQuantity { get; set; }

    public required long UpdatedAt { get; set; }
}