namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 판매 확정(Confirm Sale) 시도 결과.
/// </summary>
public class SaleResult
{
    public bool IsSuccess { get; }
    public bool RequiresConfirmation { get; }
    public string? Message { get; }

    /// <summary>
    /// 저장된 판매 줄과 그 거래 ID. 성공했을 때만 채워진다.
    /// 복약안내 로그를 거래에 연결하는 데 쓴다.
    /// </summary>
    public IReadOnlyList<ConfirmedSaleLine> ConfirmedLines { get; }

    private SaleResult(
        bool isSuccess,
        bool requiresConfirmation,
        string? message,
        IReadOnlyList<ConfirmedSaleLine> confirmedLines)
    {
        IsSuccess = isSuccess;
        RequiresConfirmation = requiresConfirmation;
        Message = message;
        ConfirmedLines = confirmedLines;
    }

    public static SaleResult Success(IReadOnlyList<ConfirmedSaleLine> confirmedLines)
        => new(true, false, null, confirmedLines);

    public static SaleResult Failure(string message)
        => new(false, false, message, Array.Empty<ConfirmedSaleLine>());

    public static SaleResult NeedsConfirmation(string message)
        => new(false, true, message, Array.Empty<ConfirmedSaleLine>());
}