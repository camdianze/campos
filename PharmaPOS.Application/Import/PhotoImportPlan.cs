using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Import;

/// <summary>사진 한 장과 그 사진이 붙을 상품.</summary>
public sealed record PhotoImportMatch(string FileName, Product Product, bool ReplacesExistingPhoto);

/// <summary>
/// 사진 임포트 계획. 다른 단계와 같이 "무엇이 일어날지" 먼저 보여주고 확인을 받는다.
/// </summary>
public class PhotoImportPlan
{
    public required IReadOnlyList<PhotoImportMatch> Matches { get; init; }

    /// <summary>짝지을 상품을 찾지 못한 파일. 파일명이 바코드와 어긋난 경우다.</summary>
    public required IReadOnlyList<string> UnmatchedFiles { get; init; }

    /// <summary>한 파일이 여러 상품에 걸리는 등 넘길 수 없는 것들.</summary>
    public required IReadOnlyList<ImportIssue> Issues { get; init; }

    /// <summary>
    /// 읽을 수 없는 형식이라 넘긴 파일과 그 이유.
    ///
    /// 조용히 건너뛰지 않는 이유: 아이폰 기본 설정으로 찍은 사진은 HEIC라
    /// 폴더가 가득 차 있는데도 "넣을 사진이 없습니다"만 뜬다. 그러면 원인을 찾을 수 없다.
    /// </summary>
    public required IReadOnlyList<ImportIssue> SkippedFiles { get; init; }

    public int TotalFiles { get; init; }

    /// <summary>이미 사진이 있는 상품에 덮어쓰는 건수. 되돌릴 수 없으므로 미리 알린다.</summary>
    public int ReplaceCount => Matches.Count(m => m.ReplacesExistingPhoto);

    public int NewCount => Matches.Count - ReplaceCount;

    public bool HasWork => Matches.Count > 0;
}
