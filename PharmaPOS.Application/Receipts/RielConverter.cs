using System.Globalization;

namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 달러 금액을 리엘로 환산한다.
///
/// 캄보디아는 소액 동전이 유통되지 않아 1리엘 단위 잔돈을 낼 방법이 없다.
/// 반올림 없이 찍으면 영수증 금액과 실제로 오간 돈이 어긋난다.
/// </summary>
public static class RielConverter
{
    /// <summary>리엘 기호. 통화 기호는 번역 대상이 아니므로 로케일 파일에 넣지 않는다.</summary>
    public const string RielSymbol = "៛";

    /// <summary>
    /// unit이 0보다 크면 그 단위로 반올림하고, 아니면 1리엘 단위로 반올림한다.
    /// </summary>
    public static long ToRiel(decimal usd, decimal rate, int unit)
    {
        var raw = usd * rate;

        if (unit > 0)
        {
            return (long)Math.Round(raw / unit, MidpointRounding.AwayFromZero) * unit;
        }

        return (long)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 천 단위 구분 쉼표 + 리엘 기호. 숫자는 아라비아 숫자로 고정한다 —
    /// 현재 문화권을 따르면 기기 설정에 따라 구분자가 바뀐다.
    /// </summary>
    public static string Format(long riel) =>
        riel.ToString("N0", CultureInfo.InvariantCulture) + " " + RielSymbol;

    public static string Format(decimal usd, decimal rate, int unit) =>
        Format(ToRiel(usd, rate, unit));
}
