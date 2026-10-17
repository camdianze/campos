using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Domain.Entities;

/// <summary>
/// WHO AWaRe 분류표의 한 행. 시드 파일(seeds/aware_2025.csv)에서 적재된다.
/// 코드에 하드코딩하지 않는다 — 개정판이 나오면 CSV만 교체한다.
/// </summary>
public class AwareClassification
{
    /// <summary>
    /// 대리 키. ATC 코드를 PK로 쓰지 않는 이유:
    /// 고정용량복합제(FDC)처럼 고유 ATC 코드가 부여되지 않은 항목이 존재하는데,
    /// 그 항목들이 바로 NOT_RECOMMENDED 그룹이라 PK로 삼으면 적재 자체가 불가능해진다.
    /// </summary>
    public required string AwareId { get; set; }

    public string? AtcCode { get; set; }

    public required string AntibioticName { get; set; }

    /// <summary>
    /// generic_name 매칭용으로 미리 정규화해 둔 이름.
    /// 조회 때마다 정규화하면 인덱스를 못 타므로 적재 시점에 계산해 저장한다.
    /// </summary>
    public required string NormalizedName { get; set; }

    public required AwareGroup AwareGroup { get; set; }

    /// <summary>
    /// 전신 제제 여부. false면 국소 제제라 복약안내 대상에서 제외한다.
    /// ATC 접두사로 판단하지 않고 시드 파일의 값을 그대로 신뢰한다
    /// (전신 항생제는 J01 외에 A07AA, J04, P01AB 등에도 걸쳐 있다).
    /// </summary>
    public required bool IsSystemic { get; set; }

    /// <summary>예: 'WHO AWaRe 2025'. 인쇄물과 로그에 출처로 함께 남긴다.</summary>
    public required string SourceVersion { get; set; }

    public required long UpdatedAt { get; set; }
}
