namespace PharmaPOS.Domain.Enums;

/// <summary>
/// Stock_Transaction의 거래 유형.
/// F-05(입고), F-06(판매), F-08(조정)에서 공통으로 사용한다.
/// </summary>
public enum TransactionType
{
    StockIn,
    StockOut,
    Adjustment
}