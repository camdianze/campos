using Microsoft.Extensions.DependencyInjection;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Licensing;
using PharmaPOS.Application.PasswordPolicy;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Application.Security;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;
using Lightweight_Digital_Inventory_Management___POS_System.Services;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;
using PharmaPOS.Application.Security;

namespace Lightweight_Digital_Inventory_Management___POS_System.Composition;

/// <summary>
/// 앱 전체의 의존성 등록을 한 곳에 모아둔 확장 메서드.
/// 인터페이스가 어떤 구현체와 연결되는지는 오직 여기서만 결정된다.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="licenseFilePath">활성화 기록 파일(license.dat)의 전체 경로.</param>
    /// <param name="awareSeedPaths">AWaRe 시드 CSV 후보 경로. 앞에 있는 것이 우선한다.</param>
    /// <param name="localeDirectories">복약안내 로케일 JSON 폴더 후보. 앞에 있는 것이 우선한다.</param>
    public static IServiceCollection AddPharmaPosServices(
        this IServiceCollection services,
        string dbFilePath,
        string licenseFilePath,
        IReadOnlyList<string> awareSeedPaths,
        IReadOnlyList<string> localeDirectories)
    {
        // 인프라 (DB 연결)
        services.AddSingleton(_ => new SqliteConnectionFactory(dbFilePath));
        services.AddSingleton<DatabaseInitializer>();

        // 보안 / 정책 (상태 없음 → Singleton으로 재사용해도 안전)
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IPasswordPolicyValidator, PasswordPolicyValidator>();
        services.AddSingleton<IReceiptPrintingService, SimulatedReceiptPrintingService>();
        services.AddSingleton<ICounsellingSheetPrintingService, WpfCounsellingSheetPrintingService>();
        services.AddSingleton<IEmailSendingService, SmtpEmailSendingService>();
        services.AddSingleton<IRecoveryDataProtector, DpapiRecoveryDataProtector>();
        services.AddSingleton<ILicenseActivationStore>(_ => new DpapiLicenseActivationStore(licenseFilePath));
        services.AddSingleton<ILicenseService, LicenseService>();

        // Repository (요청마다 새로 만들어도 비용이 적음 → Transient)
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IFacilityRepository, FacilityRepository>();
        services.AddTransient<IInitialSetupRepository, InitialSetupRepository>();
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<IInternalBarcodeSequenceRepository, InternalBarcodeSequenceRepository>();
        services.AddTransient<IStockInRepository, StockInRepository>();
        services.AddTransient<IInventoryRepository, InventoryRepository>();
        services.AddTransient<IAdjustmentRepository, AdjustmentRepository>();
        services.AddTransient<IAlertRepository, AlertRepository>();
        services.AddTransient<ISaleRepository, SaleRepository>();
        services.AddTransient<IAdminDashboardRepository, AdminDashboardRepository>();
        services.AddTransient<IAppSettingRepository, AppSettingRepository>();
        services.AddTransient<IAwareClassificationRepository, AwareClassificationRepository>();
        services.AddTransient<ISalesHistoryRepository, SalesHistoryRepository>();
        services.AddTransient<IBackupRepository>(sp => new BackupRepository(
     sp.GetRequiredService<SqliteConnectionFactory>(), dbFilePath));
        // Application 서비스
        services.AddTransient<IAuthenticationService, AuthenticationService>();
        services.AddTransient<IChangePasswordService, ChangePasswordService>();
        services.AddTransient<IInitialSetupService, InitialSetupService>();
        services.AddTransient<IProductService, ProductService>();
        services.AddTransient<IInternalBarcodeService, InternalBarcodeService>();
        services.AddTransient<IStockInService, StockInService>();
        services.AddTransient<IAdjustmentService, AdjustmentService>();
        services.AddTransient<IAlertService, AlertService>();
        services.AddTransient<ISaleService, SaleService>();
        services.AddTransient<IAdminDashboardService, AdminDashboardService>();
        services.AddTransient<IUserManagementService, UserManagementService>();
        services.AddTransient<ISalesHistoryService, SalesHistoryService>();
        services.AddTransient<IBackupService, BackupService>();
        services.AddTransient<IRecoverySettingsService, RecoverySettingsService>();
        services.AddTransient<IPasswordRecoveryService, PasswordRecoveryService>();
        services.AddTransient<IAntibioticMatchingService, AntibioticMatchingService>();
        services.AddTransient<ICounsellingLocaleProvider>(_ =>
            new FileCounsellingLocaleProvider(localeDirectories));
        services.AddTransient<IAwareSeedLoader>(sp => new AwareSeedLoader(
            sp.GetRequiredService<IAwareClassificationRepository>(),
            sp.GetRequiredService<IAppSettingRepository>(),
            awareSeedPaths));

        // ViewModel (화면을 열 때마다 새 상태로 시작해야 하므로 Transient)
        services.AddTransient<LicenseActivationViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<InitialSetupViewModel>();
        services.AddTransient<ProductListViewModel>();
        services.AddTransient<InventoryStatusViewModel>();

        return services;
    }
}