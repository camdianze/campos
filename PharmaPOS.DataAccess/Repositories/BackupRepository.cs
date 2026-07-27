using System.Text;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IBackupRepository의 SQLite 구현체.
/// </summary>
public class BackupRepository : IBackupRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly string _dbFilePath;

    private static readonly string[] ExportableTables =
    {
        "Facility", "Users", "Product_Master", "Inventory", "Stock_Transaction"
    };

    // 밀리초 단위 Unix epoch 시각이 저장된 컬럼들.
    // Excel/CSV로 내보낼 때 사람이 읽을 수 있는 날짜/시간 문자열로 변환한다.
    private static readonly HashSet<string> TimestampColumns = new()
    {
        "created_at", "updated_at", "transaction_time", "expiry_date"
    };

    public BackupRepository(SqliteConnectionFactory connectionFactory, string dbFilePath)
    {
        _connectionFactory = connectionFactory;
        _dbFilePath = dbFilePath;
    }

    public IReadOnlyList<string> GetExportableTableNames() => ExportableTables;

    public async Task BackupDatabaseAsync(string destinationDbPath)
    {
        using var source = _connectionFactory.CreateOpenConnection();
        using var destination = new SqliteConnection($"Data Source={destinationDbPath}");
        await destination.OpenAsync();

        source.BackupDatabase(destination);
    }

    public async Task ExportTableToCsvAsync(string tableName, string destinationFilePath)
    {
        ValidateTableName(tableName);

        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {tableName};";

        using var reader = await command.ExecuteReaderAsync();

        var builder = new StringBuilder();

        var columnNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        builder.AppendLine(string.Join(",", columnNames));

        while (await reader.ReadAsync())
        {
            var values = Enumerable.Range(0, reader.FieldCount)
                .Select(i => EscapeCsvValue(FormatCellValue(reader, i, columnNames[i])));
            builder.AppendLine(string.Join(",", values));
        }

        await File.WriteAllTextAsync(destinationFilePath, builder.ToString(), Encoding.UTF8);
    }

    public async Task ExportTableToExcelAsync(string tableName, string destinationFilePath)
    {
        ValidateTableName(tableName);

        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {tableName};";

        using var reader = await command.ExecuteReaderAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(tableName);

        var columnNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();

        for (var col = 0; col < columnNames.Count; col++)
        {
            var headerCell = worksheet.Cell(1, col + 1);
            headerCell.Value = columnNames[col];
            headerCell.Style.Font.Bold = true;
        }

        var row = 2;
        while (await reader.ReadAsync())
        {
            for (var col = 0; col < columnNames.Count; col++)
            {
                var cell = worksheet.Cell(row, col + 1);
                if (!reader.IsDBNull(col))
                {
                    // SetValue<string>으로 대입하면 ClosedXML이 텍스트 타입으로 고정하므로,
                    // 숫자처럼 보이는 날짜 문자열도 지수 표기로 바뀌지 않는다.
                    cell.SetValue(FormatCellValue(reader, col, columnNames[col]));
                }
            }
            row++;
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();

        workbook.SaveAs(destinationFilePath);
    }

    /// <summary>
    /// 밀리초 Unix epoch 컬럼은 "yyyy-MM-dd HH:mm:ss" 문자열로, 그 외에는 원래 값 그대로 반환한다.
    /// </summary>
    private static string FormatCellValue(SqliteDataReader reader, int columnIndex, string columnName)
    {
        if (reader.IsDBNull(columnIndex))
        {
            return string.Empty;
        }

        if (TimestampColumns.Contains(columnName))
        {
            var rawValue = reader.GetInt64(columnIndex);
            var localDateTime = DateTimeOffset.FromUnixTimeMilliseconds(rawValue).ToLocalTime();
            return localDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return reader.GetValue(columnIndex).ToString() ?? string.Empty;
    }

    public Task<bool> IsValidSqliteFileAsync(string filePath)
    {
        return Task.Run(() =>
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={filePath};Mode=ReadOnly");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table';";
                var tableCount = (long)command.ExecuteScalar()!;

                return tableCount > 0;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task RestoreDatabaseAsync(string sourceDbPath)
    {
        using (var connection = _connectionFactory.CreateOpenConnection())
        {
            using var checkpointCommand = connection.CreateCommand();
            checkpointCommand.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpointCommand.ExecuteNonQueryAsync();
        }

        // 현재 DB 파일을 사용 중인 모든 유휴 SQLite 연결(연결 풀)을 강제로 정리한다.
        // 이걸 안 하면, 방금 닫은 연결이 내부적으로 파일 핸들을 계속 쥐고 있어서
        // File.Copy가 "파일이 사용 중"이라는 예외를 던질 수 있다.
        SqliteConnection.ClearAllPools();

        File.Copy(sourceDbPath, _dbFilePath, overwrite: true);

        DeleteIfExists(_dbFilePath + "-wal");
        DeleteIfExists(_dbFilePath + "-shm");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ValidateTableName(string tableName)
    {
        if (!ExportableTables.Contains(tableName))
        {
            throw new ArgumentException($"'{tableName}' is not an exportable table.");
        }
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}