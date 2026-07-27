using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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

        var services = new ServiceCollection();
        services.AddPharmaPosServices(dbFilePath);
        Services = services.BuildServiceProvider();

        var databaseInitializer = Services.GetRequiredService<DatabaseInitializer>();
        databaseInitializer.Initialize();

        var initialSetupRepository = Services.GetRequiredService<IInitialSetupRepository>();
        var isSetupComplete = await initialSetupRepository.IsSetupCompleteAsync();

        var mainWindow = new MainWindow();

        mainWindow.Content = isSetupComplete
            ? new LoginView()
            : new InitialSetupView();

        mainWindow.Show();
    }
}