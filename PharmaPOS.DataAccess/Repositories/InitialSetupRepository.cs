using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IInitialSetupRepository의 SQLite 구현체.
/// </summary>
public class InitialSetupRepository : IInitialSetupRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public InitialSetupRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> IsSetupCompleteAsync()
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var facilityCommand = connection.CreateCommand();
        facilityCommand.CommandText = "SELECT COUNT(*) FROM Facility;";
        var facilityCount = (long)(await facilityCommand.ExecuteScalarAsync())!;

        using var adminCommand = connection.CreateCommand();
        adminCommand.CommandText = "SELECT COUNT(*) FROM Users WHERE role = 'Administrator';";
        var adminCount = (long)(await adminCommand.ExecuteScalarAsync())!;

        return facilityCount > 0 && adminCount > 0;
    }

    public async Task SaveFacilityAndAdminAsync(Facility facility, User adminUser)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            using (var insertFacility = connection.CreateCommand())
            {
                insertFacility.Transaction = transaction;
                insertFacility.CommandText = """
                    INSERT INTO Facility (facility_id, facility_name, country, district, facility_type, status)
                    VALUES ($facilityId, $facilityName, $country, $district, $facilityType, $status);
                    """;
                insertFacility.Parameters.AddWithValue("$facilityId", facility.FacilityId);
                insertFacility.Parameters.AddWithValue("$facilityName", facility.FacilityName);
                insertFacility.Parameters.AddWithValue("$country", facility.Country);
                insertFacility.Parameters.AddWithValue("$district", facility.District);
                insertFacility.Parameters.AddWithValue("$facilityType", facility.FacilityType.ToString());
                insertFacility.Parameters.AddWithValue("$status", facility.Status.ToString());
                await insertFacility.ExecuteNonQueryAsync();
            }

            using (var insertUser = connection.CreateCommand())
            {
                insertUser.Transaction = transaction;
                insertUser.CommandText = """
                    INSERT INTO Users
                        (user_id, facility_id, username, password_hash, role, status, created_at,
                         security_question, security_answer_hash)
                    VALUES
                        ($userId, $facilityId, $username, $passwordHash, $role, $status, $createdAt,
                         $securityQuestion, $securityAnswerHash);
                    """;
                insertUser.Parameters.AddWithValue("$userId", adminUser.UserId);
                insertUser.Parameters.AddWithValue("$facilityId", adminUser.FacilityId);
                insertUser.Parameters.AddWithValue("$username", adminUser.Username);
                insertUser.Parameters.AddWithValue("$passwordHash", adminUser.PasswordHash);
                insertUser.Parameters.AddWithValue("$role", adminUser.Role.ToString());
                insertUser.Parameters.AddWithValue("$status", adminUser.Status.ToString());
                insertUser.Parameters.AddWithValue("$createdAt", adminUser.CreatedAt);
                insertUser.Parameters.AddWithValue("$securityQuestion", (object?)adminUser.SecurityQuestion ?? DBNull.Value);
                insertUser.Parameters.AddWithValue("$securityAnswerHash", (object?)adminUser.SecurityAnswerHash ?? DBNull.Value);
                await insertUser.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}