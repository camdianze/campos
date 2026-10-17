namespace PharmaPOS.Application.Products;

/// <summary>
/// 라벨 출력 결과.
/// 주의: Stage 1 현재 실제 프린터 하드웨어 연동은 미구현 상태이며,
/// 이 결과는 "출력 준비 완료"만 확인한다 (M-5, 라벨 프린터 기종 미정).
/// </summary>
public class LabelPrintResult
{
    public bool IsSuccess { get; }
    public string? Message { get; }

    private LabelPrintResult(bool isSuccess, string? message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static LabelPrintResult Success(string message) => new(true, message);

    public static LabelPrintResult Failure(string message) => new(false, message);
}