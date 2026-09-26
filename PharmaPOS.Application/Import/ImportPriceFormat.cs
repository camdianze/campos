using System.Globalization;

namespace PharmaPOS.Application.Import;

/// <summary>파일의 가격 칸을 어느 통화로 읽을지.</summary>
public enum ImportPriceCurrency
{
    /// <summary>표시 없는 숫자는 달러. 지금까지 만든 파일이 전부 이쪽이다.</summary>
    Usd,

    /// <summary>표시 없는 숫자는 리엘. 환율로 나눠 달러로 저장한다.</summary>
    Riel
}

/// <summary>
/// 임포트가 금액 칸 하나를 읽는 방법. 기본 통화와 환율을 함께 들고 다닌다.
///
/// 칸마다 <c>1000 KHR</c>처럼 적을 수 있었지만, 실제로 받는 시트는 <b>통째로</b>
/// 한 통화로 적혀 있다. 6,000리엘짜리 상품이 200줄이면 200번 KHR을 붙여야 하고,
/// 한 줄이라도 빠뜨리면 그 상품만 $6,000으로 들어간다 — 값이 0보다 크고 소수점
/// 자리도 맞아서 어떤 검사에도 걸리지 않고, 누가 화면에서 눈으로 볼 때까지 아무
/// 일도 일어나지 않는다. 그래서 통화는 파일을 고를 때 한 번 정한다.
///
/// <b>칸에 적힌 표시가 항상 이긴다.</b> 리엘로 읽기로 해 두었어도 <c>$1.50</c>이라
/// 적힌 칸은 달러다 — 표시는 그 칸에 대해 더 구체적인 지시이고, 두 통화가 섞인
/// 시트(원가는 달러로 사 오고 판매가는 리엘로 매기는 경우)가 실제로 있다.
/// </summary>
public sealed class ImportPriceFormat
{
    /// <summary>리엘로 적었다는 표시. 키보드로 ៛를 치기 어려워 글자 표기도 받는다.</summary>
    private static readonly string[] RielMarkers = ["៛", "KHR", "riels", "riel", "R"];

    private static readonly string[] UsdMarkers = ["$", "USD"];

    public ImportPriceCurrency DefaultCurrency { get; }

    /// <summary>1달러가 몇 리엘인지. 0이면 설정되지 않은 것이다.</summary>
    public decimal ExchangeRate { get; }

    public ImportPriceFormat(ImportPriceCurrency defaultCurrency, decimal exchangeRate)
    {
        DefaultCurrency = defaultCurrency;
        ExchangeRate = exchangeRate;
    }

    /// <summary>
    /// 리엘로 읽기로 해 두었는데 환율이 없는 상태. 파일을 한 줄도 읽기 전에
    /// 막아야 한다 — 그대로 진행하면 모든 가격 행이 같은 이유로 실패한다.
    /// </summary>
    public bool IsUnusable => DefaultCurrency == ImportPriceCurrency.Riel && ExchangeRate <= 0;

    /// <summary>미리보기에 적을 한 줄. 무엇으로 읽었는지는 숫자보다 먼저 보여야 한다.</summary>
    public string Description => DefaultCurrency == ImportPriceCurrency.Riel
        ? $"riel (1 USD = {ExchangeRate:N0} KHR)"
        : "US dollars";

    /// <summary>
    /// 금액 칸 하나를 달러로 읽는다. 저장되는 값은 언제나 달러다 —
    /// 이 앱의 모든 금액이 달러이고, 환율은 시간이 지나면 바뀌기 때문이다.
    /// </summary>
    public bool TryRead(string text, out decimal usd, out string? error)
    {
        usd = 0m;
        error = null;

        var trimmed = text.Trim();
        var isRiel = DefaultCurrency == ImportPriceCurrency.Riel;
        var hasMarker = false;

        foreach (var marker in RielMarkers)
        {
            if (TryStrip(ref trimmed, marker))
            {
                isRiel = true;
                hasMarker = true;
                break;
            }
        }

        if (!hasMarker)
        {
            foreach (var marker in UsdMarkers)
            {
                if (TryStrip(ref trimmed, marker))
                {
                    // 리엘로 읽는 중이라도 $가 붙은 칸은 달러다.
                    isRiel = false;
                    break;
                }
            }
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            // 오류 문구에 ៛를 쓰지 않는다. 대화상자 글꼴이 그 글자를 못 그리면 네모가 뜨고,
            // 읽는 사람은 무엇을 적으라는 것인지 알 수 없다. KHR은 어느 글꼴에나 있다.
            error = "must be a number. Write it as 1000 KHR to give the price in riel.";
            return false;
        }

        if (!isRiel)
        {
            usd = value;
            return true;
        }

        // 리엘로 적혀 있는데 환율이 없으면 환산할 방법이 없다. 4,000을 4,000달러로
        // 저장하느니 그 행을 세워 두는 편이 낫다.
        if (ExchangeRate <= 0)
        {
            error = "is in riel, but no exchange rate is set. "
                  + "Set it in Admin Dashboard → Receipt Settings first.";
            return false;
        }

        // 네 자리까지 남긴다. 두 자리로 접으면 500리엘(=$0.125)이 $0.13이 되고,
        // 계산대에서 다시 리엘로 바꾸면 520리엘이 되어 원래 정한 가격과 달라진다.
        usd = decimal.Round(value / ExchangeRate, Products.ProductService.LooseUnitPriceDecimals,
            MidpointRounding.AwayFromZero);
        return true;
    }

    private static bool TryStrip(ref string text, string marker)
    {
        if (text.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            text = text[marker.Length..].Trim();
            return true;
        }

        if (text.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^marker.Length].Trim();
            return true;
        }

        return false;
    }
}
