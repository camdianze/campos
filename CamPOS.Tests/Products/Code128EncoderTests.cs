using PharmaPOS.Application.Products;

namespace PharmaPOS.Tests.Products;

/// <summary>
/// Code 128-B 인코더. 틀려도 화면에서는 멀쩡한 막대로 보이고 스캐너에서만 조용히 실패하는
/// 종류의 코드라, 손으로 검산할 수 있는 값들을 고정해 둔다.
/// </summary>
public class Code128EncoderTests
{
    /// <summary>
    /// 검사문자는 (시작심볼 + Σ 자료심볼×자리번호) mod 103이다.
    /// "A"는 ASCII 65 → 심볼 33. (104 + 33×1) mod 103 = 137 mod 103 = 34.
    /// </summary>
    [Fact]
    public void ToSymbols_PutsStartDataCheckAndStopInOrder()
    {
        var symbols = Code128Encoder.ToSymbols("A");

        Assert.Equal(new[] { 104, 33, 34, 106 }, symbols);
    }

    /// <summary>자리번호는 1부터 올라간다. 두 번째 문자에 2가 곱해지는지 본다.</summary>
    [Fact]
    public void ToSymbols_WeighsEachCharacterByItsPosition()
    {
        // "AB" → 심볼 33, 34. (104 + 33×1 + 34×2) mod 103 = 205 mod 103 = 102.
        var symbols = Code128Encoder.ToSymbols("AB");

        Assert.Equal(new[] { 104, 33, 34, 102, 106 }, symbols);
    }

    /// <summary>심볼 값은 ASCII에서 32를 뺀 값이다. 공백이 0, 물결표가 94다.</summary>
    [Theory]
    [InlineData(" ", 0)]
    [InlineData("0", 16)]
    [InlineData("A", 33)]
    [InlineData("~", 94)]
    public void ToSymbols_MapsAsciiToSymbolValues(string text, int expected)
    {
        Assert.Equal(expected, Code128Encoder.ToSymbols(text)[1]);
    }

    /// <summary>
    /// 전체 모듈 수는 11n + 35다. 시작·자료·검사가 각 11모듈이고 정지만 13모듈이라
    /// 11×(1 + n + 1) + 13 = 11n + 35가 된다. 용지 폭에 맞춰 모듈 폭을 정할 때 이 값을 쓴다.
    /// </summary>
    [Theory]
    [InlineData("A", 46)]
    [InlineData("INT-00000146", 167)]
    [InlineData("INT-00000146-EA", 200)]
    public void TotalModules_IsElevenPerCharacterPlusThirtyFive(string code, int expected)
    {
        Assert.Equal(expected, Code128Encoder.TotalModules(code));
        Assert.Equal(11 * code.Length + 35, Code128Encoder.TotalModules(code));
    }

    /// <summary>
    /// 막대는 굵기 1~4모듈이고, 심볼 하나가 막대·공백 6개로 나뉜다(정지만 7개).
    /// 표가 한 자리라도 밀리면 여기서 걸린다.
    /// </summary>
    [Fact]
    public void ToModuleWidths_HasSixBarsPerSymbolAndSevenForStop()
    {
        var widths = Code128Encoder.ToModuleWidths("INT-00000146");

        // 시작 + 자료 12 + 검사 = 14심볼 × 6 + 정지 7 = 91.
        Assert.Equal(14 * 6 + 7, widths.Count);
        Assert.All(widths, w => Assert.InRange(w, 1, 4));
    }

    /// <summary>
    /// 실제로 쓰이는 두 형식이 담기는지. 내부 바코드는 영문자와 하이픈이 섞여 있어
    /// 숫자 전용 심볼로지로는 담을 수 없고, 그래서 B형을 골랐다.
    /// </summary>
    [Theory]
    [InlineData("INT-00000146")]
    [InlineData("INT-00000146-EA")]
    [InlineData("8801234567890")]
    public void CanEncode_AcceptsTheCodesThisAppActuallyPrints(string code)
    {
        Assert.True(Code128Encoder.CanEncode(code));
    }

    /// <summary>담을 수 없는 값은 그리기 전에 걸러야 한다. 크메르 문자가 상품명에 섞여 들어올 수 있다.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ក្រុម")]
    [InlineData("아목시실린")]
    public void CanEncode_RejectsWhatItCannotRepresent(string? code)
    {
        Assert.False(Code128Encoder.CanEncode(code));
    }

    [Fact]
    public void ToSymbols_ThrowsOnUnsupportedCharacters()
    {
        Assert.Throws<ArgumentException>(() => Code128Encoder.ToSymbols("아목시실린"));
    }
}
