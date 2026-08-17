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
    /// <summary>
    /// 같은 ATC 코드나 같은 성분명이 여러 행에 걸쳐 있을 때 어느 행을 고를지 정하는 순서.
    ///
    /// WHO 목록에는 제형(경구/주사)에 따라 분류가 갈리는 항목이 있다.
    /// 실제 2025년 자료 기준으로 Minocycline(J01AA08)과 Fosfomycin(J01XX01)이
    /// 주사는 RESERVE, 경구는 WATCH다.
    ///
    /// 그래서 "더 강한 안내가 필요한 쪽"을 고른다. 경구 제품을 RESERVE로 표시하는 것은
    /// 과한 경고일 뿐이지만, 주사 제품을 WATCH로 낮춰 표시하면 필요한 경고를 놓친다.
    /// 어느 쪽으로 틀리는 편이 나은지가 분명한 경우다.
    ///
    /// Product_Master.dosage_form이 생겼으므로 기술적으로는 둘을 가릴 수 있다.
    /// 그래도 여기서 쓰지 않는 것은 <b>의도된 선택</b>이다 — 제형은 뒤늦게 추가한 선택 입력이라
    /// 기존 상품 대부분이 아직 비어 있고, 비어 있는 값으로 등급을 가리면 "적지 않았다"가
    /// "경구다"로 읽혀 주사 제품의 경고가 조용히 낮아진다. 지금은 복약안내 판정에
    /// 제형을 일절 쓰지 않는다. 연동은 데이터가 채워진 뒤 따로 결정할 일이다.
    /// </summary>
    private const string GroupPrecedenceOrderBy = """
        ORDER BY CASE aware_group
                     WHEN 'NOT_RECOMMENDED' THEN 0
                     WHEN 'RESERVE'         THEN 1
                     WHEN 'WATCH'           THEN 2
                     WHEN 'ACCESS'          THEN 3
                     ELSE 4
                 END,
                 antibiotic_name
        """;

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

        return await QuerySingleAsync(
            $"""
            SELECT aware_id, atc_code, antibiotic_name, normalized_name,
                   aware_group, is_systemic, source_version, updated_at
            FROM Aware_Classification
            WHERE atc_code = $value
            {GroupPrecedenceOrderBy}
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
            $"""
            SELECT aware_id, atc_code, antibiotic_name, normalized_name,
                   aware_group, is_systemic, source_version, updated_at
            FROM Aware_Classification
            WHERE normalized_name = $value
            {GroupPrecedenceOrderBy}
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
