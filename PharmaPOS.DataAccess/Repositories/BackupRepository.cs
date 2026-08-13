using System.Text;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;

// 엔티티 이름(Inventory)이 Application의 네임스페이스와 같아 그냥 쓰면 네임스페이스로 읽힌다.
using InventoryEntity = PharmaPOS.Domain.Entities.Inventory;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IBackupRepository의 SQLite 구현체.
///
/// 내보내기는 테이블을 그대로 쏟지 않고 묶음(ExportDataset)별로 질의를 따로 둔다.
/// 상품 ID만 적힌 표는 열어 봐야 알아볼 수 없고, Users처럼 내보내면 안 되는 표도 있기 때문이다.
/// 상품 묶음의 헤더는 임포트가 읽는 이름과 같게 맞춰 두었다 — 내보내 고친 뒤 그대로 다시 넣을 수 있다.
/// </summary>
public class BackupRepository : IBackupRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly string _dbFilePath;

    // 밀리초 단위 Unix epoch 시각이 저장된 컬럼들.
    // Excel/CSV로 내보낼 때 사람이 읽을 수 있는 날짜/시간 문자열로 변환한다.
    private static readonly HashSet<string> TimestampColumns = new()
    {
        "created_at", "updated_at", "transaction_time"
    };

    /// <summary>유효기간 칸. 0(모름)은 날짜가 아니라 임포트와 같은 표기(N)로 내보낸다.</summary>
    private const string ExpiryColumn = "expiry_date";

    public BackupRepository(SqliteConnectionFactory connectionFactory, string dbFilePath)
    {
        _connectionFactory = connectionFactory;
        _dbFilePath = dbFilePath;
    }

    public string GetDatasetFileName(ExportDataset dataset) => dataset switch
    {
        ExportDataset.Products => "products",
        ExportDataset.Inventory => "inventory",
        ExportDataset.SalesHistory => "sales_history",
        _ => throw new ArgumentOutOfRangeException(nameof(dataset))
    };

    /// <summary>
    /// 묶음별 질의. 컬럼 별칭이 그대로 파일의 헤더가 되므로,
    /// 상품 묶음은 임포트가 읽는 이름(product_name, cost_price …)에 맞춰 둔다.
    /// </summary>
    private static string GetQuery(ExportDataset dataset) => dataset switch
    {
        ExportDataset.Products => """
            SELECT product_name, unit, barcode, internal_barcode,
                   generic_name, strength, atc_code, is_combination,
                   manufacturer, country_of_origin,
                   cost_price, selling_price, safety_stock_level,
                   units_per_box, unit_selling_price, category, status, created_at
            FROM Product_Master
            ORDER BY product_name;
            """,

        ExportDataset.Inventory => """
            SELECT p.product_name, i.batch_number, i.expiry_date,
                   i.current_quantity AS quantity,
                   i.box_quantity, i.unit_quantity, p.units_per_box, i.updated_at
            FROM Inventory i
            JOIN Product_Master p ON p.product_id = i.product_id
            ORDER BY p.product_name, i.expiry_date;
            """,

        // 판매와 환불을 함께 내보낸다. 환불 행은 수량·금액이 음수라 그대로 더하면 순매출이 된다.
        ExportDataset.SalesHistory => """
            SELECT st.transaction_time,
                   CASE st.transaction_type WHEN 'StockOut' THEN 'Sale' ELSE 'Refund' END AS type,
                   COALESCE(p.product_name, st.product_id) AS product_name,
                   st.batch_number,
                   st.quantity,
                   st.selling_price_at_transaction AS unit_price,
                   st.total_amount,
                   st.payment_method,
                   COALESCE(u.username, st.user_id) AS sold_by,
                   COALESCE(st.reason, '') AS reason
            FROM Stock_Transaction st
            LEFT JOIN Product_Master p ON p.product_id = st.product_id
            LEFT JOIN Users u ON u.user_id = st.user_id
            WHERE st.transaction_type IN ('StockOut', 'Refund')
            ORDER BY st.transaction_time DESC;
            """,

        _ => throw new ArgumentOutOfRangeException(nameof(dataset))
    };

    public async Task BackupDatabaseAsync(string destinationDbPath)
    {
        using var source = _connectionFactory.CreateOpenConnection();
        using var destination = new SqliteConnection($"Data Source={destinationDbPath}");
        await destination.OpenAsync();

        source.BackupDatabase(destination);
    }

    public async Task ExportDatasetAsync(ExportDataset dataset, string destinationFilePath, bool isCsvFormat)
    {
        if (isCsvFormat)
        {
            await ExportToCsvAsync(dataset, destinationFilePath);
        }
        else
        {
            await ExportToExcelAsync(dataset, destinationFilePath);
        }
    }

    private async Task ExportToCsvAsync(ExportDataset dataset, string destinationFilePath)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = GetQuery(dataset);

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

        // 엑셀이 UTF-8 CSV를 열 때 BOM이 없으면 한글/크메르어가 깨진다.
        await File.WriteAllTextAsync(destinationFilePath, builder.ToString(), new UTF8Encoding(true));
    }

    private async Task ExportToExcelAsync(ExportDataset dataset, string destinationFilePath)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = GetQuery(dataset);

        using var reader = await command.ExecuteReaderAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(GetDatasetFileName(dataset));

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
    /// 유효기간 0은 "모름"이므로 1970-01-01이 아니라 임포트와 같은 표기(N)로 내보낸다.
    /// </summary>
    private static string FormatCellValue(SqliteDataReader reader, int columnIndex, string columnName)
    {
        if (reader.IsDBNull(columnIndex))
        {
            return string.Empty;
        }

        if (columnName == ExpiryColumn)
        {
            var rawExpiry = reader.GetInt64(columnIndex);

            return rawExpiry == InventoryEntity.NoExpiryDate
                ? "N"
                : DateTimeOffset.FromUnixTimeMilliseconds(rawExpiry).ToLocalTime().ToString("yyyy-MM-dd");
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

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
