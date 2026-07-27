using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Microsoft.Win32;
using PharmaPOS.Application.Inventory;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 알림 화면(SCR-ALERT-014)의 ViewModel.
/// </summary>
public class AlertsViewModel : ViewModelBase
{
    private readonly IAlertService _alertService;
    private readonly string _facilityId;

    private AlertTypeFilter _selectedTypeFilter = AlertTypeFilter.All;
    private AlertPriorityFilter _selectedPriorityFilter = AlertPriorityFilter.All;
    private AlertItem? _selectedAlert;
    private string _message = string.Empty;

    public ObservableCollection<AlertItem> Alerts { get; } = new();

    public AlertTypeFilter SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
            {
                _ = ReloadAsync();
            }
        }
    }

    public AlertPriorityFilter SelectedPriorityFilter
    {
        get => _selectedPriorityFilter;
        set
        {
            if (SetProperty(ref _selectedPriorityFilter, value))
            {
                _ = ReloadAsync();
            }
        }
    }

    public IReadOnlyList<AlertTypeFilter> AvailableTypeFilters { get; } = Enum.GetValues<AlertTypeFilter>();
    public IReadOnlyList<AlertPriorityFilter> AvailablePriorityFilters { get; } = Enum.GetValues<AlertPriorityFilter>();

    public AlertItem? SelectedAlert
    {
        get => _selectedAlert;
        set => SetProperty(ref _selectedAlert, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand ViewInventoryCommand { get; }
    public RelayCommand ExportCommand { get; }

    /// <summary>View Inventory 클릭 시 발생. 선택한 알림의 상품명을 넘겨준다.</summary>
    public event Action<string>? NavigateToInventory;

    public AlertsViewModel(IAlertService alertService, string facilityId)
    {
        _alertService = alertService;
        _facilityId = facilityId;

        ViewInventoryCommand = new RelayCommand(_ => ExecuteViewInventory());
        ExportCommand = new RelayCommand(_ => ExecuteExport());

        _ = ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        IReadOnlyList<AlertItem> results;

        try
        {
            results = await _alertService.GetAlertsAsync(_facilityId, SelectedTypeFilter, SelectedPriorityFilter);
        }
        catch (Exception)
        {
            Message = "Alerts could not be loaded.";
            return;
        }

        Alerts.Clear();
        foreach (var alert in results)
        {
            Alerts.Add(alert);
        }

        Message = results.Count == 0 ? "No alerts found." : string.Empty;
    }

    private void ExecuteViewInventory()
    {
        if (SelectedAlert is null)
        {
            return;
        }

        NavigateToInventory?.Invoke(SelectedAlert.ProductName);
    }

    private void ExecuteExport()
    {
        if (Alerts.Count == 0)
        {
            Message = "No alerts found.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"inventory_alerts_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("AlertType,Priority,ProductName,Quantity,SafetyStockLevel,BatchNumber,ExpiryDate");

            foreach (var alert in Alerts)
            {
                var expiry = alert.ExpiryDate is not null
                    ? DateTimeOffset.FromUnixTimeMilliseconds(alert.ExpiryDate.Value).ToString("yyyy-MM-dd")
                    : "";

                builder.AppendLine(
                    $"{alert.AlertType},{alert.Priority},{alert.ProductName},{alert.Quantity}," +
                    $"{alert.SafetyStockLevel},{alert.BatchNumber},{expiry}");
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
            Message = "Export completed successfully.";
        }
        catch (Exception)
        {
            Message = "Export failed. Please try again.";
        }
    }
}