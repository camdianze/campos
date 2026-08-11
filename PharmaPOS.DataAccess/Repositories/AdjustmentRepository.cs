using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IAdjustmentRepository의 SQLite 구현체.
/// </summary>
public class AdjustmentRepository : IAdjustmentRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AdjustmentRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> SaveAdjustmentAsync(
        StockTransaction transaction,
        string inventoryId,
        int expectedCurrentQuantity,
        int physicalCount,
        int physicalBoxCount,
        int physicalUnitCount)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var dbTransaction = connection.BeginTransaction();

        try
        {
            int rowsAffected;

            using (var updateCommand = connection.CreateCommand())
            {
                updateCommand.Transaction = dbTransaction;
                updateCommand.CommandText = """
                    UPDATE Inventory
                    SET current_quantity = $physicalCount,
                        box_quantity = $physicalBoxCount,
                        unit_quantity = $physicalUnitCount,
                        updated_at = $updatedAt
                    WHERE inventory_id = $inventoryId
                      AND current_quantity = $expectedCurrentQuantity;
                    """;
                updateCommand.Parameters.AddWithValue("$physicalCount", physicalCount);
                updateCommand.Parameters.AddWithValue("$physicalBoxCount", physicalBoxCount);
                updateCommand.Parameters.AddWithValue("$physicalUnitCount", physicalUnitCount);
                updateCommand.Parameters.AddWithValue("$updatedAt", transaction.TransactionTime);
                updateCommand.Parameters.AddWithValue("$inventoryId", inventoryId);
                updateCommand.Parameters.AddWithValue("$expectedCurrentQuantity", expectedCurrentQuantity);

                rowsAffected = await updateCommand.ExecuteNonQueryAsync();
            }

            if (rowsAffected == 0)
            {
                dbTransaction.Rollback();
                return false;
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
                         NULL, NULL, NULL, $reason,
                         $transactionTime);
                    """;
                insertCommand.Parameters.AddWithValue("$transactionId", transaction.TransactionId);
                insertCommand.Parameters.AddWithValue("$facilityId", transaction.FacilityId);
                insertCommand.Parameters.AddWithValue("$productId", transaction.ProductId);
                insertCommand.Parameters.AddWithValue("$userId", transaction.UserId);
                insertCommand.Parameters.AddWithValue("$transactionType", transaction.TransactionType.ToString());
                insertCommand.Parameters.AddWithValue("$batchNumber", transaction.BatchNumber);
                insertCommand.Parameters.AddWithValue("$expiryDate", transaction.ExpiryDate);
                insertCommand.Parameters.AddWithValue("$quantity", transaction.Quantity);
                insertCommand.Parameters.AddWithValue("$reason", transaction.Reason ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("$transactionTime", transaction.TransactionTime);
                await insertCommand.ExecuteNonQueryAsync();
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