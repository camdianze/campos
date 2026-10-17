namespace PharmaPOS.Application.Authentication;

/// <summary>
/// 로그인 실패 사유. Screen 01, 7.1절의 각 오류 메시지에 1:1로 대응한다.
/// </summary>
public enum AuthenticationError
{
    None,
    UsernameEmpty,
    PasswordEmpty,
    InvalidCredentials,
    AccountInactive,
    FacilityInactive
}