using System.Globalization;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// 전기 대비 증감 표기. 리포트의 세 표가 모두 같은 규칙으로 보여야 해서 한 곳에 모았다.
///
/// 직전 기간이 0일 때 백분율을 만들 수 없다는 게 핵심이다. 그걸 0%나 100%로 얼버무리면
/// "작년에 안 팔던 걸 올해 처음 팔았다"와 "그대로다"가 같은 값으로 보인다.
/// 그래서 계산 불가일 때는 숫자를 만들지 않고 "new"/"—"로 구분해 둔다.
/// </summary>
public static class PeriodChange
{
    /// <summary>계산할 수 없으면 null. 호출부가 그 사실을 그대로 다룰 수 있게 한다.</summary>
    public static decimal? Percent(decimal current, decimal previous) =>
        previous == 0 ? null : (current - previous) / previous * 100m;

    public static string Format(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            // 직전 기간에 없던 항목. 늘었다고 말할 기준 자체가 없다.
            return current == 0 ? "—" : "new";
        }

        var percent = Percent(current, previous)!.Value;

        // 소수점 첫 자리까지만. 리포트에서 0.01%의 차이는 읽는 데 방해만 된다.
        var sign = percent > 0 ? "+" : string.Empty;
        return sign + percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }
}
