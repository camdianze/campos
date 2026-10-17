using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class StockHistoryView : UserControl
{
    public StockHistoryView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(StockHistoryViewModel viewModel)
    {
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = viewModel;
        ApplyColumnsFor(viewModel);
    }

    /// <summary>
    /// DataGridColumn은 시각 트리에 없어서 Visibility를 DataContext에 바인딩할 수 없다.
    /// 이 화면의 다른 이동과 마찬가지로 코드 비하인드에서 직접 다룬다.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StockHistoryViewModel.IsStockInFilter)
            && sender is StockHistoryViewModel viewModel)
        {
            ApplyColumnsFor(viewModel);
        }
    }

    /// <summary>
    /// 입고만 보고 있으면 유효기간이 모든 줄에 있으므로 제 컬럼으로 펼치고,
    /// 그 값을 이미 담고 있던 Detail 칸은 감춘다. 섞여 있을 때는 반대다 —
    /// 입고에만 있는 값이라 대부분의 줄이 빈칸이 된다.
    /// </summary>
    private void ApplyColumnsFor(StockHistoryViewModel viewModel)
    {
        var showExpiry = viewModel.IsStockInFilter;
        ExpiryColumn.Visibility = showExpiry ? Visibility.Visible : Visibility.Collapsed;
        DetailColumn.Visibility = showExpiry ? Visibility.Collapsed : Visibility.Visible;
    }
}
