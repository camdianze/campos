namespace PharmaPOS.Application.Security;

/// <summary>
/// 비밀번호 해싱 및 검증을 담당하는 인터페이스.
/// 구현체를 교체하더라도(BCrypt → 다른 알고리즘) 이 인터페이스를 사용하는
/// 코드는 수정할 필요가 없다.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// 평문 비밀번호를 해시값으로 변환한다.
    /// </summary>
    string Hash(string plainTextPassword);

    /// <summary>
    /// 입력된 평문 비밀번호가 저장된 해시값과 일치하는지 확인한다.
    /// </summary>
    bool Verify(string plainTextPassword, string passwordHash);
}