using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Inventory;

/// <summary>
/// IBackupService의 구현체.
/// Screen SCR-BACKUP-018, 4절 흐름을 그대로 코드로 옮긴 것이다.
/// </summary>
public class BackupService : IBackupService
{
    private readonly IBackupRepository _backupRepository;

    public BackupService(IBackupRepository backupRepository)
    {
        _backupRepository = backupRepository;
    }

    public async Task<BackupResult> CreateDatabaseBackupAsync(string? backupLocation)
    {
        if (string.IsNullOrWhiteSpace(backupLocation))
        {
            return BackupResult.Failure("Please select a backup location.");
        }

        if (!Directory.Exists(backupLocation))
        {
            return BackupResult.Failure("Backup location is not available.");
        }

        var fileName = $"pharmapos_backup_{AppVersion.FileTag}_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var destinationPath = Path.Combine(backupLocation, fileName);

        try
        {
            await _backupRepository.BackupDatabaseAsync(destinationPath);
        }
        catch (UnauthorizedAccessException)
        {
            return BackupResult.Failure("Cannot access the selected location.");
        }
        catch (Exception)
        {
            return BackupResult.Failure("Database backup failed.");
        }

        return BackupResult.Success($"Backup created: {fileName}");
    }

    public async Task<BackupResult> ExportDatasetsAsync(
        string? backupLocation,
        IReadOnlyList<ExportDataset> datasets,
        bool isCsvFormat,
        DateTime? dateFrom = null,
        DateTime? dateTo = null)
    {
        if (string.IsNullOrWhiteSpace(backupLocation))
        {
            return BackupResult.Failure("Please select a folder to export to.");
        }

        if (datasets.Count == 0)
        {
            return BackupResult.Failure("Please select what to export.");
        }

        if (!Directory.Exists(backupLocation))
        {
            return BackupResult.Failure("Export folder is not available.");
        }

        if (dateFrom is not null && dateTo is not null && dateFrom > dateTo)
        {
            return BackupResult.Failure("Start date cannot be later than end date.");
        }

        long? dateFromUtc = dateFrom is not null
            ? new DateTimeOffset(dateFrom.Value.Date).ToUnixTimeMilliseconds()
            : null;

        // 종료일은 "그 날짜 전체"를 포함해야 하므로 다음날 자정 직전까지로 계산한다.
        long? dateToUtc = dateTo is not null
            ? new DateTimeOffset(dateTo.Value.Date.AddDays(1).AddMilliseconds(-1)).ToUnixTimeMilliseconds()
            : null;

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var extension = isCsvFormat ? "csv" : "xlsx";

        try
        {
            foreach (var dataset in datasets)
            {
                // 버전을 파일 이름에 넣는다. 상품·재고 파일은 그대로 다시 가져올 수 있어야 해서
                // 안쪽 첫 줄은 반드시 헤더여야 한다 — 버전 줄을 위에 붙이면 임포트가 깨진다.
                // 기간을 자른 파일은 이름으로 그 사실이 보여야 한다. 안 그러면 6개월치
                // 파일과 전 기간 파일이 폴더에서 구분되지 않고, 나중에 어느 쪽인지 알 길이 없다.
                var period = BuildPeriodTag(dataset, dateFrom, dateTo);

                var fileName =
                    $"{_backupRepository.GetDatasetFileName(dataset)}_{AppVersion.FileTag}{period}_{timestamp}.{extension}";
                var destinationPath = Path.Combine(backupLocation, fileName);

                await _backupRepository.ExportDatasetAsync(
                    dataset, destinationPath, isCsvFormat, dateFromUtc, dateToUtc);
            }
        }
        catch (Exception)
        {
            return BackupResult.Failure(isCsvFormat ? "CSV export failed." : "Excel export failed.");
        }

        return BackupResult.Success($"{datasets.Count} file(s) exported successfully.");
    }

    /// <summary>
    /// 파일 이름에 붙일 기간 꼬리표. 기간을 받지 않는 묶음(상품·재고)에는 붙이지 않는다 —
    /// 그 파일에는 걸리지도 않은 기간이라 이름에 적으면 거짓말이 된다.
    /// </summary>
    private static string BuildPeriodTag(ExportDataset dataset, DateTime? dateFrom, DateTime? dateTo)
    {
        if (!ExportDatasets.SupportsDateRange(dataset) || (dateFrom is null && dateTo is null))
        {
            return string.Empty;
        }

        var from = dateFrom?.ToString("yyyyMMdd") ?? "start";
        var to = dateTo?.ToString("yyyyMMdd") ?? "latest";
        return $"_{from}-{to}";
    }

    public async Task<BackupResult> RestoreDatabaseAsync(string? backupFilePath, string autoBackupFolder)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
        {
            return BackupResult.Failure("Please select a backup file.");
        }

        if (!File.Exists(backupFilePath))
        {
            return BackupResult.Failure("Invalid backup file.");
        }

        var isValid = await _backupRepository.IsValidSqliteFileAsync(backupFilePath);
        if (!isValid)
        {
            return BackupResult.Failure("Invalid backup file.");
        }

        // 복원 처리 원칙(Screen §5.1절): 복원 전 반드시 기존 DB를 자동 백업한다.
        try
        {
            var autoBackupFileName = $"pharmapos_pre_restore_backup_{AppVersion.FileTag}_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var autoBackupPath = Path.Combine(autoBackupFolder, autoBackupFileName);
            await _backupRepository.BackupDatabaseAsync(autoBackupPath);
        }
        catch (Exception)
        {
            return BackupResult.Failure("Database restore failed.");
        }

        try
        {
            await _backupRepository.RestoreDatabaseAsync(backupFilePath);
        }
        catch (IOException)
        {
            return BackupResult.Failure("Database is currently in use. Please try again.");
        }
        catch (Exception)
        {
            return BackupResult.Failure("Database restore failed.");
        }

        return BackupResult.Success();
    }
}