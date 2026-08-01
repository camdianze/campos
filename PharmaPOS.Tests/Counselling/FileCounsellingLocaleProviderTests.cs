using PharmaPOS.Application.Counselling;

namespace PharmaPOS.Tests.Counselling;

/// <summary>
/// 로케일 로더 테스트. 임시 폴더에 실제 파일을 써서 읽힌다.
/// </summary>
public class FileCounsellingLocaleProviderTests : IDisposable
{
    private readonly string _primaryDirectory;
    private readonly string _fallbackDirectory;

    public FileCounsellingLocaleProviderTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "pharmapos-locale-tests", Guid.NewGuid().ToString());
        _primaryDirectory = Path.Combine(root, "appdata");
        _fallbackDirectory = Path.Combine(root, "install");

        Directory.CreateDirectory(_primaryDirectory);
        Directory.CreateDirectory(_fallbackDirectory);
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_primaryDirectory);

        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private FileCounsellingLocaleProvider BuildProvider()
        => new(new[] { _primaryDirectory, _fallbackDirectory });

    private void WriteLocaleFile(string directory, string fileName, string json)
        => File.WriteAllText(Path.Combine(directory, fileName), json);

    private static string BuildLocaleJson(
        string reviewStatus = "approved",
        string? subtitle = "ការណែនាំប្រើថ្នាំអង់ទីប៊ីយោទិច",
        string? dose = "កម្រិតថ្នាំ")
    {
        var strings = new List<string>();

        if (subtitle is not null)
        {
            strings.Add($"\"sheet.subtitle\": \"{subtitle}\"");
        }

        if (dose is not null)
        {
            strings.Add($"\"label.dose\": \"{dose}\"");
        }

        return $$"""
            {
              "locale": "km-KH",
              "language_name": "ភាសាខ្មែរ",
              "script": "Khmer",
              "render_mode": "raster",
              "review_status": "{{reviewStatus}}",
              "reviewed_by": null,
              "content_version": "1.0.0",
              "strings": { {{string.Join(", ", strings)}} }
            }
            """;
    }

    /// <summary>수용 기준: review_status가 approved면 현지어가 나온다.</summary>
    [Fact]
    public async Task GetLocaleAsync_ReturnsLocalStringsWhenApproved()
    {
        WriteLocaleFile(_primaryDirectory, "km-KH.json", BuildLocaleJson(reviewStatus: "approved"));

        var locale = await BuildProvider().GetLocaleAsync("km-KH");

        Assert.True(locale.IsApproved);
        Assert.Equal("km-KH", locale.LocaleCode);
        Assert.Equal(LocaleRenderMode.Raster, locale.RenderMode);
        Assert.Equal("ការណែនាំប្រើថ្នាំអង់ទីប៊ីយោទិច", locale.GetString(CounsellingStringKeys.SheetSubtitle));
    }

    /// <summary>
    /// 수용 기준: review_status가 pending이면 현지어를 내보내지 않는다.
    /// 파일을 읽는 것 자체는 성공하되, 문구만 잠긴다.
    /// </summary>
    [Fact]
    public async Task GetLocaleAsync_WithholdsStringsWhenNotApproved()
    {
        WriteLocaleFile(_primaryDirectory, "km-KH.json", BuildLocaleJson(reviewStatus: "pending"));

        var locale = await BuildProvider().GetLocaleAsync("km-KH");

        Assert.False(locale.IsApproved);
        Assert.Equal("km-KH", locale.LocaleCode);
        Assert.Null(locale.GetString(CounsellingStringKeys.SheetSubtitle));
    }

    /// <summary>수용 기준: 키 하나가 빠져도 그 줄만 영어로 떨어지고 나머지는 정상이다.</summary>
    [Fact]
    public async Task GetLocaleAsync_MissingKeyAffectsOnlyThatLine()
    {
        WriteLocaleFile(_primaryDirectory, "km-KH.json", BuildLocaleJson(subtitle: null));

        var locale = await BuildProvider().GetLocaleAsync("km-KH");

        Assert.Null(locale.GetString(CounsellingStringKeys.SheetSubtitle));
        Assert.Equal("កម្រិតថ្នាំ", locale.GetString(CounsellingStringKeys.LabelDose));
    }

    /// <summary>값이 공백뿐이면 없는 것으로 본다. 빈칸이 용지에 찍히면 안 된다.</summary>
    [Fact]
    public async Task GetLocaleAsync_TreatsWhitespaceValueAsMissing()
    {
        WriteLocaleFile(_primaryDirectory, "km-KH.json", BuildLocaleJson(subtitle: "   "));

        var locale = await BuildProvider().GetLocaleAsync("km-KH");

        Assert.Null(locale.GetString(CounsellingStringKeys.SheetSubtitle));
    }

    [Fact]
    public async Task GetLocaleAsync_FallsBackToEnglishWhenFileIsMissing()
    {
        var locale = await BuildProvider().GetLocaleAsync("lo-LA");

        Assert.False(locale.IsApproved);
        Assert.Null(locale.GetString(CounsellingStringKeys.SheetSubtitle));
    }

    /// <summary>JSON이 깨져 있어도 예외를 던지지 않는다.</summary>
    [Fact]
    public async Task GetLocaleAsync_FallsBackToEnglishWhenJsonIsMalformed()
    {
        WriteLocaleFile(_primaryDirectory, "km-KH.json", "{ this is not json");

        var locale = await BuildProvider().GetLocaleAsync("km-KH");

        Assert.Null(locale.GetString(CounsellingStringKeys.SheetSubtitle));
    }

    [Fact]
    public async Task GetLocaleAsync_ReturnsEnglishOnlyForBlankCode()
    {
        var locale = await BuildProvider().GetLocaleAsync(null);

        Assert.Same(CounsellingLocale.EnglishOnly, locale);
    }

    /// <summary>설정값으로 들어오는 값이라 경로 조작이 통하면 안 된다.</summary>
    [Theory]
    [InlineData("../../secrets")]
    [InlineData("km-KH/../../etc/passwd")]
    public async Task GetLocaleAsync_RejectsPathTraversalInLocaleCode(string localeCode)
    {
        var locale = await BuildProvider().GetLocaleAsync(localeCode);

        Assert.Same(CounsellingLocale.EnglishOnly, locale);
    }

    /// <summary>현장 교체본(%APPDATA%)이 설치 폴더 동봉본을 이긴다.</summary>
    [Fact]
    public async Task GetLocaleAsync_PrefersFirstDirectory()
    {
        WriteLocaleFile(_fallbackDirectory, "km-KH.json", BuildLocaleJson(reviewStatus: "pending"));
        WriteLocaleFile(_primaryDirectory, "km-KH.json", BuildLocaleJson(reviewStatus: "approved"));

        var locale = await BuildProvider().GetLocaleAsync("km-KH");

        Assert.True(locale.IsApproved);
    }

    [Fact]
    public async Task ListAvailableLocalesAsync_ReturnsEachCodeOnce()
    {
        WriteLocaleFile(_fallbackDirectory, "km-KH.json", BuildLocaleJson(reviewStatus: "pending"));
        WriteLocaleFile(_primaryDirectory, "km-KH.json", BuildLocaleJson(reviewStatus: "approved"));

        var locales = await BuildProvider().ListAvailableLocalesAsync();

        var locale = Assert.Single(locales);
        Assert.Equal("km-KH", locale.LocaleCode);
        Assert.True(locale.IsApproved);
    }

    [Fact]
    public async Task ListAvailableLocalesAsync_ReturnsEmptyWhenNoFilesInstalled()
    {
        Assert.Empty(await BuildProvider().ListAvailableLocalesAsync());
    }
}
