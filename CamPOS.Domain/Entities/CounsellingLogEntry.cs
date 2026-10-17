namespace PharmaPOS.Domain.Entities;

/// <summary>
/// 복약안내 출력 이력 한 줄. 스튜어드십 지표 집계용이다.
///
/// 환자를 식별할 수 있는 정보는 어떤 형태로도 담지 않는다.
/// 거래 ID와 상품 ID, 분류, 출력 여부만 남긴다.
/// </summary>
public class CounsellingLogEntry
{
    public required string LogId { get; set; }

    public required string TransactionId { get; set; }

    public required string ProductId { get; set; }

    public string? AtcCode { get; set; }

    /// <summary>
    /// ACCESS / WATCH / RESERVE / NOT_RECOMMENDED, 또는 매칭 실패 시 UNMATCHED.
    /// enum이 아니라 문자열인 이유는 UNMATCHED가 분류 그룹이 아니기 때문이다.
    /// </summary>
    public required string AwareGroup { get; set; }

    public required bool Printed { get; set; }

    /// <summary>인쇄하지 않은 이유. 인쇄했으면 null.</summary>
    public string? SkipReason { get; set; }

    /// <summary>인쇄 시점의 로케일. 영어 단독이면 "en".</summary>
    public required string Locale { get; set; }

    public string? SourceVersion { get; set; }

    public required long CreatedAt { get; set; }
}
