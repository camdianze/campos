using Microsoft.Data.Sqlite;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// 원장 한 줄에 "그 배치의 재고가 직전·직후에 얼마였는지"를 남긴다.
///
/// 왜 계산하지 않고 두 번 읽는가: before + quantity = after 로 계산해 넣으면 그 식은
/// 항상 참이라 아무것도 검증하지 못한다. 실제로 찾으려는 것은 <b>원장이 적은 수량과
/// 재고에 실제로 일어난 일이 다른 경우</b>이므로, 양쪽 모두 Inventory에서 읽어야 한다.
///
/// 재고를 바꾼 뒤에 원장 행을 갱신하는 방식인 이유: 저장소마다 원장 INSERT와 재고 변경의
/// 순서가 다르다(입고·환불은 원장이 먼저다). 순서를 맞추려고 네 곳을 뒤집는 것보다,
/// 같은 트랜잭션 안에서 마지막에 한 번 갱신하는 편이 안전하다.
/// </summary>
internal static class StockLedgerTrace
{
    /// <summary>inventory_id로 현재 수량을 읽는다. 행이 없으면 null.</summary>
    public static async Task<long?> ReadQuantityByInventoryIdAsync(
        SqliteConnection connection, SqliteTransaction dbTransaction, string inventoryId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = "SELECT current_quantity FROM Inventory WHERE inventory_id = $inventoryId;";
        command.Parameters.AddWithValue("$inventoryId", inventoryId);

        return await ReadScalarAsync(command);
    }

    /// <summary>
    /// 시설+상품+배치로 현재 수량을 읽는다. Inventory가 이 셋으로 유일하다.
    /// 아직 그 배치가 없으면 null — 입고로 배치가 처음 생기는 경우다.
    /// </summary>
    public static async Task<long?> ReadQuantityByBatchAsync(
        SqliteConnection connection, SqliteTransaction dbTransaction,
        string facilityId, string productId, string batchNumber)
    {
        using var command = connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = """
            SELECT current_quantity FROM Inventory
            WHERE facility_id = $facilityId
              AND product_id = $productId
              AND batch_number = $batchNumber;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);
        command.Parameters.AddWithValue("$productId", productId);
        command.Parameters.AddWithValue("$batchNumber", batchNumber);

        return await ReadScalarAsync(command);
    }

    /// <summary>이미 써 넣은 원장 행에 직전·직후 재고를 붙인다.</summary>
    public static async Task RecordAsync(
        SqliteConnection connection, SqliteTransaction dbTransaction,
        string transactionId, long? before, long? after)
    {
        using var command = connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = """
            UPDATE Stock_Transaction
            SET stock_before = $before,
                stock_after = $after
            WHERE transaction_id = $transactionId;
            """;
        command.Parameters.AddWithValue("$transactionId", transactionId);
        command.Parameters.AddWithValue("$before", (object?)before ?? DBNull.Value);
        command.Parameters.AddWithValue("$after", (object?)after ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long?> ReadScalarAsync(SqliteCommand command)
    {
        var result = await command.ExecuteScalarAsync();

        return result is null or DBNull ? null : Convert.ToInt64(result);
    }
}
