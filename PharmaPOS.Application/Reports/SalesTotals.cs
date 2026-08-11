namespace PharmaPOS.Application.Reports;

/// <summary>
/// 한 기간의 매출 요약.
/// </summary>
public class SalesTotals
{
    public decimal Amount { get; init; }

    /// <summary>판매 수량 합계. 재고와 같은 낱개 기준이다 (박스 10통은 300으로 잡힌다).</summary>
    public int ItemCount { get; init; }

    /// <summary>
    /// 거래 건수. 판매 헤더 테이블이 없어서 "판매 시각 + 판매자" 조합의 가짓수로 센다.
    /// 장바구니 한 번에 상품 3개를 담으면 Stock_Transaction은 3줄이지만 1건으로 셈한다.
    /// </summary>
    public int TransactionCount { get; init; }

    public static SalesTotals Empty { get; } = new();
}
