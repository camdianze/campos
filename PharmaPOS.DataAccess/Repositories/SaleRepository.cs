using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// ISaleRepository의 SQLite 구현체.
/// </summary>
public class SaleRepository : ISaleRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SaleRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> SaveSaleAsync(IReadOnlyList<SaleLineForSave> lines)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var dbTransaction = connection.BeginTransaction();

        try
        {
            foreach (var line in lines)
            {
                // 저장 직전 재고 재확인 (Add to Cart 시점 이후 다른 판매가
                // 먼저 재고를 가져갔을 수 있으므로 다시 확인한다).
                long currentQuantity;

                using (var checkCommand = connection.CreateCommand())
                {
                    checkCommand.Transaction = dbTransaction;
                    checkCommand.CommandText = """
                        SELECT current_quantity FROM Inventory WHERE inventory_id = $inventoryId;
                        """;
                    checkCommand.Parameters.AddWithValue("$inventoryId", line.InventoryId);

                    var result = await checkCommand.ExecuteScalarAsync();
                    if (result is null)
                    {
                        dbTransaction.Rollback();
                        return false;
                    }

                    currentQuantity = (long)result;
                }

                if (currentQuantity < line.Transaction.Quantity)
                {
                    dbTransaction.Rollback();
                    return false;
                }

                using (var updateCommand = connection.CreateCommand())
                {
                    updateCommand.Transaction = dbTransaction;
                    updateCommand.CommandText = """
                        UPDATE Inventory
                        SET current_quantity = current_quantity - $quantity,
                            updated_at = $updatedAt
                        WHERE inventory_id = $inventoryId;
                        """;
                    updateCommand.Parameters.AddWithValue("$quantity", line.Transaction.Quantity);
                    updateCommand.Parameters.AddWithValue("$updatedAt", line.Transaction.TransactionTime);
                    updateCommand.Parameters.AddWithValue("$inventoryId", line.InventoryId);
                    await updateCommand.ExecuteNonQueryAsync();
                }

                using (var insertCommand = connection.CreateCommand())
                {
                    insertCommand.Transaction = dbTransaction;
                    insertCommand.CommandText = """
                        INSERT INTO Stock_Transaction
                            (transaction_id, facility_id, product_id, user_id, transaction_type,
                             batch_number, expiry_date, quantity,
                             selling_price_at_transaction, payment_method, total_amount, reason,
                             transaction_time)
                        VALUES
                            ($transactionId, $facilityId, $productId, $userId, $transactionType,
                             $batchNumber, $expiryDate, $quantity,
                             $sellingPrice, $paymentMethod, $totalAmount, NULL,
                             $transactionTime);
                        """;
                    var t = line.Transaction;
                    insertCommand.Parameters.AddWithValue("$transactionId", t.TransactionId);
                    insertCommand.Parameters.AddWithValue("$facilityId", t.FacilityId);
                    insertCommand.Parameters.AddWithValue("$productId", t.ProductId);
                    insertCommand.Parameters.AddWithValue("$userId", t.UserId);
                    insertCommand.Parameters.AddWithValue("$transactionType", t.TransactionType.ToString());
                    insertCommand.Parameters.AddWithValue("$batchNumber", t.BatchNumber);
                    insertCommand.Parameters.AddWithValue("$expiryDate", t.ExpiryDate);
                    insertCommand.Parameters.AddWithValue("$quantity", t.Quantity);
                    insertCommand.Parameters.AddWithValue("$sellingPrice", t.SellingPriceAtTransaction!.Value);
                    insertCommand.Parameters.AddWithValue("$paymentMethod", t.PaymentMethod!);
                    insertCommand.Parameters.AddWithValue("$totalAmount", t.TotalAmount!.Value);
                    insertCommand.Parameters.AddWithValue("$transactionTime", t.TransactionTime);
                    await insertCommand.ExecuteNonQueryAsync();
                }
            }

            dbTransaction.Commit();
            return true;
        }
        catch
        {
            dbTransaction.Rollback();
            throw;
        }
    }
}