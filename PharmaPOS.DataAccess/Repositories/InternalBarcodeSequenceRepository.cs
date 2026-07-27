using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IInternalBarcodeSequenceRepository의 SQLite 구현체.
/// </summary>
public class InternalBarcodeSequenceRepository : IInternalBarcodeSequenceRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public InternalBarcodeSequenceRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string> GetNextInternalBarcodeAsync()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            long nextNumber;

            using (var updateCommand = connection.CreateCommand())
            {
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = """
                    UPDATE Internal_Barcode_Sequence
                    SET last_number = last_number + 1
                    WHERE id = 1
                    RETURNING last_number;
                    """;

                nextNumber = (long)(await updateCommand.ExecuteScalarAsync())!;
            }

            transaction.Commit();

            // PRD 규격: INT-XXXXXXXX (8자리, 0으로 채움)
            return $"INT-{nextNumber:D8}";
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}