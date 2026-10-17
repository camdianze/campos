namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 화면이 요청하는 "이 판매 줄을 이만큼 환불해 달라". 금액은 담지 않는다 —
/// 단가는 판매 시점 스냅샷을 DB에서 다시 읽어 쓰므로 화면이 정할 몫이 아니다.
/// </summary>
public class RefundLineRequest
{
    /// <summary>원 판매 줄(StockOut)의 거래 ID.</summary>
    public required string TransactionId { get; init; }

    /// <summary>되돌릴 수량(낱개 기준, 양수).</summary>
    public required int Quantity { get; init; }
}
