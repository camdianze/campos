namespace PharmaPOS.Application.Inventory;

public enum ExpiryFilterOption
{
    All,
    Expired,
    Within7Days,
    Within30Days,
    Within90Days
}

public enum InventorySortOption
{
    ProductName,
    Quantity,
    ExpiryDate
}