namespace PharmaPOS.Application.Security;

/// <summary>
/// 이메일 앱 비밀번호처럼 민감한 값을 암호화/복호화하는 인터페이스.
/// SQLite에는 암호화된 형태로만 저장한다 (평문 저장 금지 원칙).
/// </summary>
public interface IRecoveryDataProtector
{
    string Protect(string plainText);

    string Unprotect(string encryptedText);
}