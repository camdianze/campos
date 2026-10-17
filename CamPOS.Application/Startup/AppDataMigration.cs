namespace CamPOS.Application.Startup;

/// <summary>
/// 내부 이름이 PharmaPOS에서 CamPOS로 바뀌면서 데이터 폴더도 옮겨졌다.
/// 이미 쓰고 있던 PC가 새 버전을 켰을 때, 판매 기록·라이선스·동기화 설정이
/// 그대로 보여야 한다.
///
/// <b>복사이지 이동이 아니다.</b> 옛 폴더는 손대지 않고 남긴다 — 이전이 어딘가에서
/// 잘못되면 되돌아갈 곳이 있어야 하고, 약국의 데이터는 두 벌이 잠시 있는 것보다
/// 한 벌이 사라지는 쪽이 비교할 수 없이 나쁘다.
///
/// <b>폴더 전체를 가져온다.</b> <c>.db</c>만 복사하면 안 된다 — WAL 모드라 방금 끝난
/// 거래가 아직 <c>-wal</c>에만 있을 수 있고, 그것을 빼놓으면 마지막 판매들이 조용히
/// 사라진다. seeds·locales 같은 현장 교체 파일도 같이 온다.
///
/// DPAPI로 암호화된 값(동기화 토큰, 복구 메일 비밀번호)은 폴더가 아니라 <b>Windows
/// 계정</b>에 묶여 있으므로, 파일만 옮겨도 그대로 풀린다. 같은 PC·같은 계정이기
/// 때문이다.
/// </summary>
public static class AppDataMigration
{
    /// <summary>옛 폴더에 남기는 안내. 지우지 않고 남기므로, 왜 남아 있는지 적어 둔다.</summary>
    public const string NoticeFileName = "MOVED-TO-CamPOS.txt";

    private const string NoticeText =
        "This folder has moved to CamPOS.\r\n\r\n"
        + "CamPOS now keeps its data in:\r\n"
        + "    %APPDATA%\\CamPOS\r\n\r\n"
        + "Everything here was copied there, and nothing was deleted. The app is no\r\n"
        + "longer reading this folder.\r\n\r\n"
        + "Once you have checked that the app opens with your products, sales and\r\n"
        + "licence in place, this folder can be deleted.\r\n";

    /// <summary>
    /// 옛 폴더의 데이터를 새 폴더로 옮긴다. 옮길 것이 없거나 이미 옮겼으면
    /// 아무것도 하지 않는다.
    ///
    /// 예외를 밖으로 던지지 않는다 — 여기서 실패해도 앱은 떠야 하고, 그때는
    /// 옛 폴더가 온전하므로 옛 버전으로 돌아가면 된다.
    /// </summary>
    /// <param name="legacyFolder">%APPDATA%\PharmaPOS</param>
    /// <param name="currentFolder">%APPDATA%\CamPOS</param>
    /// <param name="legacyDatabaseName">pharmapos.db</param>
    /// <param name="currentDatabaseName">campos.db</param>
    /// <param name="verifyDatabase">
    /// 복사한 데이터베이스를 실제로 열어 보는 검사. 파일 크기만으로는 바이트가
    /// 망가진 것을 못 잡는다. 이 프로젝트는 SQLite를 모르므로 호출자가 넘긴다.
    /// </param>
    public static AppDataMigrationResult Run(
        string legacyFolder,
        string currentFolder,
        string legacyDatabaseName,
        string currentDatabaseName,
        Func<string, bool> verifyDatabase)
    {
        var legacyDatabase = Path.Combine(legacyFolder, legacyDatabaseName);
        var currentDatabase = Path.Combine(currentFolder, currentDatabaseName);

        try
        {
            // 새 폴더에 이미 데이터베이스가 있으면 건드리지 않는다. 옛 폴더가 남아
            // 있다고 해서 쓰고 있는 데이터를 덮어쓰는 일은 절대 없어야 한다.
            if (File.Exists(currentDatabase))
            {
                return AppDataMigrationResult.NothingToDo();
            }

            if (!File.Exists(legacyDatabase))
            {
                return AppDataMigrationResult.NothingToDo();
            }

            Directory.CreateDirectory(currentFolder);

            var copied = CopyEverything(legacyFolder, currentFolder, legacyDatabaseName, currentDatabaseName);

            if (!File.Exists(currentDatabase) || !verifyDatabase(currentDatabase))
            {
                RollBack(copied);

                return AppDataMigrationResult.Failed(
                    "Your data could not be moved to the new folder, so nothing was changed. "
                    + "The previous version of CamPOS will still open with all of it.");
            }

            WriteNotice(legacyFolder);

            return AppDataMigrationResult.Migrated(copied.Count);
        }
        catch (Exception exception)
        {
            return AppDataMigrationResult.Failed(
                "Your data could not be moved to the new folder: " + exception.Message);
        }
    }

