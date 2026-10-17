using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Domain.Entities;

/// <summary>
/// PRD의 Facility 테이블에 대응하는 엔티티.
/// F-01(로그인 시 시설 상태 확인)과 F-02(초기 시설 설정)에서 공통으로 사용한다.
/// </summary>
public class Facility
{
    public required string FacilityId { get; set; }

    public required string FacilityName { get; set; }

    public required string Country { get; set; }

    public required string District { get; set; }

    public required FacilityType FacilityType { get; set; }

    public required EntityStatus Status { get; set; }
}