namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 재고 조정 저장 시도 결과.
/// 성공/실패 외에, "Delta가 0인데 확인이 필요한" 상태와
/// "동시성 충돌" 상태를 별도로 구분한다.
/// </summary>
public class AdjustmentResult
{
    public bool IsSuccess { get; }
    public bool RequiresConfirmation { get; }
    public bool IsConcurrencyConflict { get; }
    public string? Message { get; }

    private AdjustmentResult(bool isSuccess, bool requiresConfirmation, bool isConcurrencyConflict, string? message)
    {
        IsSuccess = isSuccess;
        RequiresConfirmation = requiresConfirmation;
        IsConcurrencyConflict = isConcurrencyConflict;
        Message = message;
    }

    public static AdjustmentResult Success() => new(true, false, false, null);

    public static AdjustmentResult Failure(string message) => new(false, false, false, message);

    public static AdjustmentResult NeedsConfirmation(string message) => new(false, true, false, message);

    public static AdjustmentResult ConcurrencyConflict() =>
        new(false, false, true, "Inventory quantity has changed. Please try again.");
}