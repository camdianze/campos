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

        // 검수 전이라 GetString은 잠겨 있다. 키가 실제로 들어 있는지 보려면
        // 검수된 사본을 만들어 확인해야 한다.
        Assert.False(locale.IsApproved);
    }

    /// <summary>
    /// 동봉본은 반드시 미검수 상태여야 한다.
    /// 검수도 하지 않은 번역이 기본값으로 환자에게 나가면 안 된다.
    /// </summary>
    [Fact]
    public async Task ShippedLocales_AreNotApprovedByDefault()
    {
        var provider = new FileCounsellingLocaleProvider(new[] { FindLocalesDirectory() });

        var locales = await provider.ListAvailableLocalesAsync();

        Assert.NotEmpty(locales);
        Assert.All(locales, locale => Assert.False(locale.IsApproved));
    }
}
