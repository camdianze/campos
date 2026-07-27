namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-05 입고 등록 로직을 담당하는 인터페이스. (Screen SCR-STOCKIN-009)
/// </summary>
public interface IStockInService
{
    /// <summary>
    /// Screen SCR-STOCKIN-009, 4절의 입고 검증/저장 흐름을 수행한다.
    /// </summary>
    Task<StockInResult> SaveStockInAsync(
        string facilityId,
        string productId,
        string userId,
        string batchNumber,
        DateTime expiryDate,
        DateTime stockInDate,
        int quantity);
}