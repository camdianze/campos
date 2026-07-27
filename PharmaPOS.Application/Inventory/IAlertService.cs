namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-09 재고 알림 로직을 담당하는 인터페이스. (Screen SCR-ALERT-014)
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Low Stock과 Expiry 알림을 분류/필터링해서 반환한다.
    /// </summary>
    Task<IReadOnlyList<AlertItem>> GetAlertsAsync(
        string facilityId,
        AlertTypeFilter typeFilter,
        AlertPriorityFilter priorityFilter);
}