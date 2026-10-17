using PharmaPOS.Application.Receipts;

namespace PharmaPOS.Tests.Receipts;

/// <summary>
/// 달러와 리엘을 섞어 받은 현금 계산.
///
/// 계산대에서 틀리면 그날 시재가 맞지 않는데, 화면에도 원장에도 아무 표시가 나지 않는다.
/// 판매는 정상으로 기록되고 돈만 어긋난 채 남는다 — 저녁에 금고를 세어 봐야 안다.
/// </summary>
public class MixedCashTenderTests
{
    private const decimal Rate = 4100m;
    private const int Rounding = 100;

    private static MixedCashTender Tender(decimal total, decimal usd, long riel,
        decimal rate = Rate, int rounding = Rounding) =>
        MixedCashTender.Calculate(total, usd, riel, rate, rounding);

    // ── 섞어 받기 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 이 계산이 존재하는 이유. $4.53짜리를 $2 지폐와 12,000리엘로 내면
    /// 받은 돈은 $4.93이고 거스름은 $0.40 — 직원이 암산할 값이 아니다.
    /// </summary>
    [Fact]
    public void UsdAndRielTogether_AddUpAndLeaveChange()
    {
        var tender = Tender(total: 4.53m, usd: 2m, riel: 12_000);

        Assert.Equal(2.93m, tender.RielTenderedInUsd);
        Assert.Equal(4.93m, tender.TotalTenderedUsd);
        Assert.Equal(0.40m, tender.ChangeUsd);
        Assert.True(tender.IsEnough);
    }

    /// <summary>거스름은 리엘로 건넨다. 0.40 × 4100 = 1,640 → 100 단위로 1,600.</summary>
    [Fact]
    public void Change_IsHandedOverInRielRoundedToTheCirculatingUnit()
    {
        var tender = Tender(total: 4.53m, usd: 2m, riel: 12_000);

        Assert.Equal(1_600, tender.ChangeRiel);
    }

    /// <summary>반올림 단위가 500이면 그 단위로 접힌다. 1,640 → 1,500.</summary>
    [Fact]
    public void Change_FollowsTheConfiguredRoundingUnit()
    {
        var tender = Tender(total: 4.53m, usd: 2m, riel: 12_000, rounding: 500);

        Assert.Equal(1_500, tender.ChangeRiel);
    }

    // ── 한 통화만 ─────────────────────────────────────────────────────────

    [Fact]
    public void UsdOnly_BehavesLikeBefore()
    {
        var tender = Tender(total: 4.53m, usd: 5m, riel: 0);

        Assert.Equal(5m, tender.TotalTenderedUsd);
        Assert.Equal(0.47m, tender.ChangeUsd);
        Assert.True(tender.IsEnough);
    }

    [Fact]
    public void RielOnly_IsAcceptedWithoutAnyDollars()
    {
        var tender = Tender(total: 4.53m, usd: 0m, riel: 20_000);

        Assert.Equal(4.88m, tender.RielTenderedInUsd);
        Assert.Equal(0.35m, tender.ChangeUsd);
        Assert.Equal(1_400, tender.ChangeRiel);
    }

    [Fact]
    public void ExactAmount_LeavesNoChange()
    {
        var tender = Tender(total: 5m, usd: 5m, riel: 0);

        Assert.True(tender.IsEnough);
        Assert.Equal(0m, tender.ChangeUsd);
        Assert.Equal(0, tender.ChangeRiel);
    }

    // ── 모자랄 때 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 모자란 채로 확정되면 판매는 기록되는데 돈은 덜 받은 상태가 된다.
    /// 부족액을 말해 줘야 얼마를 더 받을지 알 수 있다.
    /// </summary>
    [Fact]
    public void NotEnough_ReportsTheShortfallAndOffersNoChange()
    {
        var tender = Tender(total: 10m, usd: 2m, riel: 8_200);

        Assert.False(tender.IsEnough);
        Assert.Equal(6m, tender.ShortfallUsd);
        Assert.Equal(0, tender.ChangeRiel);
    }

    [Fact]
    public void NothingTendered_IsShortByTheWholeTotal()
    {
        var tender = Tender(total: 4.53m, usd: 0m, riel: 0);

        Assert.False(tender.IsEnough);
        Assert.Equal(4.53m, tender.ShortfallUsd);
    }

    // ── 환율이 없을 때 ────────────────────────────────────────────────────

    /// <summary>
    /// 환율이 설정되지 않았으면 리엘을 달러로 바꿀 방법이 없다. 그 상태에서 리엘을
    /// 받은 것으로 치면 <b>공짜로 받은 돈</b>이 되어 판매가 통과해 버린다.
    /// 0으로 나누어 죽는 것보다 이쪽이 더 위험하다 — 아무 일도 없어 보인다.
    /// </summary>
    [Fact]
    public void WithoutAnExchangeRate_RielIsIgnoredRatherThanCountedFree()
    {
        var tender = Tender(total: 4.53m, usd: 1m, riel: 20_000, rate: 0m);

        Assert.Equal(0m, tender.RielTenderedInUsd);
        Assert.Equal(1m, tender.TotalTenderedUsd);
        Assert.False(tender.IsEnough);
        Assert.Equal(0, tender.ChangeRiel);
    }

    [Fact]
    public void WithoutAnExchangeRate_UsdStillWorks()
    {
        var tender = Tender(total: 4.53m, usd: 5m, riel: 0, rate: 0m);

        Assert.True(tender.IsEnough);
        Assert.Equal(0.47m, tender.ChangeUsd);
    }

    // ── 반올림의 방향 ─────────────────────────────────────────────────────

    /// <summary>
    /// 받은 리엘의 달러 환산은 센트로 접는다. 접힌 나머지는 거스름돈 리엘의
    /// 반올림에서 다시 흡수되므로, 양쪽이 서로 다른 규칙으로 움직이지 않는다.
    /// </summary>
    [Fact]
    public void RielTendered_IsConvertedToWholeCents()
    {
        // 5,000 / 4100 = 1.2195... → 1.22
        var tender = Tender(total: 1m, usd: 0m, riel: 5_000);

        Assert.Equal(1.22m, tender.RielTenderedInUsd);
        Assert.Equal(0.22m, tender.ChangeUsd);
    }

    /// <summary>
    /// 영수증의 합계 환산과 계산대의 환산이 같은 함수를 써야 한다.
    /// 갈리면 종이와 화면이 다른 금액을 말하고, 어느 쪽이 맞는지 알 수 없다.
    /// </summary>
    [Fact]
    public void ChangeInRiel_MatchesTheReceiptsOwnConversion()
    {
        var tender = Tender(total: 4.53m, usd: 2m, riel: 12_000);

        Assert.Equal(
            RielConverter.ToRiel(tender.ChangeUsd, Rate, Rounding),
            tender.ChangeRiel);
    }
}
