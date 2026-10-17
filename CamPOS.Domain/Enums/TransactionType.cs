namespace PharmaPOS.Domain.Enums;

/// <summary>
/// Stock_Transaction의 거래 유형.
/// F-05(입고), F-06(판매·환불), F-08(조정)에서 공통으로 사용한다.
/// </summary>
public enum TransactionType
{
    StockIn,
    StockOut,
    Adjustment,

    /// <summary>
    /// 판매 취소. 원 판매(StockOut) 한 줄을 되돌리는 행이며 수량·금액을 음수로 기록한다.
    /// 매출 집계가 StockOut과 Refund를 함께 더하기만 하면 순매출이 되도록 한 것이다.
    /// </summary>
    Refund
}