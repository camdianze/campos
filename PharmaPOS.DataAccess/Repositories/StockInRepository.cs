using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IStockInRepository의 SQLite 구현체.
/// </summary>
public class StockInRepository : IStockInRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public StockInRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveStockInAsync(StockTransaction transaction)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var dbTransaction = connection.BeginTransaction();

        try
        {
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
                         NULL, NULL, NULL, NULL,
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
                insertCommand.Parameters.AddWithValue("$transactionTime", transaction.TransactionTime);
                await insertCommand.ExecuteNonQueryAsync();
            }

            // 동일 facility+product+batch가 Inventory에 이미 있는지 확인 (UPSERT).
            long existingQuantity = -1;

            using (var checkCommand = connection.CreateCommand())
            {
                checkCommand.Transaction = dbTransaction;
                checkCommand.CommandText = """
                    SELECT current_quantity FROM Inventory
                    WHERE facility_id = $facilityId
                      AND product_id = $productId
                      AND batch_number = $batchNumber;
                    """;
                checkCommand.Parameters.AddWithValue("$facilityId", transaction.FacilityId);
                checkCommand.Parameters.AddWithValue("$productId", transaction.ProductId);
                checkCommand.Parameters.AddWithValue("$batchNumber", transaction.BatchNumber);

                var result = await checkCommand.ExecuteScalarAsync();
                if (result is not null)
                {
                    existingQuantity = (long)result;
                }
            }

            if (existingQuantity >= 0)
            {
                // 기존 배치 — 수량 증가
                using var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = dbTransaction;
                updateCommand.CommandText = """
                    UPDATE Inventory
                    SET current_quantity = current_quantity + $quantity,
                        updated_at = $updatedAt
                    WHERE facility_id = $facilityId
                      AND product_id = $productId
                      AND batch_number = $batchNumber;
                    """;
                updateCommand.Parameters.AddWithValue("$quantity", transaction.Quantity);
                updateCommand.Parameters.AddWithValue("$updatedAt", transaction.TransactionTime);
                updateCommand.Parameters.AddWithValue("$facilityId", transaction.FacilityId);
                updateCommand.Parameters.AddWithValue("$productId", transaction.ProductId);
                updateCommand.Parameters.AddWithValue("$batchNumber", transaction.BatchNumber);
                await updateCommand.ExecuteNonQueryAsync();
            }
            else
            {
                // 신규 배치 — Inventory에 새 행 생성
                using var insertInventoryCommand = connection.CreateCommand();
                insertInventoryCommand.Transaction = dbTransaction;
                insertInventoryCommand.CommandText = """
                    INSERT INTO Inventory
                        (inventory_id, facility_id, product_id, batch_number, expiry_date,
                         current_quantity, updated_at)
                    VALUES
                        ($inventoryId, $facilityId, $productId, $batchNumber, $expiryDate,
                         $quantity, $updatedAt);
                    """;
                insertInventoryCommand.Parameters.AddWithValue("$inventoryId", Guid.NewGuid().ToString());
                insertInventoryCommand.Parameters.AddWithValue("$facilityId", transaction.FacilityId);
                insertInventoryCommand.Parameters.AddWithValue("$productId", transaction.ProductId);
                insertInventoryCommand.Parameters.AddWithValue("$batchNumber", transaction.BatchNumber);
                insertInventoryCommand.Parameters.AddWithValue("$expiryDate", transaction.ExpiryDate);
                insertInventoryCommand.Parameters.AddWithValue("$quantity", transaction.Quantity);
                insertInventoryCommand.Parameters.AddWithValue("$updatedAt", transaction.TransactionTime);
                await insertInventoryCommand.ExecuteNonQueryAsync();
            }

            dbTransaction.Commit();
        }
        catch
        {
            dbTransaction.Rollback();
            throw;
        }
    }
}