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

    public async Task<bool> BatchNumberExistsAsync(
        string facilityId, string productId, string batchNumber, string excludeInventoryId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM Inventory
            WHERE facility_id = $facilityId
              AND product_id = $productId
              AND batch_number = $batchNumber
              AND inventory_id <> $excludeInventoryId;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);
        command.Parameters.AddWithValue("$productId", productId);
        command.Parameters.AddWithValue("$batchNumber", batchNumber);
        command.Parameters.AddWithValue("$excludeInventoryId", excludeInventoryId);

        var count = await command.ExecuteScalarAsync();

        return Convert.ToInt64(count) > 0;
    }

    public async Task<bool> SaveAdjustmentAsync(
        StockTransaction transaction,
        string inventoryId,
        string batchNumber,
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
                // 배치번호도 함께 넣는다. 바꾸지 않았으면 같은 값이 다시 들어갈 뿐이다.
                // 초기 임포트로 배치번호 없이 들어온 재고에 나중에 번호를 붙이는 경로다.
                updateCommand.CommandText = """
                    UPDATE Inventory
                    SET current_quantity = $physicalCount,
                        box_quantity = $physicalBoxCount,
                        unit_quantity = $physicalUnitCount,
                        batch_number = $batchNumber,
                        updated_at = $updatedAt
                    WHERE inventory_id = $inventoryId
                      AND current_quantity = $expectedCurrentQuantity;
                    """;
                updateCommand.Parameters.AddWithValue("$batchNumber", batchNumber);
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

            // before는 expectedCurrentQuantity를 그대로 쓴다. 위 UPDATE가
            // "current_quantity = $expectedCurrentQuantity"를 조건으로 걸고 성공했으므로,
            // 그 값이 실제 재고였다는 것을 DB가 확인해 준 셈이다.
            var stockAfter = await StockLedgerTrace.ReadQuantityByInventoryIdAsync(
                connection, dbTransaction, inventoryId);

            await StockLedgerTrace.RecordAsync(
                connection, dbTransaction, transaction.TransactionId,
                expectedCurrentQuantity, stockAfter);

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