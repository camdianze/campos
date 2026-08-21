using PharmaPOS.Application.Receipts;
using PharmaPOS.Application.Settings;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Receipts;

/// <summary>
/// 설정 읽기·저장 규칙. 읽기는 어떤 이유로도 실패하지 않아야 하고,
/// 저장은 관리자가 아니면 통과하지 않아야 한다.
/// </summary>
public class ReceiptSettingsServiceTests
{
    private static (ReceiptSettingsService Service, FakeAppSettingRepository Repository) Build(
        FakeAppSettingRepository? repository = null, ReceiptSettingsCache? cache = null)
    {
        repository ??= new FakeAppSettingRepository();

        return (new ReceiptSettingsService(repository, cache ?? new ReceiptSettingsCache()), repository);
    }

    private static ReceiptSettings Valid() => new()
    {
        ShopNameKm = "ឱសថស្ថាន",
        ShopNameEn = "Sample Pharmacy",
        ReceiptPrefix = "sp01",
        ExchangeRate = 4200m
    };

    // ── 읽기 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsCodeDefaultsWhenNothingIsStored()
    {
        var (service, _) = Build();

        var settings = await service.GetAsync();

        Assert.Equal(ReceiptPrintLanguage.KhmerAndEnglish, settings.PrintLanguage);
        Assert.Equal(ReceiptPaperWidth.Mm80, settings.PaperWidth);
        Assert.Equal(ReceiptNumberResetCycle.Daily, settings.ResetCycle);
        Assert.True(settings.ShowRiel);
        Assert.Equal(100, settings.RielRounding);
        Assert.False(settings.VatEnabled);

        // 약국 정보에는 기본값이 없다. 있으면 남의 약국 정보가 코드에 박힌다.
        Assert.Equal(string.Empty, settings.ShopNameEn);
    }

    [Fact]
    public async Task GetAsync_FallsBackPerKeyWhenAValueIsUnreadable()
    {
        var repository = new FakeAppSettingRepository();
        repository.Seed(AppSettingKeys.PrintWidth, "nonsense");
        repository.Seed(AppSettingKeys.CurrencyRate, "not a number");
        repository.Seed(AppSettingKeys.CurrencyRounding, "37");
        repository.Seed(AppSettingKeys.ShopNameEn, "Sample Pharmacy");

        var (service, _) = Build(repository);

        var settings = await service.GetAsync();

        // 깨진 값만 기본값으로 떨어지고 멀쩡한 값은 살아남는다.
        Assert.Equal(ReceiptPaperWidth.Mm80, settings.PaperWidth);
        Assert.Equal(4100m, settings.ExchangeRate);
        Assert.Equal(100, settings.RielRounding);
        Assert.Equal("Sample Pharmacy", settings.ShopNameEn);
    }

    /// <summary>
    /// 저장소 자체가 터져도 예외가 밖으로 나가면 안 된다.
    /// 설정을 못 읽는 것과 영수증을 못 내는 것은 다른 이야기다.
    /// </summary>
    [Fact]
    public async Task GetAsync_DoesNotThrowWhenTheStoreFails()
    {
        var repository = new FakeAppSettingRepository { FailReads = true };
        var (service, _) = Build(repository);

        var settings = await service.GetAsync();

        Assert.Equal(ReceiptPrintLanguage.KhmerAndEnglish, settings.PrintLanguage);
    }

    /// <summary>빈 값도 저장된 값이다. 지운 주소가 기본값으로 되살아나면 안 된다.</summary>
    [Fact]
    public async Task GetAsync_KeepsDeliberatelyEmptyValues()
    {
        var repository = new FakeAppSettingRepository();
        repository.Seed(AppSettingKeys.ReceiptFooterEn, string.Empty);

        var (service, _) = Build(repository);

        Assert.Equal(string.Empty, (await service.GetAsync()).FooterEn);
    }

    // ── 캐시 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReadsTheStoreOnlyOnceWhileTheCacheIsWarm()
    {
        var cache = new ReceiptSettingsCache();
        var (service, repository) = Build(cache: cache);

        await service.GetAsync();
        await service.GetAsync();
        await service.GetAsync();

        Assert.Equal(1, repository.ReadCount);
    }

