using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Domain.Entities;

/// <summary>
/// 임포트 1회의 기록. 같은 파일을 두 번 넣어 재고가 두 배가 되는 사고를 막는 것이 목적이라
/// 파일 내용의 해시를 남긴다 (파일 이름은 바뀌어도 내용이 같으면 같은 파일로 본다).
/// </summary>
public class ImportHistoryEntry
{
    public required string ImportId { get; set; }

    public required string FacilityId { get; set; }

    public required ImportType ImportType { get; set; }

    /// <summary>파일 내용의 SHA-256 (소문자 16진수).</summary>
    public required string FileHash { get; set; }

    /// <summary>사람이 알아볼 수 있게 남기는 값. 판정에는 쓰지 않는다.</summary>
    public string? FileName { get; set; }

    public required int RowCount { get; set; }

    public required int SuccessCount { get; set; }

    public required int FailureCount { get; set; }

    public required long ImportedAt { get; set; }
}
