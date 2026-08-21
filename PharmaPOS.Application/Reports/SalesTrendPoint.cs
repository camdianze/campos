using System.Globalization;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// 매출 추이의 한 달치.
///
/// 금액은 <b>순매출</b>이다 — 환불 행은 음수로 쌓여 있어 그대로 더하면 저절로 상계된다.
/// 판매 행만 더하면 총매출이 되어 위 요약 카드의 Sales Amount와 달라지고,
/// 같은 화면의 두 숫자가 어긋나면 어느 쪽이 맞는지 알 수 없다.
///
/// 판매가 없던 달도 0으로 채워 넣는다. 없는 달을 빼면 그래프의 가로 간격이
/// 달마다 달라져서, 비어 있는 달과 이어지는 달을 눈으로 구분할 수 없다.
/// </summary>
public class SalesTrendPoint
{
    /// <summary>그 달의 1일.</summary>
    public required DateTime Month { get; init; }

    /// <summary>순매출. 환불이 판매보다 많았던 달은 음수가 될 수 있다.</summary>
    public decimal Amount { get; init; }

    /// <summary>거래 건수. 환불은 세지 않는다 — 환불은 또 한 번의 판매가 아니다.</summary>
    public int TransactionCount { get; init; }

    /// <summary>가로축 눈금. 해가 바뀌는 달만 연도를 적어 12칸이 빽빽해지지 않게 한다.</summary>
    public string Label => Month.Month == 1
        ? Month.ToString("yyyy", CultureInfo.InvariantCulture)
        : Month.ToString("MMM", CultureInfo.InvariantCulture);

    public string FullLabel => Month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
