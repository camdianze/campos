using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Repositories;

/// <summary>재고 이력(Stock_Transaction) 조회.</summary>
public interface IStockHistoryRepository
{
    /// <summary>
    /// 조건에 맞는 거래를 조회한다. dateFrom/dateTo가 null이면 해당 조건은 무시된다.
    /// transactionTypes가 비어 있으면 종류를 가리지 않는다.
    /// </summary>
    Task<IReadOnlyList<StockHistoryLineItem>> SearchAsync(
        string facilityId,
        long? dateFromUtc,
        long? dateToUtc,
        string searchTerm,
        IReadOnlyList<string> transactionTypes);
}
