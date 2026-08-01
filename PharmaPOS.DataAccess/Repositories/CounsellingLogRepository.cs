using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// ICounsellingLogRepository의 SQLite 구현체.
/// </summary>
public class CounsellingLogRepository : ICounsellingLogRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public CounsellingLogRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(CounsellingLogEntry entry)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Counselling_Log
                (log_id, transaction_id, product_id, atc_code, aware_group,
                 printed, skip_reason, locale, source_version, created_at)
            VALUES
                ($logId, $transactionId, $productId, $atcCode, $awareGroup,
                 $printed, $skipReason, $locale, $sourceVersion, $createdAt);
            """;

        command.Parameters.AddWithValue("$logId", entry.LogId);
        command.Parameters.AddWithValue("$transactionId", entry.TransactionId);
        command.Parameters.AddWithValue("$productId", entry.ProductId);
        command.Parameters.AddWithValue("$atcCode", (object?)entry.AtcCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$awareGroup", entry.AwareGroup);
        command.Parameters.AddWithValue("$printed", entry.Printed ? 1 : 0);
        command.Parameters.AddWithValue("$skipReason", (object?)entry.SkipReason ?? DBNull.Value);
        command.Parameters.AddWithValue("$locale", entry.Locale);
        command.Parameters.AddWithValue("$sourceVersion", (object?)entry.SourceVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", entry.CreatedAt);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<CounsellingMetrics> GetMetricsAsync(long fromUtcMillis, long toUtcMillis)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        // 전체 판매 건수는 복약안내 로그가 아니라 판매 거래에서 센다.
        // 항생제가 아닌 상품은 로그를 남기지 않기 때문이다.
        int totalSaleLines;

        using (var totalCommand = connection.CreateCommand())
        {
            totalCommand.CommandText = """
                SELECT COUNT(*) FROM Stock_Transaction
                WHERE transaction_type = $type
                  AND transaction_time >= $from
                  AND transaction_time < $to;
                """;
            totalCommand.Parameters.AddWithValue("$type", TransactionType.StockOut.ToString());
            totalCommand.Parameters.AddWithValue("$from", fromUtcMillis);
            totalCommand.Parameters.AddWithValue("$to", toUtcMillis);

            totalSaleLines = (int)(long)(await totalCommand.ExecuteScalarAsync())!;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT aware_group, printed, COUNT(*)
            FROM Counselling_Log
            WHERE created_at >= $from AND created_at < $to
            GROUP BY aware_group, printed;
            """;
        command.Parameters.AddWithValue("$from", fromUtcMillis);
        command.Parameters.AddWithValue("$to", toUtcMillis);

        var access = 0;
        var watch = 0;
        var reserve = 0;
        var notRecommended = 0;
        var unmatched = 0;
        var printed = 0;
        var skipped = 0;

        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var group = reader.GetString(0);
                var wasPrinted = reader.GetInt32(1) != 0;
                var count = reader.GetInt32(2);

                switch (group)
                {
                    case AwareGroupCodes.Access:
                        access += count;
                        break;
                    case AwareGroupCodes.Watch:
                        watch += count;
                        break;
                    case AwareGroupCodes.Reserve:
                        reserve += count;
                        break;
                    case AwareGroupCodes.NotRecommended:
                        notRecommended += count;
                        break;
                    default:
                        unmatched += count;
                        break;
                }

                // unmatched는 항생제로 확인된 건이 아니므로 출력률 분자/분모에서 뺀다.
                if (group == AwareGroupCodes.Unmatched)
                {
                    continue;
                }

                if (wasPrinted)
                {
                    printed += count;
                }
                else
                {
                    skipped += count;
                }
            }
        }

        return new CounsellingMetrics
        {
            TotalSaleLines = totalSaleLines,
            AntibioticSaleLines = access + watch + reserve + notRecommended,
            AccessCount = access,
            WatchCount = watch,
            ReserveCount = reserve,
            NotRecommendedCount = notRecommended,
            UnmatchedCount = unmatched,
            PrintedCount = printed,
            SkippedCount = skipped
        };
    }
}
