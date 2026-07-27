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
    /// </summary>
    public required int Quantity { get; set; }

    /// <summary>판매 시점 가격 스냅샷. STOCK_IN/ADJUSTMENT에서는 null.</summary>
    public decimal? SellingPriceAtTransaction { get; set; }

    /// <summary>결제 수단. STOCK_IN/ADJUSTMENT에서는 null.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Quantity × SellingPriceAtTransaction 스냅샷. STOCK_IN/ADJUSTMENT에서는 null.</summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>조정 사유. ADJUSTMENT에서만 사용.</summary>
    public string? Reason { get; set; }

    public required long TransactionTime { get; set; }
}