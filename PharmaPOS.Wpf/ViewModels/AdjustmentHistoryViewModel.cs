using System.Collections.ObjectModel;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

public class AdjustmentHistoryRecord
{
    public string ProductName { get; init; } = string.Empty;
    public string BatchNumber { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string AdjustedBy { get; init; } = string.Empty;
    public string AdjustedAt { get; init; } = string.Empty;
}

public class AdjustmentHistoryViewModel : ViewModelBase
{
    private readonly string _facilityId;

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private string _searchTerm = string.Empty;
    private string _message = string.Empty;

    public ObservableCollection<AdjustmentHistoryRecord> Records { get; } = new();

    public DateTime? DateFrom
    {
        get => _dateFrom;
        set => SetProperty(ref _dateFrom, value);
    }

    public DateTime? DateTo
    {
        get => _dateTo;
        set => SetProperty(ref _dateTo, value);
    }

    public string SearchTerm
    {
        get => _searchTerm;
        set => SetProperty(ref _searchTerm, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateBack;

    public AdjustmentHistoryViewModel(string facilityId)
    {
        _facilityId = facilityId;

        SearchCommand = new RelayCommand(async _ => await LoadAsync());
        ResetCommand = new RelayCommand(_ => ExecuteReset());
        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        Message = string.Empty;
        try
        {
            var dbPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PharmaPOS", "pharmapos.db");

            var records = await Task.Run(() => QueryAdjustments(dbPath));

            Records.Clear();
            foreach (var r in records)
                Records.Add(r);

            if (Records.Count == 0)
                Message = "No adjustment records found.";
        }
        catch (Exception ex)
        {
            Message = $"Error: {ex.Message}";
        }
    }

    private List<AdjustmentHistoryRecord> QueryAdjustments(string dbPath)
    {
        var results = new List<AdjustmentHistoryRecord>();

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        var sql = @"
            SELECT
                COALESCE(p.product_name, '') AS ProductName,
                COALESCE(st.batch_number, '') AS BatchNumber,
                st.quantity AS Quantity,
                COALESCE(st.reason, '') AS Reason,
                COALESCE(u.Username, '') AS AdjustedBy,
                st.transaction_time AS AdjustedAt
            FROM Stock_Transaction st
            LEFT JOIN Product_Master p ON st.product_id = p.product_id
            LEFT JOIN Users u          ON st.user_id    = u.user_id
            WHERE st.facility_id       = @FacilityId
              AND st.transaction_type  = 'Adjustment'";

        var conditions = new List<string>();

        if (DateFrom.HasValue)
            conditions.Add("DATE(st.transaction_time) >= DATE(@DateFrom)");
        if (DateTo.HasValue)
            conditions.Add("DATE(st.transaction_time) <= DATE(@DateTo)");
        if (!string.IsNullOrWhiteSpace(SearchTerm))
            conditions.Add("(p.product_name LIKE @Search OR st.batch_number LIKE @Search)");

        if (conditions.Count > 0)
            sql += " AND " + string.Join(" AND ", conditions);

        sql += " ORDER BY st.transaction_time DESC";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@FacilityId", _facilityId);

        if (DateFrom.HasValue)
            cmd.Parameters.AddWithValue("@DateFrom", DateFrom.Value.ToString("yyyy-MM-dd"));
        if (DateTo.HasValue)
            cmd.Parameters.AddWithValue("@DateTo", DateTo.Value.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrWhiteSpace(SearchTerm))
            cmd.Parameters.AddWithValue("@Search", $"%{SearchTerm}%");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new AdjustmentHistoryRecord
            {
                ProductName = reader.IsDBNull(0) ? "" : reader.GetString(0),
                BatchNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Quantity = reader.GetInt32(2),
                Reason = reader.IsDBNull(3) ? "" : reader.GetString(3),
                AdjustedBy = reader.IsDBNull(4) ? "" : reader.GetString(4),
                AdjustedAt = reader.IsDBNull(5) ? "" : reader.GetString(5)
            });
        }

        return results;
    }

    private void ExecuteReset()
    {
        DateFrom = null;
        DateTo = null;
        SearchTerm = string.Empty;
        _ = LoadAsync();
    }
}