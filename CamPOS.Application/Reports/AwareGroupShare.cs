using System.Globalization;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// AWaRe 등급 하나가 기간 내 항생제 판매에서 차지하는 몫.
///
/// 요약 카드는 ACCESS 비중 하나만 보여준다. WHO 권고(70% 이상)를 한 값으로 읽기에는
/// 그게 맞지만, ACCESS가 낮을 때 남은 몫이 WATCH인지 RESERVE인지는 대응이 전혀 다르다.
/// 그래서 네 등급을 따로 늘어놓는다.
/// </summary>
public class AwareGroupShare
{
    /// <summary>ACCESS / WATCH / RESERVE / NOT_RECOMMENDED. 번역하지 않는다.</summary>
    public required string Group { get; init; }

    /// <summary>낱개 기준 판매 수량.</summary>
    public required int Quantity { get; init; }

    /// <summary>같은 기간 항생제 판매 수량 전체.</summary>
    public required int TotalQuantity { get; init; }

    public decimal? SharePercent =>
        TotalQuantity == 0 ? null : (decimal)Quantity / TotalQuantity * 100m;

    public string ShareDisplay =>
        SharePercent is null
            ? "—"
            : SharePercent.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// <summary>비중 옆에 붙는 실제 수량. 0.5%가 1개인지 500개인지는 비중만으로 알 수 없다.</summary>
    public string QuantityDisplay =>
        Quantity.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>막대 길이 계산용. 비중이 없으면 0으로 둔다.</summary>
    public double Fraction =>
        SharePercent is null ? 0 : (double)SharePercent.Value / 100.0;

    /// <summary>막대의 빈 쪽. 화면이 두 칸의 비율로 막대를 그리는 데 쓴다.</summary>
    public double Remainder => 1.0 - Fraction;
}
