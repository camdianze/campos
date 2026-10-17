using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// ISalesHistoryService의 구현체.
/// </summary>
public class SalesHistoryService : ISalesHistoryService
{
    private readonly ISalesHistoryRepository _salesHistoryRepository;

    public SalesHistoryService(ISalesHistoryRepository salesHistoryRepository)
    {
        _salesHistoryRepository = salesHistoryRepository;
    }

    public async Task<SalesHistoryQueryResult> SearchAsync(
        string facilityId,
        DateTime? dateFrom,
        DateTime? dateTo,
        string searchTerm,
        PaymentMethod? paymentMethod)
    {
        if (dateFrom is not null && dateTo is not null && dateFrom > dateTo)
        {
            return SalesHistoryQueryResult.Failure("Start date cannot be later than end date.");
        }

        long? dateFromUtc = dateFrom is not null
            ? new DateTimeOffset(dateFrom.Value.Date).ToUnixTimeMilliseconds()
            : null;

        // 종료일은 "그 날짜 전체"를 포함해야 하므로 다음날 자정 직전까지로 계산한다.
        long? dateToUtc = dateTo is not null
            ? new DateTimeOffset(dateTo.Value.Date.AddDays(1).AddMilliseconds(-1)).ToUnixTimeMilliseconds()
            : null;

        string? paymentMethodString = paymentMethod?.ToString();

        IReadOnlyList<SalesHistoryLineItem> items;

        try
        {
            items = await _salesHistoryRepository.SearchAsync(
                facilityId, dateFromUtc, dateToUtc, searchTerm, paymentMethodString);
        }
        catch (Exception)
        {
            return SalesHistoryQueryResult.Failure("Sales history could not be loaded.");
        }

        return SalesHistoryQueryResult.Success(items);
    }

    public async Task<IReadOnlyList<SalesHistoryLineItem>> GetTransactionGroupAsync(
        string facilityId, SalesHistoryLineItem selectedLine)
    {
        return await _salesHistoryRepository.GetTransactionGroupAsync(
            facilityId, selectedLine.TransactionTime, selectedLine.UserId);
    }
}