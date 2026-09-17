using PharmaPOS.Application.Counselling;

namespace PharmaPOS.Tests.Counselling;

/// <summary>
/// 저장소에 동봉된 실제 로케일 파일이 형식에 맞는지 확인한다.
/// 파일을 손으로 고치다 JSON이 깨지면 런타임에는 조용히 영어로만 나가버려서
/// 알아채기 어렵기 때문에, 빌드 단계에서 잡히도록 테스트로 둔다.
/// </summary>
public class ShippedLocaleFileTests
{
    private static string FindLocalesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PharmaPOS.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "PharmaPOS.Wpf", "locales");
    }

    [Fact]
    public async Task ShippedKhmerLocale_ParsesAndCarriesEveryKey()
    {
        var provider = new FileCounsellingLocaleProvider(new[] { FindLocalesDirectory() });

        var locale = await provider.GetLocaleAsync("km-KH");

        Assert.Equal("km-KH", locale.LocaleCode);
        Assert.Equal(LocaleRenderMode.Raster, locale.RenderMode);

        // 검수를 마친 동봉본이다. 그래서 GetString이 실제 값을 돌려준다.
        Assert.True(locale.IsApproved);
        Assert.Equal("កម្រិតថ្នាំ", locale.GetString(CounsellingStringKeys.LabelDose));
    }

    /// <summary>
    /// 승인된 동봉본은 누가 검수했는지 적혀 있어야 한다.
    ///
    /// 전에는 "동봉본은 반드시 미검수"였다 — 검수도 안 한 번역이 기본값으로 환자에게
    /// 나가면 안 되니까. 검수를 마치고 승인하면서 그 규칙은 끝났지만, 취지는 남긴다:
    /// 이름 없이 approved만 켜 두면 몇 달 뒤 누구도 그 번역을 책임지지 않는다.
    /// </summary>
    [Fact]
    public async Task ApprovedShippedLocales_NameTheirReviewer()
    {
        var provider = new FileCounsellingLocaleProvider(new[] { FindLocalesDirectory() });

        var locales = await provider.ListAvailableLocalesAsync();

        Assert.NotEmpty(locales);
        Assert.All(locales, locale =>
        {
            if (locale.IsApproved)
            {
                Assert.False(string.IsNullOrWhiteSpace(locale.ReviewedBy),
                    $"{locale.LocaleCode} is approved but reviewed_by is empty.");
            }
        });
    }
}
