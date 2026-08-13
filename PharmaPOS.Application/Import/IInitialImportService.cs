using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Import;

/// <summary>
/// 수기로 관리하던 약국의 실사 파일 하나로 상품과 재고를 차례로 들여오는 기능.
///
/// 흐름은 언제나 세 단계다: 같은 파일인지 확인(WasAlreadyImportedAsync) →
/// 무엇이 들어가는지 계산(Plan…) → 사용자가 확인한 뒤 반영(Apply…).
/// 계산과 반영을 나눈 이유는 미리보기에서 본 숫자와 실제로 들어가는 것이 같아야 하기 때문이다.
/// </summary>
public interface IInitialImportService
{
    /// <summary>
    /// 같은 내용의 파일을 같은 종류로 이미 넣었는지. 넣었다면 진행을 막아야 한다
    /// (재고가 두 배가 되는 사고는 되돌리기가 매우 번거롭다).
    /// </summary>
    Task<bool> WasAlreadyImportedAsync(ImportType importType, string fileHash);

    /// <summary>파일 행들을 읽어 등록할 상품과 건너뛸 행을 계산한다. DB는 건드리지 않는다.</summary>
    Task<ProductImportPlan> PlanProductsAsync(IReadOnlyList<ImportSourceRow> rows);

    /// <summary>계산된 상품을 저장하고 임포트 이력을 남긴다.</summary>
    Task<ImportApplyResult> ApplyProductsAsync(
        ProductImportPlan plan, string fileHash, string? fileName, string facilityId);

    /// <summary>
    /// 파일 행들을 읽어 만들 배치를 계산한다. 상품은 이미 등록돼 있어야 하며,
    /// 못 찾은 행은 실패로 따로 모은다.
    /// </summary>
    Task<InventoryImportPlan> PlanInventoryAsync(IReadOnlyList<ImportSourceRow> rows);

    /// <summary>계산된 배치를 입고로 기록하고 임포트 이력을 남긴다.</summary>
    Task<ImportApplyResult> ApplyInventoryAsync(
        InventoryImportPlan plan, string fileHash, string? fileName, string facilityId, string userId);
}
