using PharmaPOS.Application.Counselling;

namespace PharmaPOS.Tests.Counselling;

public class AntibioticNameNormalizerTests
{
    /// <summary>
    /// 정규화의 핵심 성질: 같은 성분을 가리키는 서로 다른 표기가 하나로 모여야 한다.
    /// 상품 쪽 이름과 시드 쪽 이름에 같은 함수를 적용하므로, 이 성질이 곧 매칭 정확도다.
    /// </summary>
    [Theory]
    // 염·수화물 형태 제거
    [InlineData("Azithromycin dihydrate", "azithromycin")]
    [InlineData("Azithromycin monohydrate", "azithromycin")]
    [InlineData("Amoxicillin trihydrate", "amoxicillin")]
    [InlineData("Cefazolin sodium", "cefazolin")]
    [InlineData("Amoxicillin sodium", "amoxicillin")]
    [InlineData("Clindamycin hydrochloride", "clindamycin")]
    [InlineData("Benzylpenicillin potassium", "benzylpenicillin")]
    [InlineData("Gentamicin sulfate", "gentamicin")]
    [InlineData("Doxycycline hyclate", "doxycycline")]
    // 대소문자·공백·하이픈
    [InlineData("AMOXICILLIN", "amoxicillin")]
    [InlineData("  Amoxicillin  ", "amoxicillin")]
    [InlineData("Sulfamethoxazole-trimethoprim", "sulfamethoxazoletrimethoprim")]
    // 용량 표기
    [InlineData("Amoxicillin 500mg", "amoxicillin")]
    [InlineData("Amoxicillin 500 mg", "amoxicillin")]
    public void Normalize_CollapsesFormattingVariants(string input, string expected)
    {
        Assert.Equal(expected, AntibioticNameNormalizer.Normalize(input));
    }

    /// <summary>수용 기준: Cephalexin / Cefalexin 양쪽 모두 매칭돼야 한다.</summary>
    [Theory]
    [InlineData("Cephalexin", "Cefalexin")]
    [InlineData("Cephradine", "Cefradine")]
    [InlineData("Cephazolin", "Cefazolin")]
    [InlineData("Sulphamethoxazole", "Sulfamethoxazole")]
    public void Normalize_UnifiesSpellingVariants(string variantA, string variantB)
    {
        Assert.Equal(
            AntibioticNameNormalizer.Normalize(variantA),
            AntibioticNameNormalizer.Normalize(variantB));
    }

    /// <summary>
    /// 접두사만 통합해야 한다. 단어 중간의 ph까지 f로 바꾸면
    /// 관계없는 성분끼리 같은 값으로 뭉개진다.
    /// </summary>
    [Fact]
    public void Normalize_DoesNotTouchPhInsideWord()
    {
        Assert.Equal("chloramphenicol", AntibioticNameNormalizer.Normalize("Chloramphenicol"));
    }

    /// <summary>
    /// 성분명의 일부인 한 글자를 용량 단위로 오인하면 안 된다.
    /// "Penicillin G"의 G를 그램으로 보고 버리면 다른 성분이 돼버린다.
    /// 그래서 단위는 바로 앞이 숫자였을 때만 용량 표기로 친다.
    /// </summary>
    [Theory]
    [InlineData("Penicillin G", "penicilling")]
    [InlineData("Polymyxin B", "polymyxinb")]
    [InlineData("Amoxicillin 1 g", "amoxicillin")]
    [InlineData("Amoxicillin 1g", "amoxicillin")]
    public void Normalize_TreatsUnitAsDoseOnlyAfterANumber(string input, string expected)
    {
        Assert.Equal(expected, AntibioticNameNormalizer.Normalize(input));
    }

    /// <summary>서로 다른 성분이 같은 값으로 뭉개지면 안 된다.</summary>
    [Theory]
    [InlineData("Amoxicillin", "Ampicillin")]
    [InlineData("Cefazolin", "Cefixime")]
    [InlineData("Azithromycin", "Erythromycin")]
    public void Normalize_KeepsDifferentAgentsDistinct(string a, string b)
    {
        Assert.NotEqual(
            AntibioticNameNormalizer.Normalize(a),
            AntibioticNameNormalizer.Normalize(b));
    }

    /// <summary>
    /// 복합제는 구분자 표기가 제각각이라(슬래시/더하기/하이픈) 하나로 모아야 한다.
    /// </summary>
    [Theory]
    [InlineData("Amoxicillin/clavulanic acid")]
    [InlineData("Amoxicillin + clavulanic acid")]
    [InlineData("amoxicillin-clavulanic acid")]
    public void Normalize_CollapsesCombinationSeparators(string input)
    {
        Assert.Equal("amoxicillinclavulanicacid", AntibioticNameNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ReturnsEmptyForBlankInput(string? input)
    {
        Assert.Equal(string.Empty, AntibioticNameNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("j01ca04", "J01CA04")]
    [InlineData(" J01CA04 ", "J01CA04")]
    [InlineData("J01.CA.04", "J01CA04")]
    [InlineData(null, "")]
    public void NormalizeAtcCode_UppercasesAndStripsPunctuation(string? input, string expected)
    {
        Assert.Equal(expected, AntibioticNameNormalizer.NormalizeAtcCode(input));
    }
}