    /// <summary>
    /// 폴더 전체를 베낀다. 데이터베이스 파일만 이름이 바뀌고(<c>-wal</c>·<c>-shm</c> 포함),
    /// 나머지는 이름 그대로 온다.
    /// </summary>
    private static List<string> CopyEverything(
        string from, string to, string legacyDatabaseName, string currentDatabaseName)
    {
        var written = new List<string>();

        foreach (var source in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, source);
            var destination = Path.Combine(to, RenameDatabase(relative, legacyDatabaseName, currentDatabaseName));

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);

            // 크기가 다르면 그 자리에서 멈춘다. 반쯤 복사된 데이터베이스를 열어 보는
            // 것보다 먼저 알아차리는 편이 낫다.
            if (new FileInfo(source).Length != new FileInfo(destination).Length)
            {
                written.Add(destination);
                throw new IOException($"{relative} was not copied completely.");
            }

            written.Add(destination);
        }

        return written;
    }

    /// <summary>
    /// <c>pharmapos.db</c>, <c>pharmapos.db-wal</c>, <c>pharmapos.db-shm</c>의 이름을 바꾼다.
    /// 세 파일은 SQLite가 이름으로 짝을 찾으므로 함께 바뀌어야 한다 — 하나만 바꾸면
    /// WAL이 고아가 되고, 거기 들어 있던 거래가 사라진다.
    /// </summary>
    private static string RenameDatabase(string relative, string legacyName, string currentName)
    {
        var fileName = Path.GetFileName(relative);

        if (!fileName.StartsWith(legacyName, StringComparison.OrdinalIgnoreCase))
        {
            return relative;
        }

        var suffix = fileName[legacyName.Length..];
        var directory = Path.GetDirectoryName(relative);

        return directory is null or ""
            ? currentName + suffix
            : Path.Combine(directory, currentName + suffix);
    }

    /// <summary>실패하면 새 폴더에 쓴 것만 지운다. 옛 폴더는 처음부터 읽기만 했다.</summary>
    private static void RollBack(IEnumerable<string> written)
    {
        foreach (var path in written)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // 치우다 실패해도 할 말이 없다. 중요한 것은 옛 폴더가 온전하다는 점이다.
            }
        }
    }

    private static void WriteNotice(string legacyFolder)
    {
        try
        {
            File.WriteAllText(Path.Combine(legacyFolder, NoticeFileName), NoticeText);
        }
        catch
        {
            // 안내문을 못 남긴 것으로 이전을 실패로 만들지 않는다. 데이터는 이미 옮겨졌다.
        }
    }
}

/// <summary>이전이 무엇을 했는지. 예외 대신 결과로 말한다.</summary>
public sealed class AppDataMigrationResult
{
    private AppDataMigrationResult(bool migrated, bool isSuccess, int fileCount, string message)
    {
        DidMigrate = migrated;
        IsSuccess = isSuccess;
        FileCount = fileCount;
        Message = message;
    }

    /// <summary>실제로 옮겼는가. 옮길 것이 없었던 경우와 구분된다.</summary>
    public bool DidMigrate { get; }

    public bool IsSuccess { get; }

    public int FileCount { get; }

    public string Message { get; }

    public static AppDataMigrationResult NothingToDo() => new(false, true, 0, string.Empty);

    public static AppDataMigrationResult Migrated(int fileCount) =>
        new(true, true, fileCount, $"Your data was moved to the new CamPOS folder ({fileCount:N0} files).");

    public static AppDataMigrationResult Failed(string message) => new(false, false, 0, message);
}
