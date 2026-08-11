namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 환불 시도 결과.
/// </summary>
public class RefundResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }

    /// <summary>실제로 돌려준 금액(양수). 실패했으면 0.</summary>
    public decimal RefundedAmount { get; }

    private RefundResult(bool isSuccess, string? message, decimal refundedAmount)
    {
        IsSuccess = isSuccess;
        Message = message;
        RefundedAmount = refundedAmount;
    }

    public static RefundResult Success(decimal refundedAmount)
        => new(true, null, refundedAmount);

    public static RefundResult Failure(string message)
        => new(false, message, 0m);
}
