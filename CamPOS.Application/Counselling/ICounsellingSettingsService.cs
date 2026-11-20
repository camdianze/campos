using CamPOS.Domain.Enums;

namespace CamPOS.Application.Counselling;

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

    /// <summary>
    /// 설정을 저장한다.
    ///
    /// <b>권한 검사를 여기서 한다.</b> 안내지 설정은 항생제 리포트 화면에서도 고칠 수
    /// 있게 됐고 그 화면은 직원도 열 수 있다. 화면에서 단추를 숨기는 것만으로 막으면
    /// 화면을 하나 더 만들 때마다 같은 숨김을 되풀이해야 하고, 한 번 빼먹으면
    /// 시설 전체에 적용되는 설정이 누구에게나 열린다. 거부가 이 자리에 있으면
    /// 화면이 몇 개든 규칙은 하나다.
    ///
    /// 저장할 값은 <see cref="GetAsync"/>로 읽은 것에서 고친 칸만 바꿔 넘긴다 —
    /// 이 메서드는 받은 객체를 그대로 다 쓰므로, 일부만 채워 넘기면 나머지가 기본값으로
    /// 덮인다.
    /// </summary>
    /// <param name="actingRole">저장을 시도하는 사용자의 역할.</param>
    Task<CounsellingSettingsSaveResult> SaveAsync(CounsellingSettings settings, UserRole actingRole);

    /// <summary>
    /// AWaRe 참조 데이터가 몇 건 적재돼 있고 출처가 무엇인지.
    /// 설정 화면에서 "참조 데이터 설치됨/안 됨"을 보여주는 데 쓴다.
    /// </summary>
    Task<(int Count, string? SourceVersion)> GetReferenceDataStatusAsync();
}
