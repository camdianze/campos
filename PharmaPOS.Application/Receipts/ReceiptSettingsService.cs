using System.Globalization;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Settings;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Receipts;

/// <summary>
/// IReceiptSettingsService의 구현체.
///
/// 읽기는 절대 실패하지 않는다. 값이 없거나 숫자로 못 읽히면 그 항목만
/// 기본값으로 떨어지고 나머지는 그대로 쓴다 — 설정 하나가 깨졌다고
/// 영수증 전체를 포기하는 쪽이 더 나쁘다.
/// </summary>
public class ReceiptSettingsService : IReceiptSettingsService
{
    /// <summary>App_Setting.value_type에 남기는 값의 종류.</summary>
    private const string TypeText = "text";
    private const string TypeEnum = "enum";
    private const string TypeBool = "bool";
    private const string TypeNumber = "number";

    private readonly IAppSettingRepository _settingRepository;
    private readonly ReceiptSettingsCache _cache;

    public ReceiptSettingsService(IAppSettingRepository settingRepository, ReceiptSettingsCache cache)
    {
        _settingRepository = settingRepository;
        _cache = cache;
    }

    public async Task<ReceiptSettings> GetAsync()
    {
        var cached = _cache.TryGet();

        if (cached is not null)
        {
            return cached;
        }

        var settings = new ReceiptSettings();

        IReadOnlyDictionary<string, string> stored;

        try
        {
            stored = await _settingRepository.GetManyAsync(AppSettingKeys.ReceiptSettingKeys);
        }
        catch (Exception)
        {
            // DB를 못 읽었다. 기본값으로 인쇄하되 캐시에는 넣지 않는다 —
            // 다음 호출에서 다시 시도해야 한다.
            return settings;
        }

        settings.ShopNameKm = Text(stored, AppSettingKeys.ShopNameKm, settings.ShopNameKm);
        settings.ShopNameEn = Text(stored, AppSettingKeys.ShopNameEn, settings.ShopNameEn);
        settings.ShopAddressKm = Text(stored, AppSettingKeys.ShopAddressKm, settings.ShopAddressKm);
        settings.ShopAddressEn = Text(stored, AppSettingKeys.ShopAddressEn, settings.ShopAddressEn);
        settings.ShopTel = Text(stored, AppSettingKeys.ShopTel, settings.ShopTel);

        settings.PrintLanguage = ReceiptSettingCodes.ParseLanguage(
            Raw(stored, AppSettingKeys.PrintLanguage), settings.PrintLanguage);

        settings.PaperWidth = ReceiptSettingCodes.ParseWidth(
            Raw(stored, AppSettingKeys.PrintWidth), settings.PaperWidth);

        settings.ShowRiel = Bool(stored, AppSettingKeys.CurrencyShowRiel, settings.ShowRiel);
        settings.ExchangeRate = Number(stored, AppSettingKeys.CurrencyRate, settings.ExchangeRate);
        settings.RielRounding = Rounding(stored, settings.RielRounding);

        settings.ShowReceiptNumber = Bool(stored, AppSettingKeys.ReceiptShowNo, settings.ShowReceiptNumber);
        settings.ShowStaffName = Bool(stored, AppSettingKeys.ReceiptShowStaff, settings.ShowStaffName);
        settings.ShowUnitPrice = Bool(stored, AppSettingKeys.ReceiptShowPrice, settings.ShowUnitPrice);
        settings.ShowUnitLabel = Bool(stored, AppSettingKeys.ReceiptShowUnit, settings.ShowUnitLabel);

        settings.ReceiptPrefix = Text(stored, AppSettingKeys.ReceiptPrefix, settings.ReceiptPrefix);

        settings.ResetCycle = ReceiptSettingCodes.ParseResetCycle(
            Raw(stored, AppSettingKeys.ReceiptResetCycle), settings.ResetCycle);

        settings.FooterKm = Text(stored, AppSettingKeys.ReceiptFooterKm, settings.FooterKm);
        settings.FooterEn = Text(stored, AppSettingKeys.ReceiptFooterEn, settings.FooterEn);

        settings.VatEnabled = Bool(stored, AppSettingKeys.VatEnabled, settings.VatEnabled);
        settings.VatTin = Text(stored, AppSettingKeys.VatTin, settings.VatTin);
        settings.VatRate = Number(stored, AppSettingKeys.VatRate, settings.VatRate);

        _cache.Set(settings);

        return settings;
    }

    public async Task<ReceiptSettingsSaveResult> SaveAsync(
        ReceiptSettings settings, UserRole actingUserRole, string actingUserId)
    {
        if (actingUserRole != UserRole.Administrator)
        {
            return ReceiptSettingsSaveResult.Failure(
                ReceiptStrings.English(ReceiptStringKeys.ErrorNotAdministrator));
        }

        var normalized = Normalize(settings);
        var errors = ReceiptSettingsValidator.Validate(normalized);

        if (errors.Count > 0)
        {
            return ReceiptSettingsSaveResult.Invalid(errors);
        }

        // 21개 키를 쓰는 동안 다른 창이 끼어들면 반쯤 바뀐 설정이 남는다.
        await _cache.SaveLock.WaitAsync();

        try
        {
            await WriteAsync(normalized, actingUserId);
        }
        catch (Exception)
        {
            // 일부만 쓰였을 수 있으므로 캐시를 믿을 수 없다.
            _cache.Invalidate();

            return ReceiptSettingsSaveResult.Failure(
                ReceiptStrings.English(ReceiptStringKeys.ErrorSaveFailed));
        }
        finally
        {
            _cache.SaveLock.Release();
        }

        // 방금 저장한 값이 곧바로 다음 영수증에 반영되도록 캐시를 갈아 끼운다.
        _cache.Set(normalized);

        return ReceiptSettingsSaveResult.Success(
            ReceiptStrings.English(ReceiptStringKeys.MessageSaved));
    }

