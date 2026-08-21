namespace PharmaPOS.Application.Inventory;

/// <summary>
/// F-15 영수증 출력을 담당하는 인터페이스.
/// </summary>
public interface IReceiptPrintingService
{
    /// <summary>
    /// 판매 완료 후 영수증을 출력한다.
    /// 실패하더라도 판매 거래 자체는 이미 확정된 상태이며,
    /// 이 메서드의 실패는 판매를 취소하지 않는다 (Screen §5절 원칙).
    /// </summary>
    Task<ReceiptPrintResult> PrintReceiptAsync(ReceiptPrintRequest request);
}
