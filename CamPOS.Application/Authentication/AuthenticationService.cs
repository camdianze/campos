using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Security;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Authentication;

/// <summary>
/// IAuthenticationService의 구현체.
/// Screen 01, 6.1절 로그인 흐름(1~9단계)을 그대로 코드로 옮긴 것이다.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IFacilityRepository _facilityRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AuthenticationService(
        IUserRepository userRepository,
        IFacilityRepository facilityRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _facilityRepository = facilityRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthenticationResult> LoginAsync(string username, string password)
    {
        // 1~2단계: 입력값 검증 (Screen 01, 7.1절)
        if (string.IsNullOrWhiteSpace(username))
        {
            return AuthenticationResult.Failure(AuthenticationError.UsernameEmpty);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return AuthenticationResult.Failure(AuthenticationError.PasswordEmpty);
        }

        // 3단계: Users 테이블에서 username 조회
        var user = await _userRepository.GetByUsernameAsync(username);

        // 4단계: 사용자가 없는 경우.
        // 보안 원칙(7.2절): "아이디 없음"과 "비밀번호 틀림"을 구분하지 않는다.
        if (user is null)
        {
            return AuthenticationResult.Failure(AuthenticationError.InvalidCredentials);
        }

        // 5단계: 계정 상태 확인
        if (user.Status != EntityStatus.Active)
        {
            return AuthenticationResult.Failure(AuthenticationError.AccountInactive);
        }

        // 6단계: 비밀번호 검증
        var isPasswordCorrect = _passwordHasher.Verify(password, user.PasswordHash);
        if (!isPasswordCorrect)
        {
            return AuthenticationResult.Failure(AuthenticationError.InvalidCredentials);
        }

        // 시설 상태 확인 (Screen 01, 10절 예외 처리 표: "시설 상태가 Inactive").
        var facility = await _facilityRepository.GetByIdAsync(user.FacilityId);
        if (facility is null || facility.Status != EntityStatus.Active)
        {
            return AuthenticationResult.Failure(AuthenticationError.FacilityInactive);
        }

        // 7~9단계: 인증 성공
        return AuthenticationResult.Success(user);
    }
}