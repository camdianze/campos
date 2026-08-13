using PharmaPOS.Domain.Entities;

namespace PharmaPOS.Application.Import;

/// <summary>
/// 등록할 상품 하나와 그 상품이 온 행 번호.
/// 저장에 실패했을 때 파일의 어느 줄을 고쳐야 하는지 알려면 행 번호가 끝까지 따라가야 한다.
/// </summary>
public sealed class ProductImportLine
{
    public required int LineNumber { get; init; }
    public required Product Product { get; init; }
}

/// <summary>
/// Products 임포트를 실제로 반영하기 전에 계산해 둔 결과.
/// 미리보기 화면이 보여주는 숫자와, 진행을 눌렀을 때 저장할 목록이 같은 객체에서 나온다 —
/// 두 번 계산하면 "미리보기에는 12건인데 11건이 들어갔다"가 생긴다.
/// </summary>
public sealed class ProductImportPlan
{
    /// <summary>파일이 통째로 잘못된 경우의 사유(필수 컬럼 없음 등). 이 값이 있으면 진행할 수 없다.</summary>
    public string? FileError { get; init; }

    /// <summary>헤더를 뺀 데이터 행 수(빈 행 제외).</summary>
    public int TotalRows { get; init; }

    /// <summary>새로 등록할 상품. 중복 제거와 검증을 마친 목록이다.</summary>
    public IReadOnlyList<ProductImportLine> ProductsToCreate { get; init; } = [];

    /// <summary>
    /// 이미 등록돼 있고, 파일에 채워진 칸만 고쳐 넣을 상품.
    /// 비어 있는 칸은 그대로 둔다 — 상품명만 적힌 행이 기존 단가를 지우면 안 된다.
    /// </summary>
    public IReadOnlyList<ProductImportLine> ProductsToUpdate { get; init; } = [];

    /// <summary>같은 상품명이 다시 나와 건너뛴 행 수. 배치가 여러 개면 정상적으로 생긴다.</summary>
    public int DuplicateRowCount { get; init; }

    /// <summary>이미 등록돼 있는데 고칠 값이 하나도 적혀 있지 않아 손대지 않는 상품명.</summary>
    public IReadOnlyList<string> UnchangedNames { get; init; } = [];

    /// <summary>값이 잘못돼 건너뛸 행.</summary>
    public IReadOnlyList<ImportIssue> Issues { get; init; } = [];

    public int CreateCount => ProductsToCreate.Count;
    public int UpdateCount => ProductsToUpdate.Count;
    public int UnchangedCount => UnchangedNames.Count;
    public int ErrorRowCount => Issues.Count;

    public bool HasFileError => FileError is not null;

    /// <summary>넣거나 고칠 것이 하나라도 있는지. 없으면 진행을 물을 이유가 없다.</summary>
    public bool HasWork => ProductsToCreate.Count > 0 || ProductsToUpdate.Count > 0;
}
