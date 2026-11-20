using CamPOS.Application.Counselling;
using CamPOS.Application.Repositories;
using CamPOS.DataAccess.Database;
using CamPOS.Domain.Entities;
using CamPOS.Domain.Enums;

namespace CamPOS.DataAccess.Repositories;

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

        // skip_reason까지 묶는다. 안내가 나가지 않은 이유가 약사의 판단인지 인쇄 실패인지는
        // 할 일이 전혀 다른 두 가지라, 합산된 한 숫자로는 아무 결정도 내릴 수 없다.
        command.CommandText = """
            SELECT aware_group, printed, skip_reason, COUNT(*)
            FROM Counselling_Log
            WHERE created_at >= $from AND created_at < $to
            GROUP BY aware_group, printed, skip_reason;
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
        var pharmacistSkipped = 0;
        var printFailed = 0;

        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var group = reader.GetString(0);
                var wasPrinted = reader.GetInt32(1) != 0;
                var skipReason = reader.IsDBNull(2) ? null : reader.GetString(2);
                var count = reader.GetInt32(3);

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
                    continue;
                }

                skipped += count;

                // 이유가 적혀 있지 않은 행은 어느 쪽으로도 세지 않는다. 둘 중 하나로
                // 밀어 넣으면 없는 근거를 만들어 내는 셈이고, 그래서 SkippedCount가
                // 두 값의 합보다 클 수 있다 — 그 차이가 "이유를 모른다"는 사실이다.
                switch (skipReason)
                {
                    case CounsellingService.SkipReasonPharmacist:
                        pharmacistSkipped += count;
                        break;
                    case CounsellingService.SkipReasonPrintFailed:
                        printFailed += count;
                        break;
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
            SkippedCount = skipped,
            PharmacistSkippedCount = pharmacistSkipped,
            PrintFailedCount = printFailed
        };
    }
}
