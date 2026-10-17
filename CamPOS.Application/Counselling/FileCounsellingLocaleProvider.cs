using System.Text.Json;
using System.Text.Json.Serialization;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// locales/{bcp47}.json 파일에서 로케일을 읽는 구현체.
///
/// 폴더 후보를 앞에서부터 훑어 먼저 발견한 파일을 쓴다. 보통 이렇게 넘긴다:
///   1) %APPDATA%\PharmaPOS\locales   ← 현장 교체용 (검수 끝난 번역을 여기 놓는다)
///   2) (설치 폴더)\locales           ← 기본 동봉본
///
/// 이 클래스는 어떤 이유로도 예외를 밖으로 내보내지 않는다.
/// 로케일을 못 읽는 것은 "영어로만 인쇄한다"는 뜻이지, 인쇄를 못 한다는 뜻이 아니다.
/// </summary>
public class FileCounsellingLocaleProvider : ICounsellingLocaleProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IReadOnlyList<string> _localeDirectories;

    public FileCounsellingLocaleProvider(IReadOnlyList<string> localeDirectories)
    {
        _localeDirectories = localeDirectories;
    }

    public async Task<CounsellingLocale> GetLocaleAsync(string? localeCode)
    {
        if (string.IsNullOrWhiteSpace(localeCode))
        {
            return CounsellingLocale.EnglishOnly;
        }

        // 경로 조작을 막는다. 로케일 코드는 설정값이라 사용자가 손댈 수 있다.
        var safeCode = localeCode.Trim();

        if (safeCode.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
        {
            return CounsellingLocale.EnglishOnly;
        }

        foreach (var directory in _localeDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var path = Path.Combine(directory, safeCode + ".json");

            if (!File.Exists(path))
            {
                continue;
            }

            var locale = await ReadLocaleFileAsync(path);

            if (locale is not null)
            {
                return locale;
            }
        }

        return CounsellingLocale.EnglishOnly;
    }

    public async Task<IReadOnlyList<CounsellingLocale>> ListAvailableLocalesAsync()
    {
        var found = new List<CounsellingLocale>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in _localeDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(directory, "*.json");
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var locale = await ReadLocaleFileAsync(file);

                // 앞선 폴더(현장 교체본)가 우선이므로, 이미 본 코드는 건너뛴다.
                if (locale is not null && seenCodes.Add(locale.LocaleCode))
                {
                    found.Add(locale);
                }
            }
        }

        return found;
    }

    private static async Task<CounsellingLocale?> ReadLocaleFileAsync(string path)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var file = JsonSerializer.Deserialize<LocaleFile>(json, JsonOptions);

            if (file is null)
            {
                return null;
            }

            // 파일 안의 locale 값이 비었으면 파일 이름을 쓴다.
            var code = string.IsNullOrWhiteSpace(file.Locale)
                ? Path.GetFileNameWithoutExtension(path)
                : file.Locale.Trim();

            var renderMode = string.Equals(file.RenderMode, "raster", StringComparison.OrdinalIgnoreCase)
                ? LocaleRenderMode.Raster
                : LocaleRenderMode.Text;

            return new CounsellingLocale(
                code,
                file.LanguageName,
                file.Script,
                renderMode,
                file.ReviewStatus,
                file.ReviewedBy,
                file.ContentVersion,
                file.Strings ?? new Dictionary<string, string>());
        }
        catch (Exception)
        {
            // JSON이 깨졌거나 읽을 수 없는 파일. 영어 단독으로 떨어뜨린다.
            return null;
        }
    }

    /// <summary>
    /// 로케일 JSON의 역직렬화용 형태.
    /// 파일 쪽 키는 snake_case라 속성마다 이름을 명시한다.
    /// strings의 키("sheet.subtitle" 등)는 사전 키라서 변환되지 않고 그대로 들어온다.
    /// </summary>
    private class LocaleFile
    {
        [JsonPropertyName("locale")]
        public string? Locale { get; set; }

        [JsonPropertyName("language_name")]
        public string? LanguageName { get; set; }

        [JsonPropertyName("script")]
        public string? Script { get; set; }

        [JsonPropertyName("render_mode")]
        public string? RenderMode { get; set; }

        [JsonPropertyName("review_status")]
        public string? ReviewStatus { get; set; }

        [JsonPropertyName("reviewed_by")]
        public string? ReviewedBy { get; set; }

        [JsonPropertyName("content_version")]
        public string? ContentVersion { get; set; }

        [JsonPropertyName("strings")]
        public Dictionary<string, string>? Strings { get; set; }
    }
}
