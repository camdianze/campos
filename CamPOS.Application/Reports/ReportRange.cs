namespace PharmaPOS.Application.Reports;

/// <summary>
/// 리포트가 집계할 기간과, 그와 비교할 직전 기간.
///
/// "전달 비교"의 정의가 애매해질 수 있어 규칙을 하나로 고정했다.
/// 달력상 한 달을 통째로 고르면 비교 대상은 전월이고(8월 → 7월, 길이가 달라도 그대로),
/// 그 밖의 임의 구간은 같은 길이의 바로 앞 구간과 비교한다.
/// 전자를 특별 취급하지 않으면 "8월(31일)"의 비교 대상이 "7월 1일~31일"이 아니라
/// "6월 30일~7월 30일"이 되어, 달 단위로 보려는 사람에게는 틀린 값이 된다.
/// </summary>
public sealed class ReportRange
{
    private ReportRange(DateTime from, DateTime to, DateTime previousFrom, DateTime previousTo)
    {
        From = from;
        To = to;
        PreviousFrom = previousFrom;
        PreviousTo = previousTo;
    }

    /// <summary>집계 시작일(그 날 포함).</summary>
    public DateTime From { get; }

    /// <summary>집계 종료일(그 날 포함).</summary>
    public DateTime To { get; }

    public DateTime PreviousFrom { get; }

    public DateTime PreviousTo { get; }

    /// <summary>고른 구간이 달력상 한 달과 정확히 일치하는지. 화면 문구를 고르는 데 쓴다.</summary>
    public bool IsWholeCalendarMonth =>
        From.Day == 1 && To == new DateTime(From.Year, From.Month, DateTime.DaysInMonth(From.Year, From.Month));

    public long FromUtc => ToUnixStartOfDay(From);
    public long ToUtc => ToUnixEndOfDay(To);
    public long PreviousFromUtc => ToUnixStartOfDay(PreviousFrom);
    public long PreviousToUtc => ToUnixEndOfDay(PreviousTo);

    public string Label => $"{From:yyyy-MM-dd} ~ {To:yyyy-MM-dd}";
    public string PreviousLabel => $"{PreviousFrom:yyyy-MM-dd} ~ {PreviousTo:yyyy-MM-dd}";

    public static ReportRange Create(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;

        // 달력상 한 달을 통째로 고른 경우에는 전월 전체와 비교한다.
        var isWholeMonth =
            start.Day == 1
            && end == new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));

        if (isWholeMonth)
        {
            var previousMonth = start.AddMonths(-1);

            return new ReportRange(
                start, end,
                previousMonth,
                new DateTime(
                    previousMonth.Year,
                    previousMonth.Month,
                    DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month)));
        }

        var lengthInDays = (end - start).Days + 1;
        var previousEnd = start.AddDays(-1);

        return new ReportRange(start, end, previousEnd.AddDays(-(lengthInDays - 1)), previousEnd);
    }

    /// <summary>이번 달 1일부터 오늘까지. 화면을 처음 열었을 때의 기본 구간이다.</summary>
    public static ReportRange CurrentMonthToDate(DateTime today)
    {
        var start = new DateTime(today.Year, today.Month, 1);
        return Create(start, today.Date);
    }

    private static long ToUnixStartOfDay(DateTime date) =>
        new DateTimeOffset(date.Date).ToUnixTimeMilliseconds();

    // 종료일은 "그 날 전체"를 포함해야 하므로 다음날 자정 직전까지로 잡는다
    // (판매 내역 화면과 같은 방식).
    private static long ToUnixEndOfDay(DateTime date) =>
        new DateTimeOffset(date.Date.AddDays(1).AddMilliseconds(-1)).ToUnixTimeMilliseconds();
}
