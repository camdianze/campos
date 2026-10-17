using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Microsoft.Win32;
using PharmaPOS.Application;
using PharmaPOS.Application.Inventory;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 재고 이력 화면의 ViewModel. 입고·조정·판매·환불이 한 목록에 시간순으로 섞여 나온다.
/// </summary>
public class StockHistoryViewModel : ViewModelBase
{
    private readonly IStockHistoryService _stockHistoryService;
    private readonly string _facilityId;

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private string _searchTerm = string.Empty;
    private StockHistoryFilter _selectedFilter = StockHistoryFilter.All;
    private string _message = string.Empty;

    public ObservableCollection<StockHistoryLineItem> Results { get; } = new();

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

    /// <summary>
    /// 기본값은 All이다. 종류별로 갈라 놓으면 한 배치의 중간 줄이 빠져서
    /// Before/After가 어긋나는 지점을 어느 목록에서도 짚을 수 없다.
    /// </summary>
    public StockHistoryFilter SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                OnPropertyChanged(nameof(IsStockInFilter));
                _ = ExecuteSearchAsync();
            }
        }
    }

    /// <summary>입고만 보고 있을 때만 유효기간을 제 컬럼으로 펼친다.</summary>
    public bool IsStockInFilter => SelectedFilter == StockHistoryFilter.StockIn;

    public IReadOnlyList<StockHistoryFilter> AvailableFilters { get; } =
        Enum.GetValues<StockHistoryFilter>();

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? NavigateBack;

    public StockHistoryViewModel(IStockHistoryService stockHistoryService, string facilityId)
    {
        _stockHistoryService = stockHistoryService;
        _facilityId = facilityId;

        SearchCommand = new RelayCommand(async _ => await ExecuteSearchAsync());
        ResetCommand = new RelayCommand(_ => ExecuteReset());
        ExportCommand = new RelayCommand(_ => ExecuteExport());
        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());

        _ = ExecuteSearchAsync();
    }

    public async Task ExecuteSearchAsync()
    {
        Message = string.Empty;

        var result = await _stockHistoryService.SearchAsync(
            _facilityId, DateFrom, DateTo, SearchTerm, SelectedFilter);

        if (!result.IsSuccess)
        {
            Message = result.Message!;
            return;
        }

        Results.Clear();
        foreach (var item in result.Items!)
        {
            Results.Add(item);
        }

        Message = Results.Count == 0 ? "No stock records found." : string.Empty;
    }

    private void ExecuteReset()
    {
        DateFrom = null;
        DateTo = null;
        SearchTerm = string.Empty;
        // 필터 setter가 다시 조회하므로, 이미 All이면 여기서 직접 부른다.
        if (SelectedFilter == StockHistoryFilter.All)
        {
            _ = ExecuteSearchAsync();
        }
        else
        {
            SelectedFilter = StockHistoryFilter.All;
        }
    }

    private void ExecuteExport()
    {
        if (Results.Count == 0)
        {
            Message = "No data to export.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"stock_history_{AppVersion.FileTag}_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("Date,Type,Product,Batch,Quantity,StockBefore,StockAfter,Detail,User");

            foreach (var item in Results)
            {
                var time = DateTimeOffset.FromUnixTimeMilliseconds(item.TransactionTime).ToLocalTime();
                builder.AppendLine(
                    $"{time:yyyy-MM-dd HH:mm},{item.TypeText},{Escape(item.ProductName)}," +
                    $"{Escape(item.BatchNumber)},{item.Quantity}," +
                    // 값이 없는 거래는 빈칸이다. 0으로 채우면 재고가 0이었다는 뜻이 된다.
                    $"{item.StockBefore},{item.StockAfter},{Escape(item.Detail)},{Escape(item.Username)}");
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
            Message = "Export completed successfully.";
        }
        catch (Exception)
        {
            Message = "Export failed. Please try again.";
        }
    }

    /// <summary>조정 사유에는 쉼표가 들어갈 수 있어 컬럼이 밀린다.</summary>
    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
