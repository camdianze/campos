using System.Globalization;
using System.IO;
using CamPOS.Application;
using CamPOS.Application.Inventory;
using CamPOS.Application.Repositories;
using CamPOS.Application.Settings;
using CamPOS.Application.Updates;
using Velopack;
using Velopack.Sources;

namespace CamPOS.Wpf.Services;

/// <summary>
/// IUpdateService의 Velopack 구현.
///
/// <b>약국에게 묻지도 알리지도 않는다.</b> 창을 띄우지 않고, 진행 막대도 없고,
/// 실패해도 말하지 않는다. 어느 날 열어 보니 버전이 올라가 있을 뿐이다 — 판매
/// 중에 "업데이트를 설치할까요?"를 묻는 것은 계산대에서 할 질문이 아니다.
///
/// 그 대가로 <see cref="UpdateLog"/>에 전부 적는다. "어제까지 되던 게 안 된다"는
/// 말을 들었을 때 짚을 것이 있어야 한다.
///
/// 어떤 실패도 밖으로 던지지 않는다. 불리는 자리가 앱의 시작과 종료이고,
/// 업데이트는 없어도 되는 기능이지만 판매는 아니다.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService
{
    private readonly IAppSettingRepository _settings;
    private readonly IBackupService _backupService;
    private readonly UpdateLog _log;
    private readonly string _baseUrl;
    private readonly string _backupFolder;

    private UpdateManager? _manager;
    private UpdateInfo? _downloaded;
    private string _lastMessage = string.Empty;

    public VelopackUpdateService(
        IAppSettingRepository settings,
        IBackupService backupService,
        string releasesBaseUrl,
        string appDataFolder)
    {
        _settings = settings;
        _backupService = backupService;
        _baseUrl = releasesBaseUrl.TrimEnd('/');
        _log = new UpdateLog(appDataFolder);

        // 교체 직전 백업은 데이터 폴더 안에 둔다. 사용자가 고른 폴더가 아니라
        // 앱이 아는 자리여야 한다 — 아무도 지켜보지 않는 시점에 만들어지므로.
        _backupFolder = Path.Combine(appDataFolder, "update-backups");
    }

    // ── 켤 때 ────────────────────────────────────────────────────────────

    public async Task CheckAndDownloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var manager = Manager();

            var decision = UpdatePolicy.ShouldCheck(new UpdateContext
            {
                IsAtStartup = true,
                IsUpdateSourceConfigured = _baseUrl.Length > 0,
                IsInstalled = manager?.IsInstalled ?? false,
            });

            if (!decision.ShouldCheck || manager is null)
            {
                _lastMessage = decision.Reason;
                return;
            }

            var available = await manager.CheckForUpdatesAsync().ConfigureAwait(false);

            await RecordCheckedAtAsync().ConfigureAwait(false);
            _log.RecordChecked(available?.TargetFullRelease.Version.ToString());

            if (available is null)
            {
                _lastMessage = "Up to date.";
                return;
            }

            // 받기만 한다. 교체는 꺼질 때다.
            await manager.DownloadUpdatesAsync(available, cancelToken: cancellationToken)
                .ConfigureAwait(false);

            _downloaded = available;
            _log.RecordDownloaded(available.TargetFullRelease.Version.ToString());
            _lastMessage = $"Version {available.TargetFullRelease.Version} is ready for next launch.";
        }
        catch (Exception exception)
        {
            // 인터넷이 없거나 서버가 안 떠 있는 것은 약국에서 흔한 일이다.
            // 조용히 지나가고 다음에 다시 본다.
            _lastMessage = "Could not check for updates.";
            _log.RecordFailed("check", Short(exception));
        }
    }

    // ── 끌 때 ────────────────────────────────────────────────────────────

    public async Task ApplyIfReadyAsync()
    {
        try
        {
            if (_downloaded is null || _manager is null)
            {
                return;
            }

            // 백업이 먼저다. 교체 자체는 데이터베이스를 건드리지 않지만, 새 버전이
            // 스키마를 고치는 경우 그 직전 상태로 돌아갈 방법이 있어야 한다.
            var backedUp = await TryBackupAsync().ConfigureAwait(false);

            var decision = UpdatePolicy.ShouldApply(new UpdateContext
            {
                IsAtShutdown = true,
                IsUpdateSourceConfigured = true,
                IsInstalled = true,
                HasDownloadedUpdate = true,
                BackupSucceeded = backedUp,
            });

            if (!decision.ShouldApply)
            {
                if (decision.IsPostponed)
                {
                    _log.RecordPostponed(decision.Reason);
                }

                return;
            }

            var from = _manager.CurrentVersion?.ToString() ?? "unknown";
            var to = _downloaded.TargetFullRelease.Version.ToString();

            _log.RecordApplied(from, to);

            // 이 프로세스가 끝나기를 기다렸다가 교체한다. 재시작하지 않는다 —
            // 끄는 중인 사람을 다시 붙잡아 둘 이유가 없고, 다음에 켜면 새 버전이다.
            _manager.WaitExitThenApplyUpdates(_downloaded, silent: true, restart: false);
        }
        catch (Exception exception)
        {
            _log.RecordFailed("apply", Short(exception));
        }
    }

    /// <summary>
    /// 교체 직전 백업. 기존 백업 기능을 그대로 쓴다 — 업데이트만의 백업 경로를
    /// 따로 만들면 둘 중 하나가 조용히 뒤처진다.
    /// </summary>
    private async Task<bool> TryBackupAsync()
    {
        try
        {
            Directory.CreateDirectory(_backupFolder);

            var result = await _backupService.CreateDatabaseBackupAsync(_backupFolder)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _log.RecordFailed("backup", result.Message ?? "unknown");
            }

            return result.IsSuccess;
        }
        catch (Exception exception)
        {
            _log.RecordFailed("backup", Short(exception));
            return false;
        }
    }

    // ── 관리자 화면 ──────────────────────────────────────────────────────

    public UpdateStatus GetStatus()
    {
        var manager = Manager();

        return new UpdateStatus
        {
            CurrentVersion = manager?.CurrentVersion?.ToString() ?? AppVersion.Number,
            DownloadedVersion = _downloaded?.TargetFullRelease.Version.ToString(),
            LastChecked = ReadLastCheck(),
            Channel = ReadChannel(),
            IsInstalled = manager?.IsInstalled ?? false,
            LastMessage = _lastMessage,
        };
    }

    public async Task<UpdateStatus> CheckNowAsync()
    {
        await CheckAndDownloadAsync().ConfigureAwait(false);
        return GetStatus();
    }

    // ── 받침대 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 채널이 바뀌면 바라보는 주소도 바뀌므로 매번 다시 만든다. 주소는
    /// <c>{base}/test/</c> 또는 <c>{base}/stable/</c>이고, 공개 읽기 버킷이라
    /// 열쇠가 들어가지 않는다 — 올리는 권한은 이 앱에 없어야 한다.
    /// </summary>
    private UpdateManager? Manager()
    {
        if (_baseUrl.Length == 0)
        {
            return null;
        }

        try
        {
            var channel = ReadChannel();

            // 채널이 두 군데에 나타난다: 폴더 이름(/test/)과 파일 이름(RELEASES-test).
            // 기본적으로 파일 이름 쪽은 <b>패키지를 만들 때 구워진</b> 채널을 따르므로,
            // 설정만 바꾸면 /stable/RELEASES-test 처럼 어긋난 주소를 찾게 된다.
            // ExplicitChannel로 둘을 같은 값에 묶는다 — 그래야 설정 하나로 채널이
            // 바뀌고, test 설치본을 stable로 되돌릴 때 다시 깔 필요가 없다.
            var options = new UpdateOptions { ExplicitChannel = channel };

            _manager ??= new UpdateManager(new SimpleWebSource($"{_baseUrl}/{channel}/"), options);
            return _manager;
        }
        catch
        {
            return null;
        }
    }

    private string ReadChannel()
    {
        try
        {
            return UpdateChannels.Normalize(
                _settings.GetAsync(AppSettingKeys.UpdateChannel).GetAwaiter().GetResult());
        }
        catch
        {
            return UpdateChannels.Default;
        }
    }

    private DateTimeOffset? ReadLastCheck()
    {
        try
        {
            var stored = _settings.GetAsync(AppSettingKeys.UpdateLastCheck).GetAwaiter().GetResult();

            return stored is not null
                && long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms)
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task RecordCheckedAtAsync()
    {
        try
        {
            await _settings.SetAsync(
                AppSettingKeys.UpdateLastCheck,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
                "number").ConfigureAwait(false);
        }
        catch
        {
            // 시각을 못 적은 것으로 확인을 실패로 만들지 않는다.
        }
    }

    /// <summary>예외 문구는 기록에만 들어가므로 한 줄로 줄인다.</summary>
    private static string Short(Exception exception) =>
        exception.Message.Replace("\r", " ").Replace("\n", " ");
}
