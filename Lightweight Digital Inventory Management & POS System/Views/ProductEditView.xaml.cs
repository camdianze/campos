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
    }

    private void OnConfirmationRequested(string message)
    {
        var result = MessageBox.Show(message, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes && DataContext is ProductEditViewModel viewModel)
        {
            viewModel.ConfirmLowerSellingPrice();
        }
    }

    private void OnNavigateBackToList()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = new ProductListView();
        }
    }
}