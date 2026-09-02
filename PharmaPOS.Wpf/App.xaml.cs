using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Licensing;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using Lightweight_Digital_Inventory_Management___POS_System.Composition;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

using Lightweight_Digital_Inventory_Management___POS_System.Services;

namespace Lightweight_Digital_Inventory_Management___POS_System;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static MainShellViewModel? CurrentShellViewModel { get; set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PharmaPOS");

        Directory.CreateDirectory(appDataFolder);

        var dbFilePath = Path.Combine(appDataFolder, "pharmapos.db");
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
            dbFilePath, licenseFilePath, awareSeedPaths, localeDirectories, sheetOutputFolder);
        Services = services.BuildServiceProvider();

        var databaseInitializer = Services.GetRequiredService<DatabaseInitializer>();
        databaseInitializer.Initialize();

        // AWaRe 시드 적재. 실패해도 앱은 그대로 뜬다 —
        // 참조 데이터가 없으면 복약안내가 안 나올 뿐, 판매를 막아서는 안 된다.
        // 적재 상태는 설정 화면에서 확인한다.
        await Services.GetRequiredService<IAwareSeedLoader>().LoadIfChangedAsync();

        var mainWindow = new MainWindow();

        // 시작 화면은 세 단계로 갈린다.
        //   활성화 안 됨  → 라이선스 코드 입력
        //   활성화 됨 + 초기 설정 전 → 시설/관리자 설정
        //   활성화 됨 + 설정 완료    → 로그인
        var licenseService = Services.GetRequiredService<ILicenseService>();

        if (licenseService.IsActivated())
        {
            mainWindow.Content = await BuildPostActivationScreenAsync();
        }
        else
        {
            var licenseView = new LicenseActivationView();

            licenseView.ActivationSucceeded += async () =>
            {
                mainWindow.Content = await BuildPostActivationScreenAsync();
            };

            mainWindow.Content = licenseView;
        }

        mainWindow.Show();
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
