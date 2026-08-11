using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Domain.Entities;

/// <summary>
/// PRD의 Stock Transaction 테이블에 대응하는 엔티티.
/// append-only — 이 테이블의 행은 절대 수정/삭제하지 않는다 (엔지니어링 원칙).
/// </summary>
public class StockTransaction
{
    public required string TransactionId { get; set; }

    public required string FacilityId { get; set; }

    public required string ProductId { get; set; }

    public required string UserId { get; set; }

    public required TransactionType TransactionType { get; set; }

    public required string BatchNumber { get; set; }

    public required long ExpiryDate { get; set; }

    /// <summary>
    /// STOCK_IN: 입고 수량(양수). STOCK_OUT: 판매 수량(양수).
    /// ADJUSTMENT: 부호가 있는 증감값(음수 가능).
    /// REFUND: 되돌린 수량(음수).
    /// </summary>
    public required int Quantity { get; set; }

    /// <summary>판매 시점 가격 스냅샷. STOCK_IN/ADJUSTMENT에서는 null.</summary>
    public decimal? SellingPriceAtTransaction { get; set; }

    /// <summary>결제 수단. STOCK_IN/ADJUSTMENT에서는 null. REFUND는 원 판매의 값을 그대로 복사한다.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Quantity × SellingPriceAtTransaction 스냅샷. STOCK_IN/ADJUSTMENT에서는 null.</summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>조정 사유(ADJUSTMENT) 또는 환불 사유(REFUND).</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 이 행이 되돌리는 원 거래의 ID. REFUND에서만 채워진다.
    /// 판매 헤더 테이블이 없으므로 "어느 판매 줄을 얼마나 환불했는가"는
    /// 이 컬럼을 거슬러 올라가 세는 것 말고는 알 방법이 없다.
    /// </summary>
    public string? RelatedTransactionId { get; set; }

    public required long TransactionTime { get; set; }
}