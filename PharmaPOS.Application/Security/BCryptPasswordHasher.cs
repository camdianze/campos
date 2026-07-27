namespace PharmaPOS.Application.Security;

/// <summary>
/// BCrypt.Net-Next 라이브러리를 이용한 IPasswordHasher 구현체.
/// PRD 보안 정책: 비밀번호는 절대 평문으로 저장하지 않는다.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plainTextPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainTextPassword);
    }

    public bool Verify(string plainTextPassword, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(plainTextPassword, passwordHash);
    }
}