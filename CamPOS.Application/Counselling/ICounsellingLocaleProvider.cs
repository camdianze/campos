namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 로케일 파일을 읽어온다.
/// </summary>
public interface ICounsellingLocaleProvider
{
    /// <summary>
    /// 로케일 코드에 해당하는 파일을 읽는다.
    /// 코드가 비었거나, 파일이 없거나, 형식이 깨졌으면 CounsellingLocale.EnglishOnly를 돌려준다.
    /// 예외를 던지지 않는다 — 로케일 문제로 인쇄가 막히면 안 된다.
    /// </summary>
    Task<CounsellingLocale> GetLocaleAsync(string? localeCode);

    /// <summary>설정 화면에서 고를 수 있도록, 설치된 로케일 파일 목록을 돌려준다.</summary>
    Task<IReadOnlyList<CounsellingLocale>> ListAvailableLocalesAsync();
}
