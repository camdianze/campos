using System.Collections.ObjectModel;
using System.Windows;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.Services;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.Shell;

public class MainShellViewModel : ViewModelBase
{
    private readonly IAlertService _alertService;
    private readonly UiLanguageService _uiLanguage;
    private int _alertCount;

    public User CurrentUser { get; }
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
        User loggedInUser, IAlertService alertService, UiLanguageService uiLanguage)
    {
        _alertService = alertService;
        _uiLanguage = uiLanguage;

        // 언어가 바뀌면 카드 글자를 다시 읽어 간다.
        // 셸 ViewModel은 로그인마다 하나라 오래 살지만, 그래도 같은 방식으로 맞춘다.
        WeakEventManager<UiLanguageService, EventArgs>.AddHandler(
            _uiLanguage, nameof(UiLanguageService.LanguageChanged), OnLanguageChanged);

        CurrentUser = loggedInUser;
        WelcomeMessage = $"Welcome, {loggedInUser.Username}";

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