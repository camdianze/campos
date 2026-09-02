using System.Collections.ObjectModel;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using System.Windows.Media;
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
        _uiLanguage.LanguageChanged += RaiseLanguageLabels;

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

    /// <summary>
    /// 미검수 번역은 붉은 글씨로 나온다. 검수를 마치고 로케일 파일을 approved로
    /// 바꾸면 저절로 보통 색이 된다 — 화면에 따로 손댈 것이 없다.
    /// </summary>
    public Brush LabelBrush =>
        _uiLanguage.TextBrushOverride ?? (Brush)System.Windows.Application.Current.Resources["TextBrush"];

    private void RaiseLanguageLabels()
    {
        OnPropertyChanged(nameof(ProductsLabel));
        OnPropertyChanged(nameof(InventoryLabel));
        OnPropertyChanged(nameof(PosSaleLabel));
        OnPropertyChanged(nameof(LabelBrush));
    }
}