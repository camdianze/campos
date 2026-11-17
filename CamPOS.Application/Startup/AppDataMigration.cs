namespace CamPOS.Application.Startup;

/// <summary>
/// 제품 이름이 바뀔 때마다 데이터 폴더도 바뀌었다. 이미 쓰고 있던 PC가 새 버전을
/// 켰을 때, 판매 기록·라이선스·동기화 설정이 그대로 보여야 한다.
///
/// 지금까지의 경로는 셋이다:
/// <code>
///   %APPDATA%\PharmaPOS    pharmapos.db     v1.26 — 지금 약국이 쓰는 것
///   %APPDATA%\CamPOS       campos.db        v1.27~2.0 — 개발·시험 PC
///   %APPDATA%\CamdiansRX   camdiansrx.db    지금
/// </code>
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
/// 때문이다. <c>license.dat</c>도 같다.
/// </summary>
public static class AppDataMigration
{
    /// <summary>옛 폴더에 남기는 안내. 지우지 않고 남기므로, 왜 남아 있는지 적어 둔다.</summary>
    public const string NoticeFileName = "MOVED-TO-CamdiansRX.txt";

    private const string NoticeText =
        "This folder has moved to CamdiansRX.\r\n\r\n"
        + "CamdiansRX now keeps its data in:\r\n"
        + "    %APPDATA%\\CamdiansRX\r\n\r\n"
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
    /// <param name="sources">
    /// 찾아볼 옛 폴더들. 개발 PC에는 <c>PharmaPOS</c>와 <c>CamPOS</c>가 <b>둘 다</b>
    /// 있을 수 있다 — 앞선 이전이 옛 폴더를 지우지 않기 때문이다.
    /// 어느 쪽을 쓸지는 <see cref="PickSource"/>가 정한다.
    /// </param>
    /// <param name="currentFolder">%APPDATA%\CamdiansRX</param>
    /// <param name="currentDatabaseName">camdiansrx.db</param>
    /// <param name="verifyDatabase">
    /// 복사한 데이터베이스를 실제로 열어 보는 검사. 파일 크기만으로는 바이트가
    /// 망가진 것을 못 잡는다. 이 프로젝트는 SQLite를 모르므로 호출자가 넘긴다.
    /// </param>
    public static AppDataMigrationResult Run(
        IReadOnlyList<AppDataSource> sources,
        string currentFolder,
        string currentDatabaseName,
        Func<string, bool> verifyDatabase)
    {
        var currentDatabase = Path.Combine(currentFolder, currentDatabaseName);

        try
        {
            // 새 폴더에 이미 데이터베이스가 있으면 건드리지 않는다. 옛 폴더가 남아
            // 있다고 해서 쓰고 있는 데이터를 덮어쓰는 일은 절대 없어야 한다.
            if (File.Exists(currentDatabase))
            {
                return AppDataMigrationResult.NothingToDo();
            }

            var source = PickSource(sources);

            if (source is null)
            {
                return AppDataMigrationResult.NothingToDo();
            }

            Directory.CreateDirectory(currentFolder);

            var copied = CopyEverything(
                source.Folder, currentFolder, source.DatabaseName, currentDatabaseName);

            if (!File.Exists(currentDatabase) || !verifyDatabase(currentDatabase))
            {
                RollBack(copied);

                return AppDataMigrationResult.Failed(
                    "Your data could not be moved to the new folder, so nothing was changed. "
                    + "The previous version will still open with all of it.");
            }

            WriteNotice(source.Folder);

            return AppDataMigrationResult.Migrated(copied.Count, source.Folder);
        }
        catch (Exception exception)
        {
            return AppDataMigrationResult.Failed(
                "Your data could not be moved to the new folder: " + exception.Message);
        }
    }

