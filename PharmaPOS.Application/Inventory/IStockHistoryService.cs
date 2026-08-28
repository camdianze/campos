namespace PharmaPOS.Application.Inventory;

/// <summary>재고 이력 조회 서비스.</summary>
public interface IStockHistoryService
{
    Task<StockHistoryQueryResult> SearchAsync(
        string facilityId,
        DateTime? dateFrom,
        DateTime? dateTo,
        string searchTerm,
        StockHistoryFilter filter);
}
