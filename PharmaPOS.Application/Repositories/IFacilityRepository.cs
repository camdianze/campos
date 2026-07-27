using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Repositories;

/// <summary>
/// Facility 테이블에 대한 데이터 접근을 추상화한 인터페이스.
/// F-01에서는 로그인 시 시설 활성 상태 확인 용도로만 사용한다.
/// </summary>
public interface IFacilityRepository
{
    /// <summary>
    /// facility_id로 시설 정보를 조회한다. 존재하지 않으면 null을 반환한다.
    /// </summary>
    Task<Facility?> GetByIdAsync(string facilityId);
}