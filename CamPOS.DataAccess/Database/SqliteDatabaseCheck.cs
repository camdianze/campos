using Microsoft.Data.Sqlite;

namespace CamPOS.DataAccess.Database;

/// <summary>
/// 데이터베이스 파일이 실제로 열리는지 본다. 폴더를 옮긴 뒤 확인하는 데 쓴다.
///
/// 파일 크기만 맞춰 보면 바이트가 망가진 것을 못 잡는다. 옮긴 뒤에 깨진 것을
/// 알아차리는 시점이 "계산대에서 앱이 안 열릴 때"가 되어서는 안 된다.
/// </summary>
public static class SqliteDatabaseCheck
{
    public static bool CanOpen(string databasePath)
    {
        try
        {
            // 없는 파일을 만들어 버리지 않도록 ReadOnly로 연다. 연결 팩토리를 쓰지
            // 않는 것도 같은 이유다 — 그쪽은 없으면 만들고 PRAGMA까지 건다.
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // 여는 것만으로는 파일 머리만 읽는다. 표 하나를 세어 봐야 안쪽이 읽힌다.
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
            command.ExecuteScalar();

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            // 검사가 파일을 붙들고 있으면 이어지는 롤백이 지우지 못한다.
            SqliteConnection.ClearAllPools();
        }
    }
}
