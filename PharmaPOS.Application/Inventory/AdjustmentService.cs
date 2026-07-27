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
        long expiryDate,
        int systemQuantity,
        int physicalCount,
        string reason,
        bool allowZeroDelta = false)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return AdjustmentResult.Failure("Please select a product.");
        }

        if (string.IsNullOrWhiteSpace(batchNumber))
        {
            return AdjustmentResult.Failure("Please select a batch number.");
        }

        if (physicalCount < 0)
        {
            return AdjustmentResult.Failure("Physical count cannot be negative.");
        }

        var delta = physicalCount - systemQuantity;

        // 정책 변경: Delta가 0이 아닐 때만 Reason을 필수로 요구한다.
        // 수량 변화가 없는 경우(Delta=0)는 Reason 없이도 진행할 수 있다.
        if (delta != 0 && string.IsNullOrWhiteSpace(reason))
        {
            return AdjustmentResult.Failure("Please enter the adjustment reason.");
        }

        if (delta == 0 && !allowZeroDelta)
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
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            Quantity = delta,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason,
            TransactionTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        bool saved;

        try
        {
            saved = await _adjustmentRepository.SaveAdjustmentAsync(
                transaction, inventoryId, systemQuantity, physicalCount);
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
}