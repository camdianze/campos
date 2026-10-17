namespace PharmaPOS.Domain.Enums;

/// <summary>
/// 시스템에 로그인하는 사용자의 역할.
/// F-01 로그인 시 이 값에 따라 이동 화면과 접근 권한이 분기된다.
/// </summary>
public enum UserRole
{
    FacilityStaff,
    Administrator
}