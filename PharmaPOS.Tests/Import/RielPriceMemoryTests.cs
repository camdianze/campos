using PharmaPOS.Application.Import;

namespace PharmaPOS.Tests.Import;

/// <summary>
/// 리엘로 정한 가격은 그 금액을 그대로 기억한다.
///
/// 이 규칙이 생긴 이유는 실제로 겪은 일이다. 8,000리엘로 적은 상품을 환율 4,100에서
/// 임포트하면 $1.9512가 저장된다. 나중에 환율을 4,000으로 바꾸면 화면이 그 달러를
/// 되돌려 <b>7,800리엘</b>이라고 말한다 — 아무도 가격을 건드리지 않았는데 200리엘이
/// 움직인 것이다. 100리엘 단위로 반올림하는 나라에서 적은 차이가 아니고, 무엇보다
/// 어느 숫자가 원래 값이었는지 되짚을 방법이 없었다.
///
/// 달러만 저장하면 약국이 정한 값이 사라진다. 그래서 적어 넣은 리엘 금액을 함께 남긴다.
/// </summary>
public class RielPriceMemoryTests
{
    /// <summary>8,000 ÷ 4,100 = 1.9512. 실제로 나온 값이다.</summary>
    [Fact]
    public void ARielPrice_KeepsTheAmountThatWasWritten()
    {
        var format = new ImportPriceFormat(ImportPriceCurrency.Riel, 4100m);

        Assert.True(format.TryRead("8000", out var usd, out var riel, out _));

        Assert.Equal(1.9512m, usd);
        Assert.Equal(8000m, riel);
    }

    /// <summary>낱개가도 같다. 4,000 ÷ 4,100 = 0.9756.</summary>
    [Fact]
    public void ALooseRielPrice_KeepsItsAmountToo()
    {
        var format = new ImportPriceFormat(ImportPriceCurrency.Riel, 4100m);

        Assert.True(format.TryRead("4000", out var usd, out var riel, out _));

        Assert.Equal(0.9756m, usd);
        Assert.Equal(4000m, riel);
    }

    /// <summary>칸에 표시를 붙여 적은 경우도 같다.</summary>
    [Theory]
    [InlineData("8000 KHR")]
    [InlineData("8,000 KHR")]
    [InlineData("8000៛")]
    [InlineData("៛8,000")]
    public void AMarkedRielCell_KeepsItsAmount(string cell)
    {
        var format = new ImportPriceFormat(ImportPriceCurrency.Usd, 4100m);

        Assert.True(format.TryRead(cell, out var usd, out var riel, out _));

        Assert.Equal(1.9512m, usd);
        Assert.Equal(8000m, riel);
    }

    /// <summary>달러로 적은 가격은 리엘 금액이 없다. 지어내면 안 된다.</summary>
    [Theory]
    [InlineData("1.9512")]
    [InlineData("$1.9512")]
    public void ADollarPrice_HasNoRielAmount(string cell)
    {
        var format = new ImportPriceFormat(ImportPriceCurrency.Usd, 4100m);

        Assert.True(format.TryRead(cell, out var usd, out var riel, out _));

        Assert.Equal(1.9512m, usd);
        Assert.Null(riel);
    }

    // ── 낼 수 있는 금액으로 정해진다 ────────────────────────────────────────
    //
    // 캄보디아는 100리엘 미만 동전이 돌지 않는다. 4,037리엘짜리 가격은 정할 수가 없고,
    // 그런 값을 저장해 두면 계산대에서 부르는 금액과 정해 둔 가격이 언제나 어긋난다.

    [Theory]
    [InlineData("4000", 4000)]     // 이미 맞는 값은 그대로
    [InlineData("4037", 4000)]
    [InlineData("4060", 4100)]
    [InlineData("8000", 8000)]
    [InlineData("7950", 8000)]
    public void ARielPrice_IsRoundedToWhatCanBePaid(string cell, decimal expected)
    {
        var format = new ImportPriceFormat(ImportPriceCurrency.Riel, 4100m, rielRounding: 100);

        Assert.True(format.TryRead(cell, out _, out var riel, out _));
        Assert.Equal(expected, riel);
    }

    /// <summary>
    /// 맞춘 <b>뒤에</b> 환산한다. 순서가 반대면 달러가 낼 수 없는 리엘 금액에서
    /// 나온 값이 되고, 계산대가 그 달러를 되돌릴 때 정해 둔 가격과 달라진다.
    /// </summary>
    [Fact]
    public void TheDollarComesFromTheRoundedRiel()
    {
        var format = new ImportPriceFormat(ImportPriceCurrency.Riel, 4100m, rielRounding: 100);

        Assert.True(format.TryRead("4037", out var usd, out var riel, out _));

        Assert.Equal(4000m, riel);
        Assert.Equal(0.9756m, usd);        // 4,000 / 4,100 — 4,037이 아니다
    }

    /// <summary>
    /// 환율이 그대로인 동안에는 손님이 정확히 정해 둔 금액을 낸다.
    /// 계산대는 달러 합계를 리엘로 되돌려 100단위로 맞추는데, 가격이 이미 100단위라
    /// 그 되돌림이 원래 값으로 떨어진다.
    /// </summary>
    [Theory]
    [InlineData(1, 4000)]
    [InlineData(3, 12000)]
    [InlineData(10, 40000)]
    public void AtTheSameRate_TheCustomerPaysExactlyThePriceThatWasSet(int quantity, long expected)
    {
        var format = new ImportPriceFormat(ImportPriceCurrency.Riel, 4100m, rielRounding: 100);
        Assert.True(format.TryRead("4000", out var usd, out _, out _));

        // 줄 금액은 센트에서 끊긴다(SaleLineItem과 같은 규칙).
        var lineTotal = decimal.Round(usd * quantity, 2, MidpointRounding.AwayFromZero);

        Assert.Equal(expected, PharmaPOS.Application.Receipts.RielConverter.ToRiel(lineTotal, 4100m, 100));
    }

    /// <summary>
    /// 이것이 문제의 핵심이다. 저장된 달러를 오늘 환율로 되돌리면 적어 넣은 값과
    /// 달라진다 — 그래서 되돌리지 않고 적어 넣은 값을 보여주는 것이다.
    /// </summary>
    [Fact]
    public void ConvertingBackAtANewRateDoesNotGiveTheOriginalAmount()
    {
        var atImport = new ImportPriceFormat(ImportPriceCurrency.Riel, 4100m);
        Assert.True(atImport.TryRead("8000", out var stored, out var entered, out _));

        // 환율을 4,000으로 바꾼 뒤 화면이 되돌려 계산하면
        var shownLater = decimal.Round(stored * 4000m, 0);

        Assert.Equal(8000m, entered);
        Assert.Equal(7805m, shownLater);           // 100단위 반올림 전
        Assert.NotEqual(entered, shownLater);      // 약국이 정한 값과 다르다
    }
}
