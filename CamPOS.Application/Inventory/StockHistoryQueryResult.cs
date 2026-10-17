namespace PharmaPOS.Application.Inventory;

/// <summary>재고 이력 조회 결과. 날짜 검증 실패와 조회 실패를 구분한다.</summary>
public class StockHistoryQueryResult
{
    public bool IsSuccess { get; }
    public IReadOnlyList<StockHistoryLineItem>? Items { get; }
    public string? Message { get; }

    private StockHistoryQueryResult(bool isSuccess, IReadOnlyList<StockHistoryLineItem>? items, string? message)
    {
        IsSuccess = isSuccess;
        Items = items;
        Message = message;
    }

    public static StockHistoryQueryResult Success(IReadOnlyList<StockHistoryLineItem> items) =>
        new(true, items, null);

    public static StockHistoryQueryResult Failure(string message) => new(false, null, message);
}
