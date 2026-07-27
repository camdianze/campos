using System.Windows.Controls;
using System.Windows.Input;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class PosSaleView : UserControl
{
    public PosSaleView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(PosSaleViewModel viewModel)
    {
        viewModel.SaleCompleted += OnSaleCompleted;
        viewModel.SaleCancelled += OnSaleCancelled;
        DataContext = viewModel;
    }

    private async void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is PosSaleViewModel viewModel)
            await viewModel.ExecuteSearchAsync();
    }

    private void OnSaleCompleted()
    {
        // 판매 완료 후 같은 화면 유지
    }

    private void OnSaleCancelled()
    {
        NavigateBack();
    }

    private void OnBackClick(object sender, System.Windows.RoutedEventArgs e)
    {
        NavigateBack();
    }

    private void NavigateBack()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = new Shell.MainShellView
            {
                DataContext = App.CurrentShellViewModel
            };
    }
}