using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// IAlertService의 구현체.
/// Screen SCR-ALERT-014, 4절의 우선순위 분류 기준을 그대로 코드로 옮긴 것이다.
/// </summary>
public class AlertService : IAlertService
{
    private readonly IAlertRepository _alertRepository;

    public AlertService(IAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<IReadOnlyList<AlertItem>> GetAlertsAsync(
        string facilityId,
        AlertTypeFilter typeFilter,
        AlertPriorityFilter priorityFilter)
    {
        var results = new List<AlertItem>();

        if (typeFilter is AlertTypeFilter.All or AlertTypeFilter.LowStock)
        {
            var lowStockCandidates = await _alertRepository.GetLowStockCandidatesAsync(facilityId);

            foreach (var candidate in lowStockCandidates)
            {
                results.Add(new AlertItem
                {
                    AlertType = AlertType.LowStock,
                    Priority = ClassifyLowStockPriority(candidate),
                    ProductId = candidate.ProductId,
                    ProductName = candidate.ProductName,
                    Quantity = candidate.TotalQuantity,
                    SafetyStockLevel = candidate.SafetyStockLevel
                });
            }
        }

        if (typeFilter is AlertTypeFilter.All or AlertTypeFilter.Expiry)
        {
            var expiryCandidates = await _alertRepository.GetExpiryCandidatesAsync(facilityId);

            foreach (var candidate in expiryCandidates)
            {
                results.Add(new AlertItem
                {
                    AlertType = AlertType.Expiry,
                    Priority = ClassifyExpiryPriority(candidate.ExpiryDate),
                    ProductId = candidate.ProductId,
                    ProductName = candidate.ProductName,
                    Quantity = candidate.Quantity,
                    BatchNumber = candidate.BatchNumber,
                    ExpiryDate = candidate.ExpiryDate
                });
            }
        }

        if (priorityFilter != AlertPriorityFilter.All)
        {
            var targetPriority = priorityFilter switch
            {
                AlertPriorityFilter.Critical => AlertPriority.Critical,
                AlertPriorityFilter.Warning => AlertPriority.Warning,
                _ => AlertPriority.Normal
            };

            results = results.Where(a => a.Priority == targetPriority).ToList();
        }

        return results;
    }

    /// <summary>
    /// Low Stock 우선순위: 0개=Critical, Safety Stock의 50% 미만=Warning, 그 외 저재고=Normal.
    /// </summary>
    private static AlertPriority ClassifyLowStockPriority(LowStockCandidate candidate)
    {
        if (candidate.TotalQuantity == 0)
        {
            return AlertPriority.Critical;
        }

        if (candidate.TotalQuantity < candidate.SafetyStockLevel * 0.5)
        {
            return AlertPriority.Warning;
        }

        return AlertPriority.Normal;
    }

    /// <summary>
    /// Expiry 우선순위: 이미 만료 또는 7일 이내=Critical, 30일 이내=Warning, 90일 이내=Normal.
    /// </summary>
    private static AlertPriority ClassifyExpiryPriority(long expiryDate)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var daysUntilExpiry = (expiryDate - now) / 86400000.0;

        if (daysUntilExpiry <= 7)
        {
            return AlertPriority.Critical;
        }

        if (daysUntilExpiry <= 30)
        {
            return AlertPriority.Warning;
        }

        return AlertPriority.Normal;
    }
}