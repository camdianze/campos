using System.Globalization;
using PharmaPOS.Application.Reports;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 매출 추이 그래프의 막대 하나.
///
/// 항생제 그래프(AntibioticTrendBar)와 같은 치수(TrendChartMetrics)를 쓴다.
/// 두 그래프가 화면 아래에 나란히 놓이므로 0선과 가로축이 겹쳐야 한다.
/// </summary>
public class SalesTrendBar
{
    public required string Label { get; init; }

    public required string FullLabel { get; init; }

    public required decimal Amount { get; init; }

    public double Height { get; init; }

    /// <summary>
    /// 막대 위에 적는 금액. 0인 달은 비워 둔다 — 0이 12개 늘어서면 눈금만 시끄럽다.
    /// 소수점은 버린다. 12칸에 소수점까지 넣으면 숫자가 겹쳐 읽을 수 없다.
    /// </summary>
    public string AmountDisplay =>
        Amount == 0 ? string.Empty : Amount.ToString("N0", CultureInfo.InvariantCulture);

    public string Tooltip { get; private init; } = string.Empty;

    public static SalesTrendBar From(SalesTrendPoint point, decimal maxAmount) => new()
    {
        Label = point.Label,
        FullLabel = point.FullLabel,
        Amount = point.Amount,
        Height = TrendChartMetrics.Scale((double)point.Amount, (double)maxAmount),
        Tooltip = BuildTooltip(point)
    };

    /// <summary>
    /// 막대는 비율만 보여주므로 정확한 금액과 건수는 툴팁이 맡는다.
    /// 환불이 판매보다 많았던 달은 금액이 음수인데, 막대는 0으로 그려지므로
    /// 그 사실을 알 수 있는 곳이 여기뿐이다.
    /// </summary>
    private static string BuildTooltip(SalesTrendPoint point)
    {
        if (point.TransactionCount == 0)
        {
            return $"{point.FullLabel} — no sales";
        }

        return
            $"{point.FullLabel} — {point.Amount.ToString("N2", CultureInfo.InvariantCulture)}\n" +
            $"{point.TransactionCount.ToString("N0", CultureInfo.InvariantCulture)} transactions";
    }
}
