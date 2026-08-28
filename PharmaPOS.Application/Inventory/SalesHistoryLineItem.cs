namespace PharmaPOS.Application.Inventory;

/// <summary>
/// 판매 내역 화면(SCR-SALES-007)에 표시할 조인된 판매 라인 데이터.
/// 환불 행도 같은 목록에 섞여 오며, 그 경우 Quantity와 LineTotal이 음수다.
/// </summary>
public class SalesHistoryLineItem
{
    public required string TransactionId { get; set; }
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string BatchNumber { get; set; }
    public required int Quantity { get; set; }
    public required decimal UnitPrice { get; set; }
    public required decimal LineTotal { get; set; }
    public required string PaymentMethod { get; set; }
    public required string UserId { get; set; }
    public required string Username { get; set; }
    public required long TransactionTime { get; set; }

    /// <summary>
    /// 이 거래 직전·직후 그 배치의 재고. 재고가 맞지 않을 때 역추적하는 값이다.
    /// 이 컬럼이 생기기 전의 거래는 채울 방법이 없어 null이고, 파일에는 빈칸으로 나간다 —
    /// 0으로 채우면 "그때 재고가 0이었다"로 읽힌다.
    /// </summary>
    public long? StockBefore { get; set; }

    public long? StockAfter { get; set; }

    /// <summary>"StockOut" 또는 "Refund".</summary>
    public required string TransactionType { get; set; }

    /// <summary>이 판매 줄에서 이미 환불된 수량(양수). 환불 행 자신은 언제나 0이다.</summary>
    public required int RefundedQuantity { get; set; }

    public bool IsRefund => TransactionType == "Refund";

    /// <summary>목록에서 상태를 한눈에 보이게 한다.</summary>
    public string StatusText => IsRefund
        ? "Refund"
        : RefundedQuantity == 0
            ? string.Empty
            : RefundedQuantity >= Quantity
                ? "Refunded"
                : $"Refunded {RefundedQuantity}/{Quantity}";
}