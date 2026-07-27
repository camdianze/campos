using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class AlertsView : UserControl
{
    public AlertsView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(AlertsViewModel viewModel)
    {
        viewModel.NavigateToInventory += OnNavigateToInventory;
        DataContext = viewModel;
    }

    private void OnBackClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = new MainShellView
            {
                DataContext = App.CurrentShellViewModel
            };
        }
    }

    private void OnNavigateToInventory(string productName)
    {
        var inventoryView = new InventoryStatusView();

        if (inventoryView.DataContext is InventoryStatusViewModel viewModel)
        {
            viewModel.SearchTerm = productName;
        }

        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = inventoryView;
        }
    }
}