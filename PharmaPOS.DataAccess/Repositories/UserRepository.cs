using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.DataAccess.Repositories;

/// <summary>
/// IUserRepository의 SQLite 구현체.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public UserRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT user_id, facility_id, username, password_hash, role, status, created_at,
                   security_question, security_answer_hash, recovery_email, email_provider, email_app_password_encrypted,
                   smtp_host, smtp_port
            FROM Users
            WHERE username = $username;
            """;
        command.Parameters.AddWithValue("$username", username);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapToUser(reader);
    }
    public async Task<User?> GetByRecoveryEmailAsync(string recoveryEmail)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT user_id, facility_id, username, password_hash, role, status, created_at,
                   security_question, security_answer_hash, recovery_email, email_provider, email_app_password_encrypted,
                   smtp_host, smtp_port
            FROM Users
            WHERE recovery_email = $recoveryEmail COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$recoveryEmail", recoveryEmail);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapToUser(reader);
    }
    public async Task UpdatePasswordHashAsync(string userId, string newPasswordHash)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Users
            SET password_hash = $passwordHash
            WHERE user_id = $userId;
            """;
        command.Parameters.AddWithValue("$passwordHash", newPasswordHash);
        command.Parameters.AddWithValue("$userId", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<User>> SearchUsersAsync(string facilityId, string searchTerm, EntityStatus? statusFilter)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        var whereClauses = new List<string> { "facility_id = $facilityId" };
        command.Parameters.AddWithValue("$facilityId", facilityId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            whereClauses.Add("LOWER(username) LIKE LOWER($search)");
            command.Parameters.AddWithValue("$search", $"%{searchTerm}%");
        }

        if (statusFilter is not null)
        {
            whereClauses.Add("status = $status");
            command.Parameters.AddWithValue("$status", statusFilter.Value.ToString());
        }

        command.CommandText = $"""
            SELECT user_id, facility_id, username, password_hash, role, status, created_at,
                   security_question, security_answer_hash, recovery_email, email_provider, email_app_password_encrypted,
                   smtp_host, smtp_port
            FROM Users
            WHERE {string.Join(" AND ", whereClauses)}
            ORDER BY username;
            """;

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<User>();
        while (await reader.ReadAsync())
        {
            results.Add(MapToUser(reader));
        }

        return results;
    }

    public async Task InsertAsync(User user)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (user_id, facility_id, username, password_hash, role, status, created_at)
            VALUES ($userId, $facilityId, $username, $passwordHash, $role, $status, $createdAt);
            """;
        command.Parameters.AddWithValue("$userId", user.UserId);
        command.Parameters.AddWithValue("$facilityId", user.FacilityId);
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$role", user.Role.ToString());
        command.Parameters.AddWithValue("$status", user.Status.ToString());
        command.Parameters.AddWithValue("$createdAt", user.CreatedAt);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateRoleAsync(string userId, UserRole role)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET role = $role WHERE user_id = $userId;";
        command.Parameters.AddWithValue("$role", role.ToString());
        command.Parameters.AddWithValue("$userId", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateStatusAsync(string userId, EntityStatus status)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET status = $status WHERE user_id = $userId;";
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$userId", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateRecoveryInfoAsync(
        string userId, string? securityQuestion, string? securityAnswerHash,
        string? recoveryEmail, EmailProvider? emailProvider, string? emailAppPasswordEncrypted,
        string? smtpHost, int? smtpPort)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Users
            SET security_question = $securityQuestion,
                security_answer_hash = $securityAnswerHash,
                recovery_email = $recoveryEmail,
                email_provider = $emailProvider,
                email_app_password_encrypted = $emailAppPasswordEncrypted,
                smtp_host = $smtpHost,
                smtp_port = $smtpPort
            WHERE user_id = $userId;
            """;
        command.Parameters.AddWithValue("$securityQuestion", (object?)securityQuestion ?? DBNull.Value);
        command.Parameters.AddWithValue("$securityAnswerHash", (object?)securityAnswerHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$recoveryEmail", (object?)recoveryEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("$emailProvider", (object?)emailProvider?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$emailAppPasswordEncrypted", (object?)emailAppPasswordEncrypted ?? DBNull.Value);
        command.Parameters.AddWithValue("$smtpHost", (object?)smtpHost ?? DBNull.Value);
        command.Parameters.AddWithValue("$smtpPort", (object?)smtpPort ?? DBNull.Value);
        command.Parameters.AddWithValue("$userId", userId);

        await command.ExecuteNonQueryAsync();
    }

    private static User MapToUser(SqliteDataReader reader)
    {
        return new User
        {
            UserId = reader.GetString(0),
            FacilityId = reader.GetString(1),
            Username = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            Role = Enum.Parse<UserRole>(reader.GetString(4)),
            Status = Enum.Parse<EntityStatus>(reader.GetString(5)),
            CreatedAt = reader.GetInt64(6),
            SecurityQuestion = reader.IsDBNull(7) ? null : reader.GetString(7),
            SecurityAnswerHash = reader.IsDBNull(8) ? null : reader.GetString(8),
            RecoveryEmail = reader.IsDBNull(9) ? null : reader.GetString(9),
            EmailProvider = reader.IsDBNull(10) ? null : Enum.Parse<EmailProvider>(reader.GetString(10)),
            EmailAppPasswordEncrypted = reader.IsDBNull(11) ? null : reader.GetString(11),
            SmtpHost = reader.IsDBNull(12) ? null : reader.GetString(12),
            SmtpPort = reader.IsDBNull(13) ? null : reader.GetInt32(13)
        };
    }
}