using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IFacilityRepository의 SQLite 구현체.
/// </summary>
public class FacilityRepository : IFacilityRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public FacilityRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Facility?> GetByIdAsync(string facilityId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT facility_id, facility_name, country, district, facility_type, status
            FROM Facility
            WHERE facility_id = $facilityId;
            """;
        command.Parameters.AddWithValue("$facilityId", facilityId);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapToFacility(reader);
    }

    /// <summary>
    /// SQLite 조회 결과 한 행을 Facility 엔티티로 변환한다.
    /// </summary>
    private static Facility MapToFacility(SqliteDataReader reader)
    {
        return new Facility
        {
            FacilityId = reader.GetString(0),
            FacilityName = reader.GetString(1),
            Country = reader.GetString(2),
            District = reader.GetString(3),
            FacilityType = Enum.Parse<FacilityType>(reader.GetString(4)),
            Status = Enum.Parse<EntityStatus>(reader.GetString(5))
        };
    }
}