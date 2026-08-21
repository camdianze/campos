using System.Globalization;
using PharmaPOS.Application.Reports;
using PharmaPOS.Domain.Enums;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 추이 그래프의 막대 하나. 한 달치를 AWaRe 등급으로 나눠 쌓은 높이를 담는다.
///
/// 픽셀 계산이 Application이 아니라 여기 있는 이유: 높이는 화면의 문제고,
/// 집계 결과(AntibioticTrendPoint)는 어디에 그리든 같은 값이어야 한다.
///
/// 치수는 TrendChartMetrics가 정한다. 옆의 매출 그래프와 같은 값을 써야
/// 두 그래프의 0선과 가로축이 겹친다.
///
/// 차트 패키지를 쓰지 않는다. 이 저장소는 바코드도 사각형으로 직접 그리고 있고,
/// 막대 12개에 패키지 의존성을 더할 이유가 없다.
/// </summary>
public class AntibioticTrendBar
{
    public required string Label { get; init; }

    public required string FullLabel { get; init; }

    public required int Total { get; init; }

    public double AccessHeight { get; init; }
    public double WatchHeight { get; init; }
    public double ReserveHeight { get; init; }
    public double NotRecommendedHeight { get; init; }

    public bool HasSales => Total > 0;

    /// <summary>막대 위에 적는 수. 0인 달은 비워 둔다 — 0이 12개 늘어서면 눈금만 시끄럽다.</summary>
    public string TotalDisplay =>
        Total == 0 ? string.Empty : Total.ToString("N0", CultureInfo.InvariantCulture);

    public string Tooltip { get; private init; } = string.Empty;

    public static AntibioticTrendBar From(AntibioticTrendPoint point, int maxTotal)
    {
        double Scale(int quantity) => TrendChartMetrics.Scale(quantity, maxTotal);

        return new AntibioticTrendBar
        {
            Label = point.Label,
            FullLabel = point.FullLabel,
            Total = point.TotalQuantity,
            AccessHeight = Scale(point.AccessQuantity),
            WatchHeight = Scale(point.WatchQuantity),
            ReserveHeight = Scale(point.ReserveQuantity),
            NotRecommendedHeight = Scale(point.NotRecommendedQuantity),
            Tooltip = BuildTooltip(point)
        };
    }

    /// <summary>
    /// 막대는 비율만 보여주므로 실제 숫자는 툴팁이 맡는다.
    /// 등급 이름은 어떤 언어로도 번역하지 않는 국제 표기라 그대로 쓴다.
    /// </summary>
    private static string BuildTooltip(AntibioticTrendPoint point)
    {
        if (point.TotalQuantity == 0)
        {
            return $"{point.FullLabel} — no antibiotic sales";
        }

        return
            $"{point.FullLabel} — {point.TotalQuantity:N0} units\n" +
            $"{AwareGroupCodes.Access} {point.AccessQuantity:N0}\n" +
            $"{AwareGroupCodes.Watch} {point.WatchQuantity:N0}\n" +
            $"{AwareGroupCodes.Reserve} {point.ReserveQuantity:N0}\n" +
            $"{AwareGroupCodes.NotRecommended} {point.NotRecommendedQuantity:N0}";
    }
}
