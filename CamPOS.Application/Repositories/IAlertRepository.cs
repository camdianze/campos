using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 알림(F-09)의 원본 데이터 조회를 담당하는 인터페이스.
/// 우선순위 분류는 IAlertService가 담당한다.
/// </summary>
public interface IAlertRepository
{
    /// <summary>
    /// 상품별 총 재고 수량이 Safety Stock Level보다 낮은 상품 목록을 조회한다.
    /// </summary>
    Task<IReadOnlyList<LowStockCandidate>> GetLowStockCandidatesAsync(string facilityId);

    /// <summary>
    /// 유통기한이 90일 이내인 배치 목록을 조회한다 (이미 만료된 것 포함).
    /// </summary>
    Task<IReadOnlyList<ExpiryCandidate>> GetExpiryCandidatesAsync(string facilityId);
}