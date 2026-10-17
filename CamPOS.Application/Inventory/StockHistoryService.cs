using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Inventory;

/// <summary>IStockHistoryService의 구현체.</summary>
public class StockHistoryService : IStockHistoryService
{
    private readonly IStockHistoryRepository _stockHistoryRepository;

    public StockHistoryService(IStockHistoryRepository stockHistoryRepository)
    {
        _stockHistoryRepository = stockHistoryRepository;
    }

    /// <summary>
    /// 필터가 조회할 거래 종류. All은 빈 목록을 돌려주어 종류 조건 자체를 걸지 않는다.
    /// Sale이 StockOut과 Refund를 함께 가져오는 것은, 환불이 판매를 되돌린 줄이라
    /// 떼어 놓으면 남은 판매 줄이 이미 취소된 것인지 알 수 없기 때문이다.
    /// </summary>
    public static IReadOnlyList<string> TransactionTypesFor(StockHistoryFilter filter) => filter switch
    {
        StockHistoryFilter.StockIn => new[] { "StockIn" },
        StockHistoryFilter.Adjustment => new[] { "Adjustment" },
        StockHistoryFilter.Sale => new[] { "StockOut", "Refund" },
        _ => Array.Empty<string>()
    };

    public async Task<StockHistoryQueryResult> SearchAsync(
        string facilityId,
        DateTime? dateFrom,
        DateTime? dateTo,
        string searchTerm,
        StockHistoryFilter filter)
    {
        if (dateFrom is not null && dateTo is not null && dateFrom > dateTo)
        {
            return StockHistoryQueryResult.Failure("Start date cannot be later than end date.");
        }

        long? dateFromUtc = dateFrom is not null
            ? new DateTimeOffset(dateFrom.Value.Date).ToUnixTimeMilliseconds()
            : null;

        // 종료일은 "그 날짜 전체"를 포함해야 하므로 다음날 자정 직전까지로 계산한다.
        long? dateToUtc = dateTo is not null
            ? new DateTimeOffset(dateTo.Value.Date.AddDays(1).AddMilliseconds(-1)).ToUnixTimeMilliseconds()
            : null;

        IReadOnlyList<StockHistoryLineItem> items;

        try
        {
            items = await _stockHistoryRepository.SearchAsync(
                facilityId, dateFromUtc, dateToUtc, searchTerm, TransactionTypesFor(filter));
        }
        catch (Exception)
        {
            return StockHistoryQueryResult.Failure("Stock history could not be loaded.");
        }

        return StockHistoryQueryResult.Success(items);
    }
}
