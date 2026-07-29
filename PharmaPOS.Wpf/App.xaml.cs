using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PharmaPOS.Application.Licensing;
using PharmaPOS.Application.Repositories;
using PharmaPOS.DataAccess.Database;
using Lightweight_Digital_Inventory_Management___POS_System.Composition;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

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

        var services = new ServiceCollection();
        services.AddPharmaPosServices(dbFilePath, licenseFilePath);
        Services = services.BuildServiceProvider();

        var databaseInitializer = Services.GetRequiredService<DatabaseInitializer>();
        databaseInitializer.Initialize();

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
        var isSetupComplete = await initialSetupRepository.IsSetupCompleteAsync();

        return isSetupComplete
            ? new LoginView()
            : new InitialSetupView();
    }
}
