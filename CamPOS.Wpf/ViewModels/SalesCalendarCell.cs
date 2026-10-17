using System.Globalization;
using PharmaPOS.Application.Reports;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 매출 달력의 칸 하나.
///
/// 달의 1일이 무슨 요일이냐에 따라 앞이 비므로, 빈 칸도 같은 목록에 섞여 들어간다
/// (<see cref="IsFiller"/>). 빈 칸을 목록에서 빼면 1일이 항상 월요일 자리에 붙어
/// 요일 줄과 어긋난다.
/// </summary>
public class SalesCalendarCell
{
    private SalesCalendarCell(DailySalesPoint? point, bool isToday)
    {
        Point = point;
        IsToday = isToday;
    }

    public DailySalesPoint? Point { get; }

    /// <summary>달이 시작하기 전(또는 끝난 뒤)의 빈 자리인지.</summary>
    public bool IsFiller => Point is null;

    public bool IsToday { get; }

    public DateTime? Date => Point?.Date;

    public string DayLabel => Point?.DayLabel ?? string.Empty;

    public string AmountLabel => Point?.AmountLabel ?? string.Empty;

    /// <summary>일요일 칸. 주말을 흐리게 두면 주 단위로 눈이 끊어져 읽기 쉽다.</summary>
    public bool IsSunday => Point?.Date.DayOfWeek == DayOfWeek.Sunday;

    /// <summary>
    /// 칸에 적힌 금액은 반올림한 값이다. 정확한 금액과 건수는 여기서만 볼 수 있다.
    /// 환불이 판매보다 많았던 날은 금액이 음수인데, 그 사실도 여기 적힌다.
    /// </summary>
    public string Tooltip
    {
        get
        {
            if (Point is null)
            {
                return string.Empty;
            }

            var date = Point.Date.ToString("yyyy-MM-dd (ddd)", CultureInfo.InvariantCulture);

            if (!Point.HasSales)
            {
                return $"{date} — no sales";
            }

            return
                $"{date} — {Point.Amount.ToString("N2", CultureInfo.InvariantCulture)}\n" +
                $"{Point.TransactionCount.ToString("N0", CultureInfo.InvariantCulture)} transactions\n" +
                "Click to show this day in the report.";
        }
    }

    public static SalesCalendarCell Filler() => new(null, isToday: false);

    public static SalesCalendarCell For(DailySalesPoint point, DateTime today) =>
        new(point, point.Date == today);
}
