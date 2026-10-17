using System.Globalization;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// 증감 방향. 색을 정하는 쪽이 서식 문자열을 되짚어 읽지 않게 따로 내준다.
/// 화면은 상승을 붉게, 하락을 파랗게 칠한다 (동아시아 표기 관례).
/// </summary>
public enum ChangeDirection
{
    None,
    Up,
    Down
}

/// <summary>
/// 전기 대비 증감 표기. 리포트의 카드와 표가 모두 같은 규칙으로 보여야 해서 한 곳에 모았다.
///
/// 직전 기간이 0일 때 백분율을 만들 수 없다는 게 핵심이다. 그걸 0%나 100%로 얼버무리면
/// "작년에 안 팔던 걸 올해 처음 팔았다"와 "그대로다"가 같은 값으로 보인다.
/// 그래서 계산 불가일 때는 숫자를 만들지 않고 "new"로 구분해 둔다.
///
/// 부호(+/-)는 붙이지 않는다. 방향은 화살표가 말하고, 숫자는 크기만 말한다 —
/// "▼ -8%"는 마이너스가 두 번 나오는 셈이다.
/// </summary>
public static class PeriodChange
{
    /// <summary>변화가 없을 때 찍는 표시.</summary>
    public const string NoChangeMarker = "[-]";

    public const string UpArrow = "▲";
    public const string DownArrow = "▼";

    /// <summary>계산할 수 없으면 null. 호출부가 그 사실을 그대로 다룰 수 있게 한다.</summary>
    public static decimal? Percent(decimal current, decimal previous) =>
        previous == 0 ? null : (current - previous) / previous * 100m;

    /// <summary>
    /// 올랐는지 내렸는지. 직전 기간이 0이어도(백분율이 없어도) 방향은 정해진다 —
    /// 0에서 늘어난 것은 분명히 상승이다.
    /// </summary>
    public static ChangeDirection DirectionOf(decimal current, decimal previous)
    {
        if (current == previous)
        {
            return ChangeDirection.None;
        }

        return current > previous ? ChangeDirection.Up : ChangeDirection.Down;
    }

    public static string Format(decimal current, decimal previous)
    {
        var direction = DirectionOf(current, previous);

        if (direction == ChangeDirection.None)
        {
            return NoChangeMarker;
        }

        var arrow = direction == ChangeDirection.Up ? UpArrow : DownArrow;

        if (previous == 0)
        {
            // 직전 기간에 없던 항목. 나눌 기준이 없어 백분율을 만들지 않는다.
            return arrow + " new";
        }

        // 소수점 첫 자리까지만. 리포트에서 0.01%의 차이는 읽는 데 방해만 된다.
        var magnitude = Math.Abs(Percent(current, previous)!.Value);

        return arrow + " " + magnitude.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// 증감 절대량. 백분율 옆에 "그래서 얼마 늘었나"를 붙이는 값이다.
    ///
    /// 두 경우에 빈 문자열을 돌려준다. 변화가 없을 때는 (0)이 군더더기이고,
    /// 직전 기간이 0일 때는 증감량이 현재값과 같아서 카드에 크게 적힌 숫자를 되풀이하는 셈이 된다.
    /// </summary>
    public static string FormatDelta(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return string.Empty;
        }

        var delta = current - previous;

        if (delta == 0)
        {
            return string.Empty;
        }

        // 부호를 적어 둔다. 괄호 안의 맨숫자는 회계 표기에서 음수로도 읽힌다.
        var sign = delta > 0 ? "+" : "-";

        return "(" + sign + Math.Abs(delta).ToString("#,0.##", CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>
    /// 전체 대비 비중. 분모가 0이면 계산할 수 없어 null이다.
    /// 증감과 달리 방향이 없는 값이라 화살표도 색도 붙지 않는다.
    /// </summary>
    public static decimal? Share(decimal part, decimal total) =>
        total == 0 ? null : part / total * 100m;

    /// <summary>비중 표기. 계산할 수 없으면 변화 없음과 같은 표시를 쓴다.</summary>
    public static string FormatShare(decimal part, decimal total)
    {
        var share = Share(part, total);

        return share is null
            ? NoChangeMarker
            : share.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }
}
