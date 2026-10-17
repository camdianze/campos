using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 관리자 대시보드 지표 조회를 담당하는 인터페이스. (Screen SCR-ADMIN-015)
/// Low Stock/Expiry 개수는 이미 F-09의 IAlertRepository가 담당하므로 여기서는 다루지 않는다.
/// </summary>
public interface IAdminDashboardRepository
{
    /// <summary>오늘(현지 자정 기준) STOCK_OUT 거래의 total_amount 합계와 건수를 조회한다.</summary>
    Task<(decimal totalAmount, int count)> GetDailySalesAsync(string facilityId, long todayStartUtc, long todayEndUtc);

    /// <summary>Active 상품 수를 조회한다.</summary>
    Task<int> GetActiveProductCountAsync();

    /// <summary>현재 재고 가치(수량 × 원가 합계)를 조회한다.</summary>
    Task<decimal> GetTotalInventoryValueAsync(string facilityId);
}