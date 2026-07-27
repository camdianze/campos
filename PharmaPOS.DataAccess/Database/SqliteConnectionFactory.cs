using Microsoft.Data.Sqlite;

namespace PharmaPOS.DataAccess.Database;

/// <summary>
/// SQLite 데이터베이스 연결을 생성하는 팩토리 클래스.
/// DB 파일 경로와 연결 옵션(WAL 모드 등)을 한 곳에서 관리한다.
/// </summary>
public class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databaseFilePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        _connectionString = builder.ToString();
    }

    /// <summary>
    /// 새 SQLite 연결을 열어서 반환한다.
    /// 반환된 연결은 사용 후 반드시 Dispose(using문)해야 한다.
    /// </summary>
    public SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // WAL 모드: 여러 화면(읽기)과 쓰기 작업이 동시에 일어나도
        // DB 파일이 손상되지 않도록 하는 설정. PRD의 로컬 데이터 무결성 요구사항(F-10)에 대응한다.
        using var walCommand = connection.CreateCommand();
        walCommand.CommandText = "PRAGMA journal_mode = WAL;";
        walCommand.ExecuteNonQuery();

        // 외래 키 제약(Foreign Key) 강제 적용. SQLite는 기본적으로 꺼져 있어서 켜줘야 한다.
        using var fkCommand = connection.CreateCommand();
        fkCommand.CommandText = "PRAGMA foreign_keys = ON;";
        fkCommand.ExecuteNonQuery();

        return connection;
    }
}