    [Fact]
    public async Task SaveAsync_MakesTheNewValuesVisibleImmediately()
    {
        var cache = new ReceiptSettingsCache();
        var (service, _) = Build(cache: cache);

        await service.GetAsync();

        var settings = Valid();
        settings.ShopNameEn = "New Name";

        await service.SaveAsync(settings, UserRole.Administrator, "user-1");

        Assert.Equal("New Name", (await service.GetAsync()).ShopNameEn);
    }

    // ── 저장 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_RefusesAnyoneWhoIsNotAnAdministrator()
    {
        var (service, repository) = Build();

        var result = await service.SaveAsync(Valid(), UserRole.FacilityStaff, "user-2");

        Assert.False(result.IsSuccess);
        Assert.Contains("Administrator", result.Message);

        // 화면에서 막는 것과 별개로, 저장소에 아무것도 쓰이지 않아야 한다.
        Assert.Empty(repository.Audit);
    }

    [Fact]
    public async Task SaveAsync_ReturnsPerFieldErrors()
    {
        var (service, repository) = Build();

        var settings = Valid();
        settings.ShopNameEn = string.Empty;
        settings.ExchangeRate = 0m;

        var result = await service.SaveAsync(settings, UserRole.Administrator, "user-1");

        Assert.False(result.IsSuccess);
        Assert.Contains(AppSettingKeys.ShopNameEn, result.FieldErrors.Keys);
        Assert.Contains(AppSettingKeys.CurrencyRate, result.FieldErrors.Keys);
        Assert.Empty(repository.Audit);
    }

    /// <summary>소문자 접두어는 되돌려 보내지 않고 대문자로 고쳐 받는다.</summary>
    [Fact]
    public async Task SaveAsync_UppercasesThePrefix()
    {
        var (service, _) = Build();

        var result = await service.SaveAsync(Valid(), UserRole.Administrator, "user-1");

        Assert.True(result.IsSuccess);
        Assert.Equal("SP01", (await service.GetAsync()).ReceiptPrefix);
    }

    [Fact]
    public async Task SaveAsync_WritesEveryKeyWithItsTypeAndAuthor()
    {
        var (service, repository) = Build();

        await service.SaveAsync(Valid(), UserRole.Administrator, "user-1");

        Assert.Equal(AppSettingKeys.ReceiptSettingKeys.Count, repository.Audit.Count);
        Assert.All(repository.Audit.Values, entry => Assert.Equal("user-1", entry.UpdatedBy));

        Assert.Equal("enum", repository.Audit[AppSettingKeys.PrintLanguage].Type);
        Assert.Equal("bool", repository.Audit[AppSettingKeys.VatEnabled].Type);
        Assert.Equal("number", repository.Audit[AppSettingKeys.CurrencyRate].Type);
        Assert.Equal("text", repository.Audit[AppSettingKeys.ShopNameEn].Type);
    }

    /// <summary>명세가 정한 저장 표기를 지킨다. 한번 저장된 값은 그대로 남는다.</summary>
    [Fact]
    public async Task SaveAsync_UsesTheAgreedStorageCodes()
    {
        var (service, repository) = Build();

        var settings = Valid();
        settings.PrintLanguage = ReceiptPrintLanguage.Khmer;
        settings.PaperWidth = ReceiptPaperWidth.Mm58;
        settings.ResetCycle = ReceiptNumberResetCycle.Monthly;

        await service.SaveAsync(settings, UserRole.Administrator, "user-1");

        Assert.Equal("km", await repository.GetAsync(AppSettingKeys.PrintLanguage));
        Assert.Equal("58", await repository.GetAsync(AppSettingKeys.PrintWidth));
        Assert.Equal("monthly", await repository.GetAsync(AppSettingKeys.ReceiptResetCycle));
        Assert.Equal("true", await repository.GetAsync(AppSettingKeys.CurrencyShowRiel));
    }

    [Fact]
    public async Task SaveAsync_ReportsAFailedWriteWithoutThrowing()
    {
        var repository = new FakeAppSettingRepository { FailWrites = true };
        var (service, _) = Build(repository);

        var result = await service.SaveAsync(Valid(), UserRole.Administrator, "user-1");

        Assert.False(result.IsSuccess);
        Assert.Contains("could not be saved", result.Message);
    }
}
