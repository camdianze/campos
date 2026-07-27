namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 영수증 출력 시도 결과.
/// </summary>
public class ReceiptPrintResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }

    private ReceiptPrintResult(bool isSuccess, string? message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static ReceiptPrintResult Success() => new(true, null);

    public static ReceiptPrintResult Failure(string message) => new(false, message);
}