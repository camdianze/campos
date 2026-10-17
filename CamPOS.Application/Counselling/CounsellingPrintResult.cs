namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 용지 인쇄 시도 결과.
/// 실패해도 판매 거래는 이미 확정된 상태이며, 이 결과가 판매를 되돌리지 않는다.
/// </summary>
public class CounsellingPrintResult
{
    public bool IsSuccess { get; }

    public string? Message { get; }

    private CounsellingPrintResult(bool isSuccess, string? message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static CounsellingPrintResult Success() => new(true, null);

    public static CounsellingPrintResult Failure(string message) => new(false, message);
}
