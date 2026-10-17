namespace PharmaPOS.Domain.Enums;

/// <summary>
/// 비밀번호 복구 이메일 발송에 사용할 제공자.
/// Gmail/Outlook은 SMTP 서버 주소·포트가 고정값으로 자동 설정된다.
/// </summary>
public enum EmailProvider
{
    Gmail,
    Outlook,
    Other
}