using System.Collections.ObjectModel;
using CamPOS.Application.Sync;
using System.Windows;
using CamPOS.Application.Inventory;
using PharmaPOS.Application.Licensing;
using CamPOS.Domain.Entities;
using CamPOS.Domain.Enums;
using CamPOS.Wpf.Services;
using CamPOS.Wpf.ViewModels.Base;

namespace CamPOS.Wpf.Shell;

public class MainShellViewModel : ViewModelBase
{
    private readonly IAlertService _alertService;
    private readonly UiLanguageService _uiLanguage;
    private int _alertCount;

    public User CurrentUser { get; }
    private readonly LicenseStatus _licenseStatus;

    /// <summary>만료가 30일 안으로 다가왔을 때만 상단 바에 자리를 준다.</summary>
    public bool ShowLicenseWarning { get; }

    public string LicenseWarningText { get; } = string.Empty;

    /// <summary>배지에 마우스를 올렸을 때. 정확한 만료일과 연장 방법을 적는다.</summary>
    /// <summary>
    /// 동기화가 멈춘 상태를 상단 바에 띄울지. 자동 전송의 진짜 위험은 조용히
    /// 멈추는 것이고, 알아차릴 사람은 이 PC 앞에 없다.
    /// </summary>
    public bool ShowSyncWarning { get; }

    public string SyncWarningText { get; } = string.Empty;

    public string SyncWarningTooltip { get; } = string.Empty;

    public string LicenseWarningTooltip =>
        $"{_licenseStatus.Summary}\nAdministrators can enter a renewal code "
        + "in Admin Dashboard → License.";

    public string WelcomeMessage { get; }
    public string RoleDescription { get; }
    public bool IsAdministrator { get; }

    public ObservableCollection<AlertItem> RecentAlerts { get; } = new();

    public int AlertCount
    {
        get => _alertCount;
        set => SetProperty(ref _alertCount, value);
    }

    public RelayCommand MyPageCommand { get; }
    public RelayCommand LogoutCommand { get; }

    public event Action? MyPageRequested;
    public event Action? LogoutRequested;

    public MainShellViewModel(
        User loggedInUser, IAlertService alertService, UiLanguageService uiLanguage,
        LicenseStatus licenseStatus,
        SyncHealth syncHealth)
    {
        _alertService = alertService;
        _uiLanguage = uiLanguage;
        _licenseStatus = licenseStatus;

        // 언어가 바뀌면 카드 글자를 다시 읽어 간다.
        // 셸 ViewModel은 로그인마다 하나라 오래 살지만, 그래도 같은 방식으로 맞춘다.
        WeakEventManager<UiLanguageService, EventArgs>.AddHandler(
            _uiLanguage, nameof(UiLanguageService.LanguageChanged), OnLanguageChanged);

        CurrentUser = loggedInUser;
        WelcomeMessage = $"Welcome, {loggedInUser.Username}";

        // 만료가 가까우면 상단 바에 남은 날을 띄운다. 기한이 지나면 앱이 아예 열리지
        // 않으므로, 그 전에 볼 기회가 있어야 방문 일정을 잡을 수 있다.
        // 영구 라이선스이거나 아직 한참 남았으면 자리 자체가 없다.
        // 꺼 둔 것과 못 보내고 있는 것을 다른 문구로 말한다. 꺼 둔 상태를 "실패"로
        // 보고하면 엉뚱한 곳을 찾게 되고, 아무 말도 안 하면 꺼 둔 사실을 잊는다.
        ShowSyncWarning = syncHealth.ShouldWarn;
        SyncWarningText = syncHealth.WarningText;
        SyncWarningTooltip = syncHealth.WarningTooltip;

        ShowLicenseWarning = licenseStatus.ShouldWarn;
        LicenseWarningText = licenseStatus.DaysRemaining switch
        {
            null => string.Empty,
            <= 0 => "License expires today",
            1 => "License expires tomorrow",
            { } days => $"License expires in {days} days"
        };

        RoleDescription = loggedInUser.Role switch
        {
            UserRole.Administrator => "[Placeholder] Administrator Dashboard",
            UserRole.FacilityStaff => "[Placeholder] Main Dashboard / POS Screen",
            _ => "[Placeholder] Unknown Role"
        };

        IsAdministrator = loggedInUser.Role == UserRole.Administrator;

        MyPageCommand = new RelayCommand(_ => MyPageRequested?.Invoke());
        LogoutCommand = new RelayCommand(_ => LogoutRequested?.Invoke());

        _ = LoadAlertsAsync();
    }

    public async Task LoadAlertsAsync()
    {
        try
        {
            var alerts = await _alertService.GetAlertsAsync(
                CurrentUser.FacilityId,
                AlertTypeFilter.All,
                AlertPriorityFilter.All);

            RecentAlerts.Clear();
            foreach (var alert in alerts.Take(5))
                RecentAlerts.Add(alert);

            AlertCount = alerts.Count;
        }
        catch { }
    }

    // ── 화면 언어 ────────────────────────────────────────────────────────────
    // 번역이 없는 키는 영어가 그대로 나온다. 빈 카드보다 영어 카드가 낫다.

    public string ProductsLabel => _uiLanguage.Text("ui.products", "Products");

    public string InventoryLabel => _uiLanguage.Text("ui.inventory", "Inventory");

    public string PosSaleLabel => _uiLanguage.Text("ui.pos_sale", "POS Sale");

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RaiseLanguageLabels();

        // 알림 종류·우선순위는 변환기가 그리는데, 변환기는 값이 바뀔 때만 다시 돈다.
        // 언어만 바뀌면 값은 그대로라 목록을 다시 채워야 새 말로 나온다.
        _ = LoadAlertsAsync();
    }

    private void RaiseLanguageLabels()
    {
        OnPropertyChanged(nameof(ProductsLabel));
        OnPropertyChanged(nameof(InventoryLabel));
        OnPropertyChanged(nameof(PosSaleLabel));
    }
}