    private async Task WriteAsync(ReceiptSettings settings, string actingUserId)
    {
        async Task Write(string key, string value, string type) =>
            await _settingRepository.SetAsync(key, value, type, actingUserId);

        await Write(AppSettingKeys.ShopNameKm, settings.ShopNameKm, TypeText);
        await Write(AppSettingKeys.ShopNameEn, settings.ShopNameEn, TypeText);
        await Write(AppSettingKeys.ShopAddressKm, settings.ShopAddressKm, TypeText);
        await Write(AppSettingKeys.ShopAddressEn, settings.ShopAddressEn, TypeText);
        await Write(AppSettingKeys.ShopTel, settings.ShopTel, TypeText);

        await Write(AppSettingKeys.PrintLanguage,
            ReceiptSettingCodes.ToCode(settings.PrintLanguage), TypeEnum);
        await Write(AppSettingKeys.PrintWidth,
            ReceiptSettingCodes.ToCode(settings.PaperWidth), TypeEnum);

        await Write(AppSettingKeys.CurrencyShowRiel, ToStorage(settings.ShowRiel), TypeBool);
        await Write(AppSettingKeys.CurrencyRate, ToStorage(settings.ExchangeRate), TypeNumber);
        await Write(AppSettingKeys.CurrencyRounding,
            settings.RielRounding.ToString(CultureInfo.InvariantCulture), TypeEnum);

        await Write(AppSettingKeys.ReceiptShowNo, ToStorage(settings.ShowReceiptNumber), TypeBool);
        await Write(AppSettingKeys.ReceiptShowStaff, ToStorage(settings.ShowStaffName), TypeBool);
        await Write(AppSettingKeys.ReceiptShowPrice, ToStorage(settings.ShowUnitPrice), TypeBool);
        await Write(AppSettingKeys.ReceiptShowUnit, ToStorage(settings.ShowUnitLabel), TypeBool);

        await Write(AppSettingKeys.ReceiptPrefix, settings.ReceiptPrefix, TypeText);
        await Write(AppSettingKeys.ReceiptResetCycle,
            ReceiptSettingCodes.ToCode(settings.ResetCycle), TypeEnum);

        await Write(AppSettingKeys.ReceiptFooterKm, settings.FooterKm, TypeText);
        await Write(AppSettingKeys.ReceiptFooterEn, settings.FooterEn, TypeText);

        await Write(AppSettingKeys.VatEnabled, ToStorage(settings.VatEnabled), TypeBool);
        await Write(AppSettingKeys.VatTin, settings.VatTin, TypeText);
        await Write(AppSettingKeys.VatRate, ToStorage(settings.VatRate), TypeNumber);
    }

    /// <summary>
    /// 저장 직전 손질. 접두어를 대문자로 올리는 이유는, 소문자로 입력한 것이
    /// 형식 오류로 되돌아오는 것보다 고쳐서 받는 쪽이 낫기 때문이다.
    /// </summary>
    private static ReceiptSettings Normalize(ReceiptSettings settings)
    {
        var copy = settings.Clone();

        copy.ShopNameKm = Trim(copy.ShopNameKm);
        copy.ShopNameEn = Trim(copy.ShopNameEn);
        copy.ShopAddressKm = Trim(copy.ShopAddressKm);
        copy.ShopAddressEn = Trim(copy.ShopAddressEn);
        copy.ShopTel = Trim(copy.ShopTel);
        copy.ReceiptPrefix = Trim(copy.ReceiptPrefix).ToUpperInvariant();
        copy.FooterKm = Trim(copy.FooterKm);
        copy.FooterEn = Trim(copy.FooterEn);
        copy.VatTin = Trim(copy.VatTin);

        // 명세가 허용하는 반올림 단위는 셋뿐이다. 다른 값이 들어오면 100으로 되돌린다.
        if (copy.RielRounding is not (0 or 100 or 500))
        {
            copy.RielRounding = 100;
        }

        return copy;
    }

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;

    private static string ToStorage(bool value) => value ? "true" : "false";

    private static string ToStorage(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string? Raw(IReadOnlyDictionary<string, string> stored, string key) =>
        stored.TryGetValue(key, out var value) ? value : null;

    private static string Text(
        IReadOnlyDictionary<string, string> stored, string key, string fallback)
    {
        var raw = Raw(stored, key);

        // 빈 값도 "저장된 값"이다. 주소나 맺음 문구는 일부러 비워 두는 항목이라
        // 비었다고 기본값으로 되돌리면 지운 값이 되살아난다.
        return raw is null ? fallback : raw;
    }

    private static bool Bool(
        IReadOnlyDictionary<string, string> stored, string key, bool fallback)
    {
        var raw = Raw(stored, key);

        return bool.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static decimal Number(
        IReadOnlyDictionary<string, string> stored, string key, decimal fallback)
    {
        var raw = Raw(stored, key);

        return decimal.TryParse(
            raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static int Rounding(IReadOnlyDictionary<string, string> stored, int fallback)
    {
        var raw = Raw(stored, AppSettingKeys.CurrencyRounding);

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return fallback;
        }

        return parsed is 0 or 100 or 500 ? parsed : fallback;
    }
}
