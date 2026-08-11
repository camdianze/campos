using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IRefundRepository의 SQLite 구현체.
///
/// 기환불 수량은 어디에도 저장하지 않고 언제나 세어서 구한다
/// (related_transaction_id가 그 판매 줄을 가리키는 Refund 행들의 수량 합).
/// 저장해 두면 원장과 어긋날 수 있는데, 원장 쪽이 진실이라 어긋나면 답이 없다.
/// </summary>
public class RefundRepository : IRefundRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public RefundRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RefundableLine>> GetRefundableLinesAsync(
        string facilityId, long transactionTime, string soldByUserId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT st.transaction_id, st.product_id,
                   COALESCE(p.product_name, st.product_id) AS product_name,
                   st.batch_number, st.expiry_date, st.quantity,
                   COALESCE((SELECT -SUM(r.quantity)
                             FROM Stock_Transaction r
                             WHERE r.related_transaction_id = st.transaction_id
                               AND r.transaction_type = 'Refund'), 0) AS refunded_quantity,
                   st.selling_price_at_transaction, st.total_amount, st.payment_method,
                   COALESCE(p.units_per_box, 1)
            FROM Stock_Transaction st
            LEFT JOIN Product_Master p ON p.product_id = st.product_id
            WHERE st.facility_id = $facilityId
              AND st.transaction_type = 'StockOut'
              AND st.transaction_time = $transactionTime
              AND st.user_id = $userId
            ORDER BY product_name;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);
        command.Parameters.AddWithValue("$transactionTime", transactionTime);
        command.Parameters.AddWithValue("$userId", soldByUserId);

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<RefundableLine>();
        while (await reader.ReadAsync())
        {
            results.Add(new RefundableLine
            {
                TransactionId = reader.GetString(0),
                ProductId = reader.GetString(1),
                ProductName = reader.GetString(2),
                BatchNumber = reader.GetString(3),
                ExpiryDate = reader.GetInt64(4),
                SoldQuantity = reader.GetInt32(5),
                RefundedQuantity = reader.GetInt32(6),
                UnitPrice = (decimal)reader.GetDouble(7),
                LineTotal = (decimal)reader.GetDouble(8),
                PaymentMethod = reader.GetString(9),
                UnitsPerBox = reader.GetInt32(10)
            });
        }

        return results;
    }

    public async Task<bool> SaveRefundAsync(IReadOnlyList<RefundLineForSave> lines)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var dbTransaction = connection.BeginTransaction();

        try
        {
            foreach (var line in lines)
            {
                var originalTransactionId = line.Transaction.RelatedTransactionId!;

                // 저장 직전 재확인. 환불 창을 띄워 둔 사이에 다른 창에서 같은 줄을
                // 환불했을 수 있다 — 판매 쪽이 재고를 다시 확인하는 것과 같은 이유다.
                int soldQuantity;
                int alreadyRefunded;

                using (var checkCommand = connection.CreateCommand())
                {
                    checkCommand.Transaction = dbTransaction;
                    checkCommand.CommandText = """
                        SELECT o.quantity,
                               COALESCE((SELECT -SUM(r.quantity)
                                         FROM Stock_Transaction r
                                         WHERE r.related_transaction_id = o.transaction_id
                                           AND r.transaction_type = 'Refund'), 0)
                        FROM Stock_Transaction o
                        WHERE o.transaction_id = $transactionId
                          AND o.transaction_type = 'StockOut';
                        """;
                    checkCommand.Parameters.AddWithValue("$transactionId", originalTransactionId);

                    using var reader = await checkCommand.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        dbTransaction.Rollback();
                        return false;
                    }

                    soldQuantity = reader.GetInt32(0);
                    alreadyRefunded = reader.GetInt32(1);
                }

                if (alreadyRefunded + line.RefundQuantity > soldQuantity)
                {
                    dbTransaction.Rollback();
                    return false;
                }

                if (line.ReturnToStock)
                {
                    await ReturnToStockAsync(connection, dbTransaction, line);
                }

                using (var insertCommand = connection.CreateCommand())
                {
                    insertCommand.Transaction = dbTransaction;
                    insertCommand.CommandText = """
                        INSERT INTO Stock_Transaction
                            (transaction_id, facility_id, product_id, user_id, transaction_type,
                             batch_number, expiry_date, quantity,
                             selling_price_at_transaction, payment_method, total_amount, reason,
                             related_transaction_id, transaction_time)
                        VALUES
                            ($transactionId, $facilityId, $productId, $userId, $transactionType,
                             $batchNumber, $expiryDate, $quantity,
                             $sellingPrice, $paymentMethod, $totalAmount, $reason,
                             $relatedTransactionId, $transactionTime);
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
                    // 메모는 선택 입력이라 비어 있을 수 있다.
                    insertCommand.Parameters.AddWithValue("$reason", t.Reason ?? (object)DBNull.Value);
                    insertCommand.Parameters.AddWithValue("$relatedTransactionId", originalTransactionId);
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

    /// <summary>
    /// 되돌아온 수량을 원래 배치에 더한다. 그 배치가 다 팔려 지워졌을 수 있으므로
    /// (재고 0인 배치는 화면에서 지울 수 있다) 없으면 판매 기록의 유효기한으로 다시 만든다.
    /// </summary>
    private static async Task ReturnToStockAsync(
        SqliteConnection connection, SqliteTransaction dbTransaction, RefundLineForSave line)
    {
        var t = line.Transaction;

        string? inventoryId = null;
        BoxUnitStock currentStock = default;

        using (var findCommand = connection.CreateCommand())
        {
            findCommand.Transaction = dbTransaction;
            findCommand.CommandText = """
                SELECT inventory_id, current_quantity, box_quantity, unit_quantity
                FROM Inventory
                WHERE facility_id = $facilityId
                  AND product_id = $productId
                  AND batch_number = $batchNumber;
                """;
            findCommand.Parameters.AddWithValue("$facilityId", t.FacilityId);
            findCommand.Parameters.AddWithValue("$productId", t.ProductId);
            findCommand.Parameters.AddWithValue("$batchNumber", t.BatchNumber);

            using var reader = await findCommand.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                inventoryId = reader.GetString(0);
                currentStock = new BoxUnitStock(
                    reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
            }
        }

        if (inventoryId is null)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = dbTransaction;
            insertCommand.CommandText = """
                INSERT INTO Inventory
                    (inventory_id, facility_id, product_id, batch_number, expiry_date,
                     current_quantity, box_quantity, unit_quantity, updated_at)
                VALUES
                    ($inventoryId, $facilityId, $productId, $batchNumber, $expiryDate,
                     $quantity, 0, $quantity, $updatedAt);
                """;
            insertCommand.Parameters.AddWithValue("$inventoryId", Guid.NewGuid().ToString());
            insertCommand.Parameters.AddWithValue("$facilityId", t.FacilityId);
            insertCommand.Parameters.AddWithValue("$productId", t.ProductId);
            insertCommand.Parameters.AddWithValue("$batchNumber", t.BatchNumber);
            insertCommand.Parameters.AddWithValue("$expiryDate", t.ExpiryDate);
            insertCommand.Parameters.AddWithValue("$quantity", line.RefundQuantity);
            insertCommand.Parameters.AddWithValue("$updatedAt", t.TransactionTime);
            await insertCommand.ExecuteNonQueryAsync();
            return;
        }

        var newStock = BoxUnitMath.AddUnits(currentStock, line.RefundQuantity, line.UnitsPerBox);

        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = dbTransaction;
        updateCommand.CommandText = """
            UPDATE Inventory
            SET current_quantity = $currentQuantity,
                box_quantity = $boxQuantity,
                unit_quantity = $unitQuantity,
                updated_at = $updatedAt
            WHERE inventory_id = $inventoryId;
            """;
        updateCommand.Parameters.AddWithValue("$currentQuantity", newStock.TotalUnits);
        updateCommand.Parameters.AddWithValue("$boxQuantity", newStock.BoxQuantity);
        updateCommand.Parameters.AddWithValue("$unitQuantity", newStock.UnitQuantity);
        updateCommand.Parameters.AddWithValue("$updatedAt", t.TransactionTime);
        updateCommand.Parameters.AddWithValue("$inventoryId", inventoryId);
        await updateCommand.ExecuteNonQueryAsync();
    }
}
