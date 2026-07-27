namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 판매 확정(Confirm Sale) 시도 결과.
/// </summary>
public class SaleResult
{
    public bool IsSuccess { get; }
    public bool RequiresConfirmation { get; }
    public string? Message { get; }

    private SaleResult(bool isSuccess, bool requiresConfirmation, string? message)
    {
        IsSuccess = isSuccess;
        RequiresConfirmation = requiresConfirmation;
        Message = message;
    }

    public static SaleResult Success() => new(true, false, null);

    public static SaleResult Failure(string message) => new(false, false, message);

    public static SaleResult NeedsConfirmation(string message) => new(false, true, message);
}