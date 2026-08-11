using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// IRefundService의 구현체.
///
/// 원장은 append-only라 판매 행을 고치거나 지우지 않는다. 환불은 수량·금액을 음수로 담은
/// Refund 행을 새로 쌓고, related_transaction_id로 원 판매 줄을 가리키는 방식으로 남긴다.
/// 그래서 "얼마나 환불됐는가"는 언제나 그 줄을 가리키는 환불 행들의 합이고,
/// 매출 집계는 StockOut과 Refund를 함께 더하기만 하면 순매출이 된다.
///
/// 금액은 화면이 보낸 값을 쓰지 않는다. 판매 시점 단가 스냅샷을 DB에서 다시 읽어
/// 수량만 곱한다 — 그 사이 상품 가격이 바뀌었어도 손님이 낸 돈만 돌아가야 한다.
/// </summary>
public class RefundService : IRefundService
{
    private readonly IRefundRepository _refundRepository;

    public RefundService(IRefundRepository refundRepository)
    {
        _refundRepository = refundRepository;
    }

    public async Task<IReadOnlyList<RefundableLine>> GetRefundableLinesAsync(
        string facilityId, long transactionTime, string soldByUserId)
    {
        return await _refundRepository.GetRefundableLinesAsync(
            facilityId, transactionTime, soldByUserId);
    }

    public async Task<RefundResult> RefundAsync(
        string facilityId,
        string userId,
        long saleTransactionTime,
        string soldByUserId,
        IReadOnlyList<RefundLineRequest> lines,
        string? reason,
        bool returnToStock)
    {
        if (lines.Any(l => l.Quantity < 0))
        {
            return RefundResult.Failure("Refund quantity cannot be negative.");
        }

        // 같은 판매 줄이 두 번 실려 오면 하나씩은 한도 안이어도 합치면 판매 수량을 넘는다.
        // 줄별로 검사하기 전에 먼저 합쳐 두는 이유다.
        var requested = lines
            .Where(l => l.Quantity > 0)
            .GroupBy(l => l.TransactionId)
            .Select(g => new RefundLineRequest
            {
                TransactionId = g.Key,
                Quantity = g.Sum(l => l.Quantity)
            })
            .ToList();

        if (requested.Count == 0)
        {
            return RefundResult.Failure("Please enter the quantity to refund.");
        }

        // 사유(메모)는 선택 입력이다. 계산대 앞에서 손님을 세워 두고 글을 적게 하는 것보다
        // 환불이 빨리 끝나는 쪽이 낫다는 판단이며, 어차피 누가 언제 얼마를 되돌렸는지는
        // 환불 행 자체에 남는다.
        IReadOnlyList<RefundableLine> refundable;

        try
        {
            refundable = await _refundRepository.GetRefundableLinesAsync(
                facilityId, saleTransactionTime, soldByUserId);
        }
        catch (Exception)
        {
            return RefundResult.Failure("Refund could not be completed. Please try again.");
        }

        var byTransactionId = refundable.ToDictionary(l => l.TransactionId);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var toSave = new List<RefundLineForSave>();
        decimal refundedAmount = 0m;

        foreach (var request in requested)
        {
            if (!byTransactionId.TryGetValue(request.TransactionId, out var line))
            {
                return RefundResult.Failure("The sale record could not be found. Please reload and try again.");
            }

            if (request.Quantity > line.RemainingQuantity)
            {
                return RefundResult.Failure(
                    $"{line.ProductName}: refund quantity exceeds the quantity available to refund.");
            }

            var lineAmount = line.UnitPrice * request.Quantity;
            refundedAmount += lineAmount;

            toSave.Add(new RefundLineForSave
            {
                Transaction = new StockTransaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    ProductId = line.ProductId,
                    UserId = userId,
                    TransactionType = TransactionType.Refund,
                    BatchNumber = line.BatchNumber,
                    ExpiryDate = line.ExpiryDate,
                    // 수량과 금액을 음수로 넣는 게 이 기능의 핵심이다.
                    Quantity = -request.Quantity,
                    SellingPriceAtTransaction = line.UnitPrice,
                    PaymentMethod = line.PaymentMethod,
                    TotalAmount = -lineAmount,
                    Reason = BuildReason(reason, returnToStock),
                    RelatedTransactionId = line.TransactionId,
                    TransactionTime = now
                },
                RefundQuantity = request.Quantity,
                ReturnToStock = returnToStock,
                UnitsPerBox = line.UnitsPerBox
            });
        }

        bool saved;

        try
        {
            saved = await _refundRepository.SaveRefundAsync(toSave);
        }
        catch (Exception)
        {
            return RefundResult.Failure("Refund could not be completed. Please try again.");
        }

        if (!saved)
        {
            return RefundResult.Failure(
                "This sale was refunded elsewhere in the meantime. Please reload and try again.");
        }

        return RefundResult.Success(refundedAmount);
    }

    /// <summary>
    /// 재고를 되돌리지 않은 환불에는 표시를 남긴다. 사유를 안 적었어도 이 표시는 남긴다 —
    /// 재고가 왜 안 늘었는지는 원장에서 이것 말고 알 길이 없다. 환불 행의 수량은
    /// 매출을 되돌리는 값이지 재고가 실제로 돌아왔다는 뜻이 아니기 때문이다.
    /// </summary>
    private static string? BuildReason(string? reason, bool returnToStock)
    {
        var trimmed = reason?.Trim() ?? string.Empty;

        if (returnToStock)
        {
            return trimmed.Length == 0 ? null : trimmed;
        }

        return trimmed.Length == 0 ? "Not returned to stock" : $"{trimmed} (not returned to stock)";
    }
}
