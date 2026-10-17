using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증 한 장을 그리는 데 필요한 모든 것.
/// 렌더러는 이 객체 밖의 어떤 것도 읽지 않는다 — 그래야 미리보기와 실제 인쇄가
/// 같은 그림을 낸다.
/// </summary>
public class ReceiptRenderRequest
{
    public required ReceiptSettings Settings { get; init; }

    public required ReceiptText Text { get; init; }

    public required IReadOnlyList<SaleLineItem> Lines { get; init; }

    /// <summary>실제로 받은 금액. 부가세는 이 금액에 포함된 것으로 계산한다.</summary>
    public required decimal TotalAmount { get; init; }

    public decimal? CashTendered { get; init; }

    public decimal? ChangeDue { get; init; }

    /// <summary>거래 시각(Unix epoch ms, UTC). 프놈펜 시간으로 바꿔 찍는다.</summary>
    public required long TransactionTime { get; init; }

    /// <summary>발행된 영수증 번호. 표시가 꺼져 있거나 발번에 실패했으면 null.</summary>
    public string? ReceiptNumber { get; init; }

    /// <summary>판매를 처리한 직원 이름.</summary>
    public string? StaffName { get; init; }

    public string? PaymentMethod { get; init; }
}
