namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 달러와 리엘을 섞어 받은 현금 한 건을 계산한다.
///
/// 캄보디아 약국의 보통 모습이다 — 값은 달러로 매기고, 손님은 달러 지폐와 리엘 지폐를
/// 섞어 내며, 거스름돈은 리엘로 돌려준다. 달러 동전이 돌지 않기 때문에 센트 단위
/// 거스름은 리엘로밖에 줄 수 없다.
///
/// 이 계산이 ViewModel이 아니라 여기 있는 이유: 환율과 반올림이 섞인 돈 계산이고,
/// 계산대에서 틀리면 그날 시재가 맞지 않는데 화면에는 아무 표시도 나지 않는다.
/// <see cref="RielConverter"/>와 같은 자리에 두는 것은 반올림 규칙(currency.rounding)을
/// 공유하기 때문이다 — 영수증에 찍히는 환산액과 계산대가 말하는 금액이 갈리면 안 된다.
/// </summary>
public sealed class MixedCashTender
{
    private MixedCashTender(
        decimal totalUsd, decimal usdTendered, long rielTendered,
        decimal rielTenderedInUsd, decimal changeUsd, long changeRiel)
    {
        TotalUsd = totalUsd;
        UsdTendered = usdTendered;
        RielTendered = rielTendered;
        RielTenderedInUsd = rielTenderedInUsd;
        ChangeUsd = changeUsd;
        ChangeRiel = changeRiel;
    }

    public decimal TotalUsd { get; }

    public decimal UsdTendered { get; }

    public long RielTendered { get; }

    /// <summary>받은 리엘을 달러로 환산한 값. 센트 단위로 접는다.</summary>
    public decimal RielTenderedInUsd { get; }

    /// <summary>받은 돈 전부를 달러로 본 값.</summary>
    public decimal TotalTenderedUsd => UsdTendered + RielTenderedInUsd;

    /// <summary>거스름돈(달러 기준). 모자라면 음수다.</summary>
    public decimal ChangeUsd { get; }

    /// <summary>
    /// 실제로 건네줄 거스름돈. <c>currency.rounding</c> 단위로 반올림한 리엘이다 —
    /// 그 단위 미만의 돈은 캄보디아에 유통되지 않아 건넬 방법이 없다.
    /// 모자라면 0이다(모자란 금액은 <see cref="ShortfallUsd"/>가 말한다).
    /// </summary>
    public long ChangeRiel { get; }

    public bool IsEnough => ChangeUsd >= 0;

    /// <summary>더 받아야 하는 금액(달러). 충분하면 0.</summary>
    public decimal ShortfallUsd => IsEnough ? 0m : -ChangeUsd;

    /// <summary>
    /// 받은 돈과 거스름돈을 계산한다.
    ///
    /// rate가 0 이하이면 리엘을 다룰 수 없으므로 리엘 입력은 없는 것으로 보고
    /// 달러만으로 계산한다 — 환율이 설정되지 않은 약국에서 0으로 나눠 죽거나,
    /// 더 나쁘게는 리엘을 공짜로 받은 것처럼 계산되는 일을 막는다.
    /// </summary>
    public static MixedCashTender Calculate(
        decimal totalUsd, decimal usdTendered, long rielTendered, decimal rate, int rounding)
    {
        if (rate <= 0)
        {
            rielTendered = 0;
        }

        // 받은 리엘의 달러 환산. 센트 단위로 접는 이유는 달러 금액이 센트로 표현되기
        // 때문이고, 여기서 접은 나머지는 거스름돈 리엘에서 반올림으로 다시 흡수된다.
        var rielInUsd = rate > 0
            ? decimal.Round(rielTendered / rate, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var changeUsd = usdTendered + rielInUsd - totalUsd;

        var changeRiel = changeUsd > 0 && rate > 0
            ? RielConverter.ToRiel(changeUsd, rate, rounding)
            : 0L;

        return new MixedCashTender(
            totalUsd, usdTendered, rielTendered, rielInUsd, changeUsd, changeRiel);
    }
}
