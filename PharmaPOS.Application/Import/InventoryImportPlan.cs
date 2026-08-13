using PharmaPOS.Domain.Entities;

// 엔티티 이름(Inventory)이 Application의 네임스페이스와 같아 그냥 쓰면 네임스페이스로 읽힌다.
using InventoryEntity = PharmaPOS.Domain.Entities.Inventory;

namespace PharmaPOS.Application.Import;

/// <summary>
/// 임포트로 만들 배치 하나. 수량은 언제나 <b>낱개</b> 기준이다 —
/// 실사는 낱개로 세고, 박스로만 받으면 "한 통 반"을 적을 방법이 없다.
/// (입고 화면의 수량이 박스 개수인 것과 다른 점이라 저장할 때 환산해 넣는다.)
/// </summary>
public sealed class InventoryImportLine
{
    public required int LineNumber { get; init; }
    public required string ProductId { get; init; }
    public required string ProductName { get; init; }

    /// <summary>비어 있을 수 있다. 배치번호 없이 관리하던 약국이 흔하다.</summary>
    public required string BatchNumber { get; init; }

    /// <summary>Unix ms. <see cref="InventoryEntity.NoExpiryDate"/>(0)이면 유효기간 모름.</summary>
    public required long ExpiryDate { get; init; }

    /// <summary>낱개 기준 수량.</summary>
    public required int QuantityInUnits { get; init; }

    public required int UnitsPerBox { get; init; }

    public bool HasNoExpiry => ExpiryDate == InventoryEntity.NoExpiryDate;
}

/// <summary>
/// Inventory 임포트를 반영하기 전에 계산해 둔 결과.
/// </summary>
public sealed class InventoryImportPlan
{
    public string? FileError { get; init; }

    public int TotalRows { get; init; }

    public IReadOnlyList<InventoryImportLine> BatchesToCreate { get; init; } = [];

    /// <summary>상품명으로 기존 상품을 찾지 못한 행. 오류와 따로 세어 보여준다 —
    /// 대개는 Products 임포트를 먼저 하지 않았다는 뜻이라 사용자가 할 일이 다르다.</summary>
    public IReadOnlyList<ImportIssue> UnmatchedRows { get; init; } = [];

    public IReadOnlyList<ImportIssue> Issues { get; init; } = [];

    public int BatchCount => BatchesToCreate.Count;
    public int UnmatchedRowCount => UnmatchedRows.Count;
    public int ErrorRowCount => Issues.Count;
    public int NoExpiryCount => BatchesToCreate.Count(b => b.HasNoExpiry);

    public bool HasFileError => FileError is not null;
    public bool HasWork => BatchesToCreate.Count > 0;
}
