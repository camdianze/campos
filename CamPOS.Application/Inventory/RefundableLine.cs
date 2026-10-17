namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 환불 화면에 띄울 "판매된 줄 하나"와, 그 줄이 지금까지 얼마나 환불됐는지.
///
/// 판매 헤더 테이블이 없어 한 거래는 (판매 시각 + 판매자) 조합으로 묶이고,
/// 환불 가능 수량은 원 판매 수량에서 이미 나간 환불을 뺀 값이다.
/// 화면과 서비스가 같은 값을 봐야 하므로 양쪽 다 이 타입을 쓴다.
/// </summary>
public class RefundableLine
{
    /// <summary>원 판매 줄(StockOut)의 거래 ID. 환불 행은 이 값을 가리킨다.</summary>
    public required string TransactionId { get; init; }

    public required string ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string BatchNumber { get; init; }
    public required long ExpiryDate { get; init; }

    /// <summary>원 판매 수량(낱개 기준, 양수).</summary>
    public required int SoldQuantity { get; init; }

    /// <summary>이미 환불된 수량(낱개 기준, 양수).</summary>
    public required int RefundedQuantity { get; init; }

    /// <summary>판매 시점의 낱개 단가 스냅샷.</summary>
    public required decimal UnitPrice { get; init; }

    public required decimal LineTotal { get; init; }

    public required string PaymentMethod { get; init; }

    /// <summary>재고를 되돌릴 때 박스/낱개를 계산하는 데 쓴다. 상품이 지워졌으면 1.</summary>
    public required int UnitsPerBox { get; init; }

    /// <summary>아직 환불할 수 있는 수량.</summary>
    public int RemainingQuantity => Math.Max(0, SoldQuantity - RefundedQuantity);
}
