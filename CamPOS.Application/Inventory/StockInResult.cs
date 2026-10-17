namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 입고 저장 시도 결과.
/// </summary>
public class StockInResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }

    private StockInResult(bool isSuccess, string? message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static StockInResult Success() => new(true, null);

    public static StockInResult Failure(string message) => new(false, message);
}