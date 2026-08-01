namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 용지를 실제로 출력한다.
/// </summary>
public interface ICounsellingSheetPrintingService
{
    /// <summary>
    /// 용지 한 장을 인쇄한다. 프린터가 없거나 실패해도 예외를 던지지 않고
    /// Failure를 돌려준다 — 인쇄 실패가 판매를 막아서는 안 된다.
    /// </summary>
    Task<CounsellingPrintResult> PrintAsync(CounsellingSheetDocument document);
}
