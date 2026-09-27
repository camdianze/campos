using PharmaPOS.Application.Import;
using PharmaPOS.Application.Receipts;

namespace PharmaPOS.Tests.Import;

/// <summary>
/// 파일에 낱개가가 없을 때 박스가에서 뽑아 넣는 기능.
///
/// 앱이 계산대에서 몰래 하던 것과 <b>계산식은 같지만 성질이 다르다.</b> 여기서 나온
/// 값은 상품에 저장돼 화면에 보이고 고칠 수 있으며, 미리보기가 몇 건이 이렇게 들어가는지
/// 먼저 말한다. 판매 시점에 만들어지는 값은 하나도 없다.
///
/// 그리고 <b>기본은 꺼짐</b>이다. 계산해서 나온 값은 약국이 매긴 가격이 아니다 —
/// 박스를 헐어 파는 데 붙이는 마진을 앱이 알 수는 없고, 실제 가격은 이 값의
/// 1/3인 상품도 3배인 상품도 있다.
/// </summary>
public class AssumedLoosePriceTests
{
    // 환율 4,100 / 100리엘 단위
    private static ImportPriceFormat Format() =>
        new(ImportPriceCurrency.Riel, 4100m, rielRounding: 100);

    /// <summary>
    /// 리엘로 나누고 리엘로 <b>올린다</b>. 내림하면 낱개를 다 팔았을 때 박스가에
    /// 못 미친다 — 헐어 파는 값은 박스보다 조금 비싼 쪽이 맞다.
    /// </summary>
    [Fact]
    public void TheLoosePriceIsTheBoxShareRoundedUp()
    {
        // 41,000៛ 박스(= $10), 30정 → 1,366.7 → 1,400
        Assert.True(Format().TryAssumeLoosePrice(10m, 30, out var usd, out var riel));

        Assert.Equal(1400m, riel);
        Assert.Equal(0.3415m, usd);      // 1,400 / 4,100
    }

    /// <summary>올린 덕분에 낱개를 다 팔면 박스가보다 조금 더 들어온다.</summary>
    [Fact]
    public void SellingAWholeBoxLooseBringsInSlightlyMore()
    {
        Assert.True(Format().TryAssumeLoosePrice(10m, 30, out _, out var riel));

        Assert.True(riel * 30 > 10m * 4100m);          // 42,000 > 41,000
        Assert.Equal(42000m, riel * 30);
    }

    /// <summary>
    /// 100리엘이 하한이다. 그보다 작은 금액은 받을 방법이 없다.
    /// 싸고 개수 많은 박스는 이 때문에 낱개가 박스보다 크게 비싸진다 — 틀린 것이
    /// 아니라 동전이 없는 것이고, 그래서 미리보기가 건수를 알려 검토하게 한다.
    /// </summary>
    [Fact]
    public void TheFloorIsOneHundredRiel()
    {
        // 4,100៛ 박스(= $1), 100정 → 41 → 100
        Assert.True(Format().TryAssumeLoosePrice(1m, 100, out _, out var riel));

        Assert.Equal(100m, riel);
        Assert.Equal(10000m, riel * 100);              // 박스의 2.4배
    }

    [Theory]
    [InlineData(0, 30)]      // 박스가가 없다
    [InlineData(10, 1)]      // 헐 것이 없다
    [InlineData(10, 0)]
    public void NothingIsWorkedOutWhenThereIsNothingToDivide(decimal boxPrice, int unitsPerBox)
    {
        Assert.False(Format().TryAssumeLoosePrice(boxPrice, unitsPerBox, out _, out _));
    }

    /// <summary>환율이 없으면 리엘 단위로 올릴 수가 없다. 그러면 넣지 않는다.</summary>
    [Fact]
    public void NothingIsWorkedOutWithoutAnExchangeRate()
    {
        var noRate = new ImportPriceFormat(ImportPriceCurrency.Usd, 0m, rielRounding: 100);

        Assert.False(noRate.TryAssumeLoosePrice(10m, 30, out _, out _));
    }

    // ── 올림 자체 ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1366.7, 1400)]
    [InlineData(1400, 1400)]      // 이미 맞는 값은 그대로
    [InlineData(1400.01, 1500)]
    [InlineData(41, 100)]
    [InlineData(1, 100)]
    public void RoundUpToPayable_AlwaysGoesUp(decimal riel, decimal expected)
    {
        Assert.Equal(expected, RielConverter.RoundUpToPayable(riel, 100));
    }
}
