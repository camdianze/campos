using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 시드 CSV 한 줄을 파싱한 결과.
/// </summary>
public class AwareSeedRow
{
    public string? AtcCode { get; set; }

    public required string AntibioticName { get; set; }

    public required AwareGroup AwareGroup { get; set; }

    public required bool IsSystemic { get; set; }

    public required string SourceVersion { get; set; }
}
