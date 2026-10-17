using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using CamPOS.Application.Counselling;
using CamPOS.Application.Startup;
using CamPOS.Application.Updates;
using PharmaPOS.Application.Licensing;
using CamPOS.Application.Repositories;
using CamPOS.DataAccess.Database;
using CamPOS.Wpf.Composition;
using CamPOS.Wpf.Shell;
using CamPOS.Wpf.Views;

using CamPOS.Wpf.Services;

namespace CamPOS.Wpf;

// 기반 클래스를 전체 이름으로 적는다. 이 네임스페이스(CamPOS.Wpf) 안에서 그냥
// Application이라고 쓰면 WPF의 Application이 아니라 네임스페이스 CamPOS.Application이
// 먼저 잡힌다 — 프로젝트 이름이 Application이어서 생기는 충돌이고, 이름을 바꾸는
// 것보다 여기 한 줄을 길게 쓰는 편이 싸다.
public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static MainShellViewModel? CurrentShellViewModel { get; set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataFolder = Path.Combine(roaming, "CamPOS");

        // 내부 이름이 바뀌기 전에 쓰던 폴더. 이미 운영 중인 PC가 새 버전을 켰을 때
        // 판매 기록·라이선스·동기화 설정이 그대로 보여야 하므로, 데이터베이스를
        // 열기 전에 옮긴다. 복사이고, 옛 폴더는 남는다.
        var migration = AppDataMigration.Run(
            legacyFolder: Path.Combine(roaming, "PharmaPOS"),
            currentFolder: appDataFolder,
            legacyDatabaseName: "pharmapos.db",
            currentDatabaseName: "campos.db",
            verifyDatabase: SqliteDatabaseCheck.CanOpen);

        if (!migration.IsSuccess)
        {
            MessageBox.Show(
                migration.Message,
                "CamPOS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        Directory.CreateDirectory(appDataFolder);

        var dbFilePath = Path.Combine(appDataFolder, "campos.db");
        var licenseFilePath = Path.Combine(appDataFolder, "license.dat");

        // AWaRe 참조 데이터와 복약안내 로케일 파일은 두 곳에서 찾는다.
        // %APPDATA% 쪽이 먼저다 — 분류 개정본이나 번역 교체본을 재빌드 없이
        // 그 자리에 놓기만 하면 다음 실행부터 적용되게 하기 위해서다.
        var installFolder = AppContext.BaseDirectory;

        var awareSeedPaths = new[]
        {
            Path.Combine(appDataFolder, "seeds", "aware_2025.csv"),
            Path.Combine(installFolder, "seeds", "aware_2025.csv")
        };

        var localeDirectories = new[]
        {
            Path.Combine(appDataFolder, "locales"),
            Path.Combine(installFolder, "locales")
        };

        // 프린터 없이 용지 내용을 확인할 때 저장되는 기본 위치.
        var sheetOutputFolder = Path.Combine(appDataFolder, "counselling-sheets");

        var services = new ServiceCollection();
        services.AddPharmaPosServices(
            dbFilePath, licenseFilePath, awareSeedPaths, localeDirectories, sheetOutputFolder,
            appDataFolder);
        Services = services.BuildServiceProvider();

        var databaseInitializer = Services.GetRequiredService<DatabaseInitializer>();
        databaseInitializer.Initialize();

        // AWaRe 시드 적재. 실패해도 앱은 그대로 뜬다 —
        // 참조 데이터가 없으면 복약안내가 안 나올 뿐, 판매를 막아서는 안 된다.
        // 적재 상태는 설정 화면에서 확인한다.
        await Services.GetRequiredService<IAwareSeedLoader>().LoadIfChangedAsync();

        var mainWindow = new MainWindow();

        // 시작 화면은 세 단계로 갈린다.
        //   활성화 안 됨 또는 기한 지남 → 라이선스 코드 입력
        //   쓸 수 있음 + 초기 설정 전   → 시설/관리자 설정
        //   쓸 수 있음 + 설정 완료      → 로그인
        //
        // 저장된 코드를 매번 다시 검증한다. 예전에는 license.dat이 열리는지만 봤고,
        // 그래서 기한이 지난 코드로도 앱이 계속 열렸다.
        var licenseService = Services.GetRequiredService<ILicenseService>();
        var licenseStatus = licenseService.GetStatus();

        if (licenseStatus.CanRun)
        {
            mainWindow.Content = await BuildPostActivationScreenAsync();
        }
        else
        {
            var licenseView = new LicenseActivationView(licenseStatus);

            licenseView.ActivationSucceeded += async () =>
            {
                mainWindow.Content = await BuildPostActivationScreenAsync();
            };

            mainWindow.Content = licenseView;
        }

        mainWindow.Show();

        // 업데이트 확인은 화면이 뜬 뒤에 시작한다. 켤 때 한 번뿐이고, 받아만 두고
        // 교체는 꺼질 때 한다. 기다리지 않는다 — 느린 회선에서 시작이 막히면
        // 그것은 업데이트가 판매를 방해한 것이다.
        _ = Services.GetRequiredService<IUpdateService>().CheckAndDownloadAsync();
    }

    /// <summary>
    /// 받아 둔 업데이트를 여기서 적용한다. 쓰는 중이 아니라 꺼지는 중이라,
    /// 판매 도중에 재시작이 걸리는 일이 없다.
    ///
    /// 백업이 먼저이고, 실패하면 미룬다. 받아 둔 것은 그대로 남아 다음에 다시
    /// 시도된다.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Services?.GetService<IUpdateService>()?.ApplyIfReadyAsync()
                .GetAwaiter().GetResult();
        }
        catch
        {
            // 닫는 중이다. 업데이트 때문에 종료가 막히는 일은 없어야 한다.
        }

        base.OnExit(e);
    }

    /// <summary>
    /// 활성화를 통과한 뒤 보여줄 화면. 초기 설정 완료 여부로 갈린다.
    /// </summary>
    private static async Task<UserControl> BuildPostActivationScreenAsync()
    {
        var initialSetupRepository = Services.GetRequiredService<IInitialSetupRepository>();
        // 화면 언어를 먼저 읽어 둔다. 로케일 파일과 지난번 선택을 여기서 한 번만 읽고,
        // 이후 화면들은 이미 준비된 값을 쓴다.
        await Services.GetRequiredService<UiLanguageService>().InitializeAsync();

        var isSetupComplete = await initialSetupRepository.IsSetupCompleteAsync();

        return isSetupComplete
            ? new LoginView()
            : new InitialSetupView();
    }
}
