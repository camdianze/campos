namespace PharmaPOS.Domain.Enums;

/// <summary>
/// 사용자(Users) 및 시설(Facility)의 활성/비활성 상태.
/// 비활성 상태인 계정/시설은 로그인이 차단된다 (Screen 01, 7.1절).
/// </summary>
public enum EntityStatus
{
    Active,
    Inactive
}