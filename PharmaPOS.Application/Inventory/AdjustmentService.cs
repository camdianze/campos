using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// IAdjustmentService의 구현체.
/// Screen SCR-ADJ-010, 4절 흐름을 기반으로 하되, 아래 한 가지는
/// 의도적으로 스펙과 다르게 구현했다:
///   - 원 스펙: Reason은 Delta 값과 무관하게 항상 필수.
///   - 변경 사항: 실사 결과 수량 차이가 없는 경우(Delta=0)는 Reason을
///     선택사항으로 둔다 (제품 오너 결정, 2026-07 반영).
/// </summary>
public class AdjustmentService : IAdjustmentService
{
    private readonly Repositories.IAdjustmentRepository _adjustmentRepository;

    public AdjustmentService(Repositories.IAdjustmentRepository adjustmentRepository)
    {
        _adjustmentRepository = adjustmentRepository;
    }

    public async Task<AdjustmentResult> SaveAdjustmentAsync(
        string facilityId,
        string productId,
        string userId,
        string inventoryId,
        string batchNumber,
        string originalBatchNumber,
        long expiryDate,
        int systemQuantity,
        int physicalBoxCount,
        int physicalUnitCount,
        int unitsPerBox,
        string reason,
        bool allowZeroDelta = false)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return AdjustmentResult.Failure("Please select a product.");
        }

        if (string.IsNullOrWhiteSpace(inventoryId))
        {
            return AdjustmentResult.Failure("Please select a batch.");
        }

        // 배치번호가 비어 있어도 막지 않는다. 배치번호 없이 관리하던 약국의 초기 재고가
        // 그렇게 들어오고, 그 재고를 조정하지도 못하게 되면 초기 데이터를 손볼 방법이 없다.
        var newBatchNumber = batchNumber?.Trim() ?? string.Empty;
        var currentBatchNumber = originalBatchNumber?.Trim() ?? string.Empty;
        var batchNumberChanged = !string.Equals(newBatchNumber, currentBatchNumber, StringComparison.Ordinal);

        if (batchNumberChanged && newBatchNumber.Length > 0)
        {
            bool exists;

            try
            {
                exists = await _adjustmentRepository.BatchNumberExistsAsync(
                    facilityId, productId, newBatchNumber, inventoryId);
            }
            catch (Exception)
            {
                return AdjustmentResult.Failure("Adjustment could not be saved.");
            }

            // Inventory가 (시설 + 상품 + 배치번호)로 유일하다. 겹치면 저장이 실패하는데,
            // 그때 나오는 DB 오류만으로는 무엇이 문제인지 알 수 없어 여기서 미리 막는다.
            if (exists)
            {
                return AdjustmentResult.Failure(
                    "This batch number is already used by another batch of this product.");
            }
        }

        if (physicalBoxCount < 0 || physicalUnitCount < 0)
        {
            return AdjustmentResult.Failure("Physical count cannot be negative.");
        }

        // 실사 결과를 그대로 저장한다. 낱개가 박스당 개수를 넘어도(예: 30개들이인데
        // 낱개 35개) 되돌리지 않는다 — 센 대로 적는 게 실사이고, 판매 쪽 계산은
        // 낱개가 남아도는 상태를 이미 감당한다.
        var physicalCount = BoxUnitMath.ToTotalUnits(physicalBoxCount, physicalUnitCount, unitsPerBox);

        var delta = physicalCount - systemQuantity;

        // 정책 변경: Delta가 0이 아닐 때만 Reason을 필수로 요구한다.
        // 수량 변화가 없는 경우(Delta=0)는 Reason 없이도 진행할 수 있다.
        if (delta != 0 && string.IsNullOrWhiteSpace(reason))
        {
            return AdjustmentResult.Failure("Please enter the adjustment reason.");
        }

        // 수량 차이가 없어도 배치번호를 고쳤으면 저장할 이유가 있다.
        // 그때까지 "차이가 없습니다"를 물으면 번호만 고치려던 사람이 매번 확인을 눌러야 한다.
        if (delta == 0 && !allowZeroDelta && !batchNumberChanged)
        {
            return AdjustmentResult.NeedsConfirmation("No quantity difference was found.");
        }

        var transaction = new StockTransaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            ProductId = productId,
            UserId = userId,
            TransactionType = TransactionType.Adjustment,
            BatchNumber = newBatchNumber,
            ExpiryDate = expiryDate,
            Quantity = delta,
            Reason = BuildReason(reason, batchNumberChanged, currentBatchNumber, newBatchNumber),
            TransactionTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        bool saved;

        try
        {
            saved = await _adjustmentRepository.SaveAdjustmentAsync(
                transaction, inventoryId, newBatchNumber, systemQuantity, physicalCount,
                physicalBoxCount, physicalUnitCount);
        }
        catch (Exception)
        {
            return AdjustmentResult.Failure("Adjustment could not be saved.");
        }

        if (!saved)
        {
            return AdjustmentResult.ConcurrencyConflict();
        }

        return AdjustmentResult.Success();
    }

    /// <summary>
    /// 조정 사유. 배치번호를 고쳤으면 그 사실을 함께 남긴다.
    ///
    /// 원장은 append-only라 이미 쌓인 입고·판매 행의 배치번호는 예전 값 그대로 남는다.
    /// 그 행들이 왜 다른 번호를 달고 있는지 설명할 곳이 이 사유뿐이다.
    /// </summary>
    private static string? BuildReason(
        string? reason, bool batchNumberChanged, string oldBatchNumber, string newBatchNumber)
    {
        var trimmed = reason?.Trim() ?? string.Empty;

        if (!batchNumberChanged)
        {
            return trimmed.Length == 0 ? null : trimmed;
        }

        var note = $"Batch number: {Describe(oldBatchNumber)} → {Describe(newBatchNumber)}";

        return trimmed.Length == 0 ? note : $"{trimmed} ({note})";

        static string Describe(string batchNumber) =>
            batchNumber.Length == 0 ? "(none)" : batchNumber;
    }
}