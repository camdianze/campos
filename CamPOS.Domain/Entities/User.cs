using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Domain.Entities;

/// <summary>
/// PRD의 Users 테이블에 대응하는 엔티티.
/// </summary>
public class User
{
    public required string UserId { get; set; }

    public required string FacilityId { get; set; }

    public required string Username { get; set; }

    /// <summary>
    /// bcrypt로 해시된 비밀번호. 평문은 절대 저장하지 않는다.
    /// </summary>
    public required string PasswordHash { get; set; }

    public required UserRole Role { get; set; }

    public required EntityStatus Status { get; set; }

    /// <summary>
    /// 계정 생성 시각 (Unix epoch milliseconds).
    /// </summary>
    public required long CreatedAt { get; set; }

    // ── 관리자 본인 비밀번호 복구용 (Administrator 계정에만 설정, 전부 nullable) ──

    /// <summary>보안 질문. 로그인 화면의 "Forgot Password?"에서 사용.</summary>
    public string? SecurityQuestion { get; set; }

    /// <summary>보안 질문 답변의 해시값. 비밀번호와 동일하게 평문 저장하지 않는다.</summary>
    public string? SecurityAnswerHash { get; set; }

    /// <summary>복구용 이메일 주소.</summary>
    public string? RecoveryEmail { get; set; }

    /// <summary>이메일 발송에 사용할 제공자 (SMTP 서버/포트 자동 설정용).</summary>
    public EmailProvider? EmailProvider { get; set; }

    /// <summary>
    /// 이메일 발송용 앱 비밀번호. Windows DPAPI로 암호화된 상태로 저장한다 (평문 저장 금지).
    /// </summary>
    public string? EmailAppPasswordEncrypted { get; set; }
    /// <summary>EmailProvider가 Other일 때만 사용하는 SMTP 서버 주소.</summary>
    public string? SmtpHost { get; set; }

    /// <summary>EmailProvider가 Other일 때만 사용하는 SMTP 포트.</summary>
    public int? SmtpPort { get; set; }
}