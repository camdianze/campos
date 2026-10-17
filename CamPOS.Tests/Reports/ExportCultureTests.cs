using System.Globalization;
using PharmaPOS.Application.Import;
using PharmaPOS.Application.Parsing;

namespace PharmaPOS.Tests.Reports;

/// <summary>
/// 파일로 나가는 숫자는 PC의 지역 설정을 따라가면 안 된다.
///
/// CSV는 쉼표로 열을 나눈다. 소수점이 ","인 지역(프랑스·독일·인도네시아·베트남 등)으로
/// 설정된 PC에서 숫자를 그 PC 방식으로 쓰면 <b>값 안에 쉼표가 생겨 열이 통째로 밀린다.</b>
/// 받는 쪽은 열이 하나 늘어난 줄을 보게 되고, 그게 항생제 제출 파일이면 연구기관이
/// 읽을 수 없는 파일을 받는다.
///
/// 상품 파일은 더 나쁘다. 고쳐서 그대로 다시 넣으라고 만든 파일인데(CLAUDE.md),
/// 임포트는 언제나 "." 규칙으로 읽으므로 "19,5122"를 195122로 삼킨다 — 오류 없이,
/// 만 배가 된 가격으로.
/// </summary>
public class ExportCultureTests : IDisposable
{
    private readonly CultureInfo _original = CultureInfo.CurrentCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _original;

    private static void PretendCommaDecimalPc() =>
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// 고치기 전에 무슨 일이 일어났는지 그대로 적어 둔다.
    /// 보간 문자열은 지역 설정을 쓴다 — 그래서 CSV 한 줄에 열이 하나 더 생겼다.
    /// </summary>
    [Fact]
    public void PlainInterpolationIsWhatBrokeTheColumns()
    {
        PretendCommaDecimalPc();

        var amount = 19.5122m;

        var broken = $"Amoxicillin,{amount},30";
        var fixedLine = string.Create(CultureInfo.InvariantCulture, $"Amoxicillin,{amount},30");

        Assert.Equal(4, broken.Split(',').Length);        // 열이 하나 늘었다
        Assert.Equal(3, fixedLine.Split(',').Length);
        Assert.Contains("19,5122", broken);
        Assert.Contains("19.5122", fixedLine);
    }

    /// <summary>
    /// 내보낸 값을 임포트가 그대로 되읽을 수 있어야 한다. 상품 파일의 존재 이유다.
    /// </summary>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("id-ID")]
    [InlineData("ko-KR")]
    [InlineData("en-US")]
    public void AnExportedPriceReadsBackAsTheSameNumber(string culture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

        foreach (var price in new[] { 19.5122m, 0.9756m, 20m, 4.53m, 1234.5m })
        {
            var written = string.Create(CultureInfo.InvariantCulture, $"{price}");

            Assert.DoesNotContain(",", written);   // 쉼표가 생기면 CSV 열이 밀린다

            var format = new ImportPriceFormat(ImportPriceCurrency.Usd, 4000m);
            Assert.True(format.TryRead(written, out var readBack, out _));
            Assert.Equal(price, readBack);
        }
    }

    /// <summary>
    /// 날짜도 같다. 불교력을 쓰는 지역(태국)에서는 연도가 543 더해져 나가고,
    /// 그 값은 "미래 날짜"라 유효기간 검사를 그대로 통과한다 — 543년 뒤에 만료되는 재고.
    /// </summary>
    [Fact]
    public void ADateDoesNotPickUpTheLocalCalendar()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");

        var date = new DateTime(2026, 12, 12);

        Assert.Equal("2026-12-12", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        // 지역 설정을 그대로 쓰면 어떻게 되는지 기록으로 남긴다.
        Assert.NotEqual("2026-12-12", date.ToString("yyyy-MM-dd"));
    }

    /// <summary>칸에 적는 숫자와 파일에 적는 숫자는 같은 규칙이어야 한다.</summary>
    [Fact]
    public void TheScreenAndTheFileAgree()
    {
        PretendCommaDecimalPc();

        var price = 19.5122m;

        Assert.Equal(
            NumberInput.ToText(price),
            string.Create(CultureInfo.InvariantCulture, $"{price}"));
    }
}
