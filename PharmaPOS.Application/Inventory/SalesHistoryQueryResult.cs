namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 판매 내역 조회 시도 결과. 날짜 검증 실패를 구분하기 위한 결과 타입.
/// </summary>
public class SalesHistoryQueryResult
{
    public bool IsSuccess { get; }
    public IReadOnlyList<SalesHistoryLineItem>? Items { get; }
    public string? Message { get; }

    private SalesHistoryQueryResult(bool isSuccess, IReadOnlyList<SalesHistoryLineItem>? items, string? message)
    {
        IsSuccess = isSuccess;
        Items = items;
        Message = message;
    }

    public static SalesHistoryQueryResult Success(IReadOnlyList<SalesHistoryLineItem> items) =>
        new(true, items, null);

    public static SalesHistoryQueryResult Failure(string message) => new(false, null, message);
}