using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IAwareClassificationRepository의 SQLite 구현체.
/// </summary>
public class AwareClassificationRepository : IAwareClassificationRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AwareClassificationRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task ReplaceAllAsync(IReadOnlyList<AwareClassification> classifications)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var dbTransaction = connection.BeginTransaction();

        try
        {
            using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = dbTransaction;
                deleteCommand.CommandText = "DELETE FROM Aware_Classification;";
                await deleteCommand.ExecuteNonQueryAsync();
            }

            // 수백 행을 넣으므로 커맨드를 한 번만 만들고 파라미터 값만 갈아끼운다.
            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = dbTransaction;
            insertCommand.CommandText = """
                INSERT INTO Aware_Classification
                    (aware_id, atc_code, antibiotic_name, normalized_name,
                     aware_group, is_systemic, source_version, updated_at)
                VALUES
                    ($awareId, $atcCode, $antibioticName, $normalizedName,
                     $awareGroup, $isSystemic, $sourceVersion, $updatedAt);
                """;

            var awareId = insertCommand.Parameters.Add("$awareId", SqliteType.Text);
            var atcCode = insertCommand.Parameters.Add("$atcCode", SqliteType.Text);
            var antibioticName = insertCommand.Parameters.Add("$antibioticName", SqliteType.Text);
            var normalizedName = insertCommand.Parameters.Add("$normalizedName", SqliteType.Text);
            var awareGroup = insertCommand.Parameters.Add("$awareGroup", SqliteType.Text);
            var isSystemic = insertCommand.Parameters.Add("$isSystemic", SqliteType.Integer);
            var sourceVersion = insertCommand.Parameters.Add("$sourceVersion", SqliteType.Text);
            var updatedAt = insertCommand.Parameters.Add("$updatedAt", SqliteType.Integer);

            foreach (var item in classifications)
            {
                awareId.Value = item.AwareId;
                atcCode.Value = (object?)item.AtcCode ?? DBNull.Value;
                antibioticName.Value = item.AntibioticName;
                normalizedName.Value = item.NormalizedName;
                awareGroup.Value = AwareGroupCodes.ToCode(item.AwareGroup);
                isSystemic.Value = item.IsSystemic ? 1 : 0;
                sourceVersion.Value = item.SourceVersion;
                updatedAt.Value = item.UpdatedAt;

                await insertCommand.ExecuteNonQueryAsync();
            }

            dbTransaction.Commit();
        }
        catch
        {
            dbTransaction.Rollback();
            throw;
        }
    }

    public async Task<AwareClassification?> FindByAtcCodeAsync(string normalizedAtcCode)
    {
        if (string.IsNullOrWhiteSpace(normalizedAtcCode))
        {
            return null;
        }

        // 시드 파일에 같은 ATC가 두 번 들어가 있어도 조회가 흔들리지 않도록 정렬을 고정한다.
        return await QuerySingleAsync(
            """
            SELECT aware_id, atc_code, antibiotic_name, normalized_name,
                   aware_group, is_systemic, source_version, updated_at
            FROM Aware_Classification
            WHERE atc_code = $value
            ORDER BY antibiotic_name
            LIMIT 1;
            """,
            normalizedAtcCode);
    }

    public async Task<AwareClassification?> FindByNormalizedNameAsync(string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        return await QuerySingleAsync(
            """
            SELECT aware_id, atc_code, antibiotic_name, normalized_name,
                   aware_group, is_systemic, source_version, updated_at
            FROM Aware_Classification
            WHERE normalized_name = $value
            ORDER BY antibiotic_name
            LIMIT 1;
            """,
            normalizedName);
    }

    public async Task<int> CountAsync()
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Aware_Classification;";

        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<AwareClassification?> QuerySingleAsync(string sql, string value)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$value", value);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        var groupText = reader.GetString(4);

        if (!AwareGroupCodes.TryParse(groupText, out var group))
        {
            // 시드 적재 시점에 검증하므로 여기까지 올 일은 없지만,
            // DB를 손으로 고친 경우까지 고려해 조용히 매칭 실패로 처리한다.
            return null;
        }

        return new AwareClassification
        {
            AwareId = reader.GetString(0),
            AtcCode = reader.IsDBNull(1) ? null : reader.GetString(1),
            AntibioticName = reader.GetString(2),
            NormalizedName = reader.GetString(3),
            AwareGroup = group,
            IsSystemic = reader.GetInt32(5) != 0,
            SourceVersion = reader.GetString(6),
            UpdatedAt = reader.GetInt64(7)
        };
    }
}
