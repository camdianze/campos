using System.Globalization;
using PharmaPOS.Application.Parsing;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// 숫자를 적고 읽는 규칙이 PC의 Windows 지역 설정에 끌려가면 안 된다.
///
/// 이 앱은 숫자를 전부 InvariantCulture로 <b>쓴다</b> — 상품 화면이 리엘을 달러로
/// 되돌릴 때, 영수증이 "0.00"으로 찍을 때, 임포트가 셀을 읽을 때. 그런데 화면의
/// ViewModel들은 <c>decimal.TryParse(text, out value)</c>로 <b>읽고</b> 있었고,
/// 그 호출은 현재 지역 설정을 쓴다.
///
/// 한국과 캄보디아 Windows는 소수점이 "."이라 둘이 우연히 같다. 그래서 이 어긋남은
/// 개발 PC에서는 절대 드러나지 않고, 지역 설정이 다른 PC에 설치했을 때에만 나타난다 —
/// 오류도 경고도 없이 가격만 달라진 채로.
/// </summary>
public class NumberInputCultureTests : IDisposable
{
    private readonly CultureInfo _original = CultureInfo.CurrentCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _original;

    /// <summary>소수점이 ","인 지역으로 PC가 설정된 상태.</summary>
    private static void PretendCommaDecimalPc() =>
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

    [Theory]
    [InlineData("19.5122", 19.5122)]
    [InlineData("0.9756", 0.9756)]
    [InlineData("20", 20)]
    [InlineData("4.53", 4.53)]
    public void ReadsTheSameValueOnACommaDecimalPc(string text, decimal expected)
    {
        PretendCommaDecimalPc();

        Assert.True(NumberInput.TryParseDecimal(text, out var value));
        Assert.Equal(expected, value);
    }

    /// <summary>
    /// 고치기 전에 무슨 일이 일어났는지 그대로 적어 둔다. 지역 설정이 ","인 PC에서는
    /// "."이 천 단위 구분자로 읽혀, $19.5122짜리 상품이 $195,122가 된다.
    /// 값이 숫자이고 0보다 커서 어떤 검사에도 걸리지 않는다.
    /// </summary>
    [Fact]
    public void ThePlainParseIsWhatWentWrong()
    {
        PretendCommaDecimalPc();

        Assert.True(decimal.TryParse("19.5122", out var mangled));
        Assert.Equal(195122m, mangled);

        Assert.True(NumberInput.TryParseDecimal("19.5122", out var correct));
        Assert.Equal(19.5122m, correct);
    }

    /// <summary>쓰는 쪽도 같은 규칙이어야 한다. 한쪽만 고치면 버그가 자리를 옮긴다.</summary>
    [Fact]
    public void WritesTheSameTextOnACommaDecimalPc()
    {
        PretendCommaDecimalPc();

        Assert.Equal("19.5122", NumberInput.ToText(19.5122m));
        Assert.Equal("20", NumberInput.ToText(20m));
    }

    /// <summary>칸에 적고 다시 읽어도 같은 값. 이것이 실제로 오가는 경로다.</summary>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("id-ID")]
    [InlineData("ko-KR")]
    [InlineData("km-KH")]
    [InlineData("en-US")]
    public void RoundTripsOnAnyPc(string culture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

        foreach (var value in new[] { 19.5122m, 0.9756m, 20m, 0.125m, 1234.5m })
        {
            Assert.True(NumberInput.TryParseDecimal(NumberInput.ToText(value), out var back));
            Assert.Equal(value, back);
        }
    }

    /// <summary>천 단위 쉼표는 받아 준다. 사람이 그렇게 적는다.</summary>
    [Fact]
    public void AcceptsAThousandsSeparator()
    {
        Assert.True(NumberInput.TryParseDecimal("80,000", out var value));
        Assert.Equal(80000m, value);
    }

    /// <summary>개수 칸에 소수점이 들어오면 개수가 아니다.</summary>
    [Fact]
    public void ACountWithADecimalPointIsRefused()
    {
        Assert.False(NumberInput.TryParseInt("2.5", out _));
        Assert.True(NumberInput.TryParseInt("30", out var count));
        Assert.Equal(30, count);
    }
}
