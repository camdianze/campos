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
        if (e.Key != Key.Enter || DataContext is not PosSaleViewModel viewModel)
        {
            return;
        }

        await viewModel.ExecuteSearchAsync();

        // 다음 스캔을 곧바로 받으려면 커서가 여기 있어야 한다. 바코드가 장바구니까지
        // 한 번에 들어가는 경로에서는 중간에 "Open a Box" 같은 창이 떴다 닫힐 수 있고,
        // 그러면 포커스가 검색창으로 돌아오지 않는다.
        SearchBox.Focus();
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