    /// <summary>
    /// 옛 폴더가 여럿일 때 어느 것을 가져올지. <b>마지막으로 쓰인 데이터베이스가
    /// 이긴다.</b>
    ///
    /// 보통은 두 번째 이름(CamPOS)이 더 새것이다 — 그 폴더는 그 이름의 앱이 돌았을
    /// 때만 생기기 때문이다. 하지만 시험용으로 새 버전을 한 번 켜 봤다가 다시
    /// v1.26으로 돌아가 장사를 계속한 PC에서는 <b>옛 이름 쪽이 더 새것</b>이고,
    /// 그때 새것을 버리면 그 사이의 판매가 전부 사라진다. 어느 쪽이 "나중 이름"인지
    /// 보다 어느 쪽이 "나중에 쓰였는지"가 답이어야 하는 이유다.
    ///
    /// 시각은 <c>-wal</c>·<c>-shm</c>까지 함께 본다. WAL 모드에서는 거래가 커밋돼도
    /// <c>.db</c>는 한동안 그대로일 수 있어서, <c>.db</c>만 보면 방금 장사한 폴더가
    /// 오래된 것으로 읽힌다.
    ///
    /// 완전히 같으면 목록 순서대로 — 호출자가 앞에 둔 것이 이긴다.
    /// </summary>
    internal static AppDataSource? PickSource(IReadOnlyList<AppDataSource> sources)
    {
        AppDataSource? best = null;
        var bestWrittenAt = DateTime.MinValue;

        foreach (var candidate in sources)
        {
            var database = Path.Combine(candidate.Folder, candidate.DatabaseName);

            if (!File.Exists(database))
            {
                continue;
            }

            var writtenAt = LastWrittenAt(database);

            if (best is null || writtenAt > bestWrittenAt)
            {
                best = candidate;
                bestWrittenAt = writtenAt;
            }
        }

        return best;
    }

    /// <summary>데이터베이스 세 파일 중 가장 최근에 쓰인 시각.</summary>
    private static DateTime LastWrittenAt(string database)
    {
        var latest = DateTime.MinValue;

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try
            {
                var file = new FileInfo(database + suffix);

                if (file.Exists && file.LastWriteTimeUtc > latest)
                {
                    latest = file.LastWriteTimeUtc;
                }
            }
            catch
            {
                // 읽을 수 없는 파일은 없는 것으로 친다. 고를 수만 있으면 된다.
            }
        }

        return latest;
    }

    /// <summary>
    /// 폴더 전체를 베낀다. 데이터베이스 파일만 이름이 바뀌고(<c>-wal</c>·<c>-shm</c> 포함),
    /// 나머지는 이름 그대로 온다.
    ///
    /// 옛 폴더에 남긴 안내문은 가져오지 않는다 — 새 폴더에 "이 폴더는 옮겨졌다"는
    /// 쪽지가 들어 있으면 읽는 사람이 거꾸로 이해한다.
    /// </summary>
    private static List<string> CopyEverything(
        string from, string to, string legacyDatabaseName, string currentDatabaseName)
    {
        var written = new List<string>();

        foreach (var source in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, source);

            if (IsNotice(relative))
            {
                continue;
            }

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
    /// 앞선 이전이 남긴 쪽지인가. 이름이 두 번 바뀌었으므로 옛 이름의 것도 거른다.
    /// </summary>
    private static bool IsNotice(string relative)
    {
        var fileName = Path.GetFileName(relative);

        return fileName.Equals(NoticeFileName, StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("MOVED-TO-CamPOS.txt", StringComparison.OrdinalIgnoreCase);
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

/// <summary>가져올 수 있는 옛 폴더 하나. 폴더 이름과 그 안의 데이터베이스 이름이 함께 간다.</summary>
/// <param name="Folder">%APPDATA%\PharmaPOS 같은 전체 경로.</param>
/// <param name="DatabaseName">그 폴더에서 쓰던 파일 이름(<c>pharmapos.db</c>).</param>
public sealed record AppDataSource(string Folder, string DatabaseName);

/// <summary>이전이 무엇을 했는지. 예외 대신 결과로 말한다.</summary>
public sealed class AppDataMigrationResult
{
    private AppDataMigrationResult(
        bool migrated, bool isSuccess, int fileCount, string sourceFolder, string message)
    {
        DidMigrate = migrated;
        IsSuccess = isSuccess;
        FileCount = fileCount;
        SourceFolder = sourceFolder;
        Message = message;
    }

    /// <summary>실제로 옮겼는가. 옮길 것이 없었던 경우와 구분된다.</summary>
    public bool DidMigrate { get; }

    public bool IsSuccess { get; }

    public int FileCount { get; }

    /// <summary>어느 폴더에서 가져왔는지. 둘 중 하나를 골랐을 때 어느 쪽인지 말해야 한다.</summary>
    public string SourceFolder { get; } = string.Empty;

    public string Message { get; }

    public static AppDataMigrationResult NothingToDo() =>
        new(false, true, 0, string.Empty, string.Empty);

    public static AppDataMigrationResult Migrated(int fileCount, string sourceFolder) =>
        new(true, true, fileCount, sourceFolder,
            $"Your data was copied from {Path.GetFileName(sourceFolder)} "
            + $"to the new CamdiansRX folder ({fileCount:N0} files).");

    public static AppDataMigrationResult Failed(string message) =>
        new(false, false, 0, string.Empty, message);
}
