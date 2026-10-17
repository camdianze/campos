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
                BoxUnitStock currentStock;

                using (var checkCommand = connection.CreateCommand())
                {
                    checkCommand.Transaction = dbTransaction;
                    checkCommand.CommandText = """
                        SELECT current_quantity, box_quantity, unit_quantity
                        FROM Inventory WHERE inventory_id = $inventoryId;
                        """;
                    checkCommand.Parameters.AddWithValue("$inventoryId", line.InventoryId);

                    using var reader = await checkCommand.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        dbTransaction.Rollback();
                        return false;
                    }

                    currentStock = new BoxUnitStock(
                        reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
                }

                // 박스 판매는 안 뜯은 박스가 있어야 하고, 낱개 판매는 헐어 놓은 낱개가
                // 모자라면 여기서 박스를 헌다. "박스를 여시겠습니까?"는 장바구니에 담을 때
                // 이미 물어봤고, 실제로 여는 건 재고를 잠근 이 안에서만 해야 안전하다.
                var succeeded = line.IsBoxSale
                    ? BoxUnitMath.TryTakeBoxes(
                        currentStock, line.BoxCount, line.UnitsPerBox, out var newStock)
                    : BoxUnitMath.TryTakeUnits(
                        currentStock, line.Transaction.Quantity, line.UnitsPerBox, out newStock);

                if (!succeeded)
                {
                    dbTransaction.Rollback();
                    return false;
                }

                using (var updateCommand = connection.CreateCommand())
                {
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

                // 차감 전후 재고를 원장에 남긴다. after는 위에서 계산한 newStock이 아니라
                // 갱신된 Inventory에서 다시 읽는다 — 계산값을 넣으면 늘 맞아떨어져서
                // 정작 찾으려는 "원장과 재고가 어긋난 경우"를 못 잡는다.
                var stockAfter = await StockLedgerTrace.ReadQuantityByInventoryIdAsync(
                    connection, dbTransaction, line.InventoryId);

                await StockLedgerTrace.RecordAsync(
                    connection, dbTransaction, line.Transaction.TransactionId,
                    currentStock.TotalUnits, stockAfter);
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