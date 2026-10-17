using System.Globalization;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// 달력 한 칸, 즉 하루치 매출.
///
/// 금액 규칙은 <see cref="SalesTrendPoint"/>와 같다 — <b>순매출</b>이고(환불 행이 음수로
/// 쌓여 있어 그대로 더하면 상계된다), 건수는 판매 행만 센다. 달력은 요약 카드 바로
/// 아래에 놓이므로, 여기서 규칙이 갈리면 날짜 칸을 다 더한 값과 Sales Amount가 어긋나고
/// 같은 화면의 두 숫자 중 어느 쪽이 맞는지 알 수 없게 된다.
///
/// 판매가 없던 날도 0으로 채워 넣는다. 달력은 빈 날도 자리를 차지해야 하는 그림이다.
/// </summary>
public class DailySalesPoint
{
    public required DateTime Date { get; init; }

    /// <summary>순매출. 환불이 판매보다 많았던 날은 음수가 될 수 있다.</summary>
    public decimal Amount { get; init; }

    /// <summary>거래 건수. 환불은 세지 않는다 — 환불은 또 한 번의 판매가 아니다.</summary>
    public int TransactionCount { get; init; }

    public bool HasSales => TransactionCount > 0 || Amount != 0;

    public string DayLabel => Date.Day.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// 칸에 적을 금액. 달력 한 칸은 좁아서 소수점을 그대로 두면 줄이 넘친다.
    /// 정확한 값은 그 날을 눌러 기간을 바꾸면 위 카드에 그대로 나온다.
    /// </summary>
    public string AmountLabel => !HasSales
        ? string.Empty
        : Math.Round(Amount, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture);
}
