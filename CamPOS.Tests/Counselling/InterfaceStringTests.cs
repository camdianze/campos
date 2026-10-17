using PharmaPOS.Application.Counselling;

namespace PharmaPOS.Tests.Counselling;

/// <summary>
/// 화면 라벨과 인쇄물은 검수(review_status)를 다르게 다룬다.
///
/// 복약 문구는 환자가 그대로 따라 하는 글이라 검수 전에는 한 글자도 나가면 안 되지만,
/// 버튼에 적힌 낱말은 어색해도 계산대에서 바로 눈에 띄고 고치면 그만이다.
/// 이 차이가 무너지면 둘 중 하나가 조용히 틀린 쪽으로 간다.
/// </summary>
public class InterfaceStringTests
{
    private static CounsellingLocale CreateLocale(string reviewStatus) => new(
        localeCode: "km-KH",
        languageName: "ភាសាខ្មែរ",
        script: "Khmer",
        renderMode: LocaleRenderMode.Text,
        reviewStatus: reviewStatus,
        reviewedBy: null,
        contentVersion: "1.2.0",
        strings: new Dictionary<string, string>
        {
            ["ui.products"] = "ឱសថ",
            ["label.dose"] = "កម្រិតថ្នាំ",
            ["ui.blank"] = "   "
        });

    [Fact]
    public void PrintedStrings_StaySilentUntilReviewed()
    {
        Assert.Null(CreateLocale("pending").GetString("label.dose"));
        Assert.Equal("កម្រិតថ្នាំ", CreateLocale("approved").GetString("label.dose"));
    }

    /// <summary>화면 라벨은 검수 전에도 나온다. 대신 화면이 붉은 글씨로 보여 준다.</summary>
    [Theory]
    [InlineData("pending")]
    [InlineData("approved")]
    public void InterfaceStrings_ShowRegardlessOfReview(string reviewStatus)
    {
        Assert.Equal("ឱសថ", CreateLocale(reviewStatus).GetInterfaceString("ui.products"));
    }

    /// <summary>
    /// 없는 키는 null이어야 부르는 쪽이 영어로 되돌아간다.
    /// 키 이름이나 빈 칸이 버튼에 찍히면 그 화면은 아무도 읽을 수 없다.
    /// </summary>
    [Theory]
    [InlineData("ui.missing")]
    [InlineData("ui.blank")]
    public void InterfaceStrings_FallBackWhenMissingOrBlank(string key)
    {
        Assert.Null(CreateLocale("approved").GetInterfaceString(key));
    }

    [Fact]
    public void EnglishOnly_HasNoInterfaceStrings()
    {
        Assert.Null(CounsellingLocale.EnglishOnly.GetInterfaceString("ui.products"));
    }
}
