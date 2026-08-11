namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-05 입고 등록 로직을 담당하는 인터페이스. (Screen SCR-STOCKIN-009)
/// </summary>
public interface IStockInService
{
    /// <summary>
    /// Screen SCR-STOCKIN-009, 4절의 입고 검증/저장 흐름을 수행한다.
    ///
    /// quantity의 단위는 상품에 달려 있다. 입고는 언제나 박스째 들어오므로,
    /// units_per_box가 1을 넘는 상품이면 quantity는 "박스 개수"이고 총 낱개 수는
    /// quantity × units_per_box가 된다. units_per_box가 1인 상품은 종전과 같이
    /// quantity가 곧 낱개 개수다.
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