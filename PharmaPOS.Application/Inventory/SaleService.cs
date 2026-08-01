using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// ISaleService의 구현체.
/// Screen SCR-POS-005, 4절의 "판매 확정(Confirm Sale)" 이후 흐름을 그대로 코드로 옮긴 것이다.
/// 장바구니에 담는(Add to Cart) 단계의 검증은 ViewModel이 담당한다
/// (이미 로드된 배치 정보만으로 판단 가능해 DB 접근이 필요 없기 때문).
/// </summary>
public class SaleService : ISaleService
{
    private readonly ISaleRepository _saleRepository;

    public SaleService(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository;
    }

    public async Task<SaleResult> ConfirmSaleAsync(
        string facilityId,
        string userId,
        IReadOnlyList<SaleLineItem> cartItems,
        PaymentMethod? paymentMethod,
        decimal? cashTendered,
        string? notes,
        bool acknowledgeLowerSellingPriceWarning = false)
    {
        if (cartItems.Count == 0)
        {
            return SaleResult.Failure("Please add at least one product to the sale.");
        }

        if (paymentMethod is null)
        {
            return SaleResult.Failure("Please select a payment method.");
        }

        var totalAmount = cartItems.Sum(i => i.LineTotal);

        if (paymentMethod == PaymentMethod.Cash)
        {
            if (cashTendered is null)
            {
                return SaleResult.Failure("Please enter the cash tendered.");
            }

            if (cashTendered < totalAmount)
            {
                return SaleResult.Failure("Cash tendered is less than total amount.");
            }
        }

        foreach (var item in cartItems)
        {
            if (item.UnitPrice <= 0)
            {
                return SaleResult.Failure("Selling price must be greater than zero.");
            }
        }

        if (!acknowledgeLowerSellingPriceWarning && cartItems.Any(i => i.UnitPrice < i.CostPrice))
        {
            return SaleResult.NeedsConfirmation("Selling price is lower than cost price. Continue?");
        }

        // TODO: notes(비고/처방 참조)는 현재 Stock_Transaction 스키마에
        // 저장할 컬럼이 없어 저장하지 않는다. 필요 시 스키마에 notes 컬럼 추가 후 반영한다.

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var lines = cartItems.Select(item => new SaleLineForSave
        {
            InventoryId = item.InventoryId,
            Transaction = new StockTransaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                ProductId = item.ProductId,
                UserId = userId,
                TransactionType = TransactionType.StockOut,
                BatchNumber = item.BatchNumber,
                ExpiryDate = item.ExpiryDate,
                Quantity = item.Quantity,
                SellingPriceAtTransaction = item.UnitPrice,
                PaymentMethod = paymentMethod.Value.ToString(),
                TotalAmount = item.LineTotal,
                TransactionTime = now
            }
        }).ToList();

        bool saved;

        try
        {
            saved = await _saleRepository.SaveSaleAsync(lines);
        }
        catch (Exception)
        {
            return SaleResult.Failure("Sale could not be completed. Please try again.");
        }

        if (!saved)
        {
            return SaleResult.Failure("Some products do not have enough stock.");
        }

        // 저장된 줄과 거래 ID를 함께 돌려준다. 판매 헤더 테이블이 없어서,
        // 복약안내 로그를 거래에 붙이려면 호출자가 이 ID를 알아야 한다.
        var confirmedLines = lines
            .Select((line, index) => new ConfirmedSaleLine
            {
                TransactionId = line.Transaction.TransactionId,
                Line = cartItems[index]
            })
            .ToList();

        return SaleResult.Success(confirmedLines);
    }
}