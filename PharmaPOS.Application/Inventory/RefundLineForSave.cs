using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 저장 계층에 넘기는 환불 한 줄. 원장에 남길 행(Transaction)과,
/// 재고를 되돌리는 데 필요한 값들을 함께 들고 간다.
/// </summary>
public class RefundLineForSave
{
    /// <summary>수량·금액이 음수로 채워진 Refund 행.</summary>
    public required StockTransaction Transaction { get; init; }

    /// <summary>되돌릴 수량(양수). Transaction.Quantity의 절댓값이다.</summary>
    public required int RefundQuantity { get; init; }

    /// <summary>false면 금액만 돌려주고 재고는 늘리지 않는다(개봉·변질 반품).</summary>
    public required bool ReturnToStock { get; init; }

    public required int UnitsPerBox { get; init; }
}
