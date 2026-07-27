using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 초기 시설 설정(F-02)의 데이터 저장을 담당하는 인터페이스.
/// </summary>
public interface IInitialSetupRepository
{
    /// <summary>
    /// Facility 정보와 Administrator 계정이 모두 존재하는지 확인한다.
    /// 앱 시작 시 초기 설정 화면으로 갈지, 로그인 화면으로 갈지 판단하는 데 사용한다.
    /// (Screen SCR-SETUP-003, 4.1절 1~4단계)
    /// </summary>
    Task<bool> IsSetupCompleteAsync();

    /// <summary>
    /// Facility와 최초 Administrator 계정을 하나의 트랜잭션으로 저장한다.
    /// 하나라도 실패하면 전체 롤백된다. (Screen SCR-SETUP-003, 4.3절 "저장 처리 원칙")
    /// </summary>
    Task SaveFacilityAndAdminAsync(Facility facility, User adminUser);
}