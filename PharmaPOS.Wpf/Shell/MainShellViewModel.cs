using System.Collections.ObjectModel;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.Shell;

public class MainShellViewModel : ViewModelBase
{
    private readonly IAlertService _alertService;
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

    public MainShellViewModel(User loggedInUser, IAlertService alertService)
    {
        _alertService = alertService;

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
}