using System.Globalization;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IReceiptNumberRepository의 SQLite 구현체.
///
/// 발번은 전부 하나의 트랜잭션 안에서 한다. 창을 두 개 띄워 놓고 동시에 판매를
/// 확정해도 같은 번호가 두 번 나가지 않아야 하기 때문이다.
/// </summary>
public class ReceiptNumberRepository : IReceiptNumberRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ReceiptNumberRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string?> FindAsync(string saleKey)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT receipt_no FROM Receipt_Number WHERE sale_key = $saleKey;
            """;
        command.Parameters.AddWithValue("$saleKey", saleKey);

        return await command.ExecuteScalarAsync() as string;
    }

    public async Task<string> IssueAsync(string saleKey, string counterKey, string format, long issuedAt)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        // 재출력이면 여기서 끝난다. 일련번호는 올리지 않는다.
        using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = """
                SELECT receipt_no FROM Receipt_Number WHERE sale_key = $saleKey;
                """;
            existingCommand.Parameters.AddWithValue("$saleKey", saleKey);

            if (await existingCommand.ExecuteScalarAsync() is string existing)
            {
                transaction.Commit();
                return existing;
            }
        }

        long sequence;

        using (var counterCommand = connection.CreateCommand())
        {
            counterCommand.Transaction = transaction;
            counterCommand.CommandText = """
                INSERT INTO Receipt_Counter (counter_key, last_number)
                VALUES ($counterKey, 1)
                ON CONFLICT(counter_key) DO UPDATE
                    SET last_number = last_number + 1
                RETURNING last_number;
                """;
            counterCommand.Parameters.AddWithValue("$counterKey", counterKey);

            sequence = Convert.ToInt64(await counterCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }

        var receiptNumber = string.Format(CultureInfo.InvariantCulture, format, sequence);

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO Receipt_Number (sale_key, receipt_no, issued_at)
                VALUES ($saleKey, $receiptNo, $issuedAt);
                """;
            insertCommand.Parameters.AddWithValue("$saleKey", saleKey);
            insertCommand.Parameters.AddWithValue("$receiptNo", receiptNumber);
            insertCommand.Parameters.AddWithValue("$issuedAt", issuedAt);

            await insertCommand.ExecuteNonQueryAsync();
        }

        transaction.Commit();

        return receiptNumber;
    }
}
