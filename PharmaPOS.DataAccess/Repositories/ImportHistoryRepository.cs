using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IImportHistoryRepository의 SQLite 구현체.
/// </summary>
public class ImportHistoryRepository : IImportHistoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ImportHistoryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ExistsAsync(ImportType importType, string fileHash)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM Import_History
            WHERE import_type = $importType AND file_hash = $fileHash;
            """;
        command.Parameters.AddWithValue("$importType", importType.ToString());
        command.Parameters.AddWithValue("$fileHash", fileHash);

        var count = await command.ExecuteScalarAsync();

        return Convert.ToInt64(count) > 0;
    }

    public async Task AddAsync(ImportHistoryEntry entry)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Import_History
                (import_id, facility_id, import_type, file_hash, file_name,
                 row_count, success_count, failure_count, imported_at)
            VALUES
                ($importId, $facilityId, $importType, $fileHash, $fileName,
                 $rowCount, $successCount, $failureCount, $importedAt);
            """;
        command.Parameters.AddWithValue("$importId", entry.ImportId);
        command.Parameters.AddWithValue("$facilityId", entry.FacilityId);
        command.Parameters.AddWithValue("$importType", entry.ImportType.ToString());
        command.Parameters.AddWithValue("$fileHash", entry.FileHash);
        command.Parameters.AddWithValue("$fileName", entry.FileName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$rowCount", entry.RowCount);
        command.Parameters.AddWithValue("$successCount", entry.SuccessCount);
        command.Parameters.AddWithValue("$failureCount", entry.FailureCount);
        command.Parameters.AddWithValue("$importedAt", entry.ImportedAt);

        await command.ExecuteNonQueryAsync();
    }
}
