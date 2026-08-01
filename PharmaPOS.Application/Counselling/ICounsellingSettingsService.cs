namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 설정을 읽고 저장한다.
/// </summary>
public interface ICounsellingSettingsService
{
    /// <summary>
    /// 저장된 설정을 읽는다. 값이 없거나 읽을 수 없으면 기본값을 돌려준다
    /// (기본값은 PrintMode = Always, 영어 단독, 전체 분량).
    /// </summary>
    Task<CounsellingSettings> GetAsync();

    Task SaveAsync(CounsellingSettings settings);

    /// <summary>
    /// AWaRe 참조 데이터가 몇 건 적재돼 있고 출처가 무엇인지.
    /// 설정 화면에서 "참조 데이터 설치됨/안 됨"을 보여주는 데 쓴다.
    /// </summary>
    Task<(int Count, string? SourceVersion)> GetReferenceDataStatusAsync();
}
