namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 추이 그래프의 치수. 매출 그래프와 항생제 그래프가 화면 아래에 나란히 놓이므로
/// 두 그래프가 같은 값을 써야 0선과 가로축 눈금이 정확히 겹친다.
///
/// XAML 쪽 행 높이(18 / 132 / 20)도 이 값에 맞춰져 있다. 여기를 고치면 그쪽도 함께 고쳐야 한다.
/// </summary>
public static class TrendChartMetrics
{
    /// <summary>그리는 칸의 높이(px). 가장 큰 달이 이 높이가 된다.</summary>
    public const double PlotHeight = 132;

    /// <summary>
    /// 0이 아닌 값의 최소 높이. 이게 없으면 500개인 달 옆에서 1개인 달이
    /// 소수점 이하 픽셀이 되어 아예 보이지 않는다 — "안 팔렸다"로 읽힌다.
    /// </summary>
    private const double MinimumVisibleHeight = 2;

    /// <summary>
    /// 값을 막대 높이로 옮긴다. 0 이하이거나 기준이 없으면 0을 돌려준다.
    ///
    /// 음수(환불이 판매보다 많았던 달)도 0으로 둔다. 아래로 자라는 막대를 그리려면
    /// 0선을 가운데로 옮겨야 하는데, 그러면 흔한 경우인 "전부 양수"에서 위쪽 절반이
    /// 늘 비어 보인다. 그 달의 실제 금액은 막대 위 숫자와 툴팁이 말해 준다.
    /// </summary>
    public static double Scale(double value, double max)
    {
        if (value <= 0 || max <= 0)
        {
            return 0;
        }

        return Math.Max(MinimumVisibleHeight, value / max * PlotHeight);
    }
}
