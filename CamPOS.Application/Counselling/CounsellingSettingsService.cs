using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Settings;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// ICounsellingSettingsService의 구현체.
///
/// 설정을 읽다 실패하면 예외를 던지지 않고 기본값으로 떨어진다.
/// 설정을 못 읽었다고 판매 화면이 멈추면 안 되고, 기본값(Always)이
/// 안전한 쪽이기 때문이다.
/// </summary>
public class CounsellingSettingsService : ICounsellingSettingsService
{
    private readonly IAppSettingRepository _settingRepository;
    private readonly IAwareClassificationRepository _awareRepository;

    public CounsellingSettingsService(
        IAppSettingRepository settingRepository,
        IAwareClassificationRepository awareRepository)
    {
        _settingRepository = settingRepository;
        _awareRepository = awareRepository;
    }

    public async Task<CounsellingSettings> GetAsync()
    {
        var settings = new CounsellingSettings();

        try
        {
            var printMode = await _settingRepository.GetAsync(AppSettingKeys.CounsellingPrintMode);

            if (Enum.TryParse<CounsellingPrintMode>(printMode, ignoreCase: true, out var parsedMode))
            {
                settings.PrintMode = parsedMode;
            }

            var format = await _settingRepository.GetAsync(AppSettingKeys.CounsellingSheetFormat);

            if (Enum.TryParse<CounsellingSheetFormat>(format, ignoreCase: true, out var parsedFormat))
            {
                settings.SheetFormat = parsedFormat;
            }

            var output = await _settingRepository.GetAsync(AppSettingKeys.CounsellingOutput);

            if (Enum.TryParse<CounsellingOutput>(output, ignoreCase: true, out var parsedOutput))
            {
                settings.Output = parsedOutput;
            }

            settings.FileOutputFolder =
                await _settingRepository.GetAsync(AppSettingKeys.CounsellingFileFolder) ?? string.Empty;

            settings.LocaleCode =
                await _settingRepository.GetAsync(AppSettingKeys.CounsellingLocale) ?? string.Empty;

            settings.QrUrl =
                await _settingRepository.GetAsync(AppSettingKeys.CounsellingQrUrl) ?? string.Empty;

            settings.ResearchSiteCode =
                await _settingRepository.GetAsync(AppSettingKeys.ResearchSiteCode) ?? string.Empty;
        }
        catch (Exception)
        {
            return new CounsellingSettings();
        }

        return settings;
    }

    public async Task SaveAsync(CounsellingSettings settings)
    {
        await _settingRepository.SetAsync(
            AppSettingKeys.CounsellingPrintMode, settings.PrintMode.ToString());
        await _settingRepository.SetAsync(
            AppSettingKeys.CounsellingSheetFormat, settings.SheetFormat.ToString());
        await _settingRepository.SetAsync(
            AppSettingKeys.CounsellingOutput, settings.Output.ToString());
        await _settingRepository.SetAsync(
            AppSettingKeys.CounsellingFileFolder, settings.FileOutputFolder ?? string.Empty);
        await _settingRepository.SetAsync(
            AppSettingKeys.CounsellingLocale, settings.LocaleCode ?? string.Empty);
        await _settingRepository.SetAsync(
            AppSettingKeys.CounsellingQrUrl, settings.QrUrl ?? string.Empty);
        await _settingRepository.SetAsync(
            AppSettingKeys.ResearchSiteCode, settings.ResearchSiteCode ?? string.Empty);
    }

    public async Task<(int Count, string? SourceVersion)> GetReferenceDataStatusAsync()
    {
        try
        {
            var count = await _awareRepository.CountAsync();
            var sourceVersion = await _settingRepository.GetAsync(AppSettingKeys.AwareSourceVersion);

            return (count, sourceVersion);
        }
        catch (Exception)
        {
            return (0, null);
        }
    }
}
