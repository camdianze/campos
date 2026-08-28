using System.Windows;
using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class ProductEditView : UserControl
{
    public ProductEditView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(ProductEditViewModel viewModel)
    {
        viewModel.ConfirmationRequested += OnConfirmationRequested;
        viewModel.NavigateBackToList += OnNavigateBackToList;

        DataContext = viewModel;

        // 사진은 화면을 띄운 뒤에 읽는다. 목록에서 들어오는 길이 사진 때문에 느려지면 안 된다.
        _ = viewModel.LoadPhotoAsync();
    }

    private void OnConfirmationRequested(string message)
    {
        var result = AppDialog.Confirm("Confirm", message);

        if (result && DataContext is ProductEditViewModel viewModel)
        {
            viewModel.ConfirmLowerSellingPrice();
        }
    }

    private void OnNavigateBackToList()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = ProductListView.Create();
        }
    }
}