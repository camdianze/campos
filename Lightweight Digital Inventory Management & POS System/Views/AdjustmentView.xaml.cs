using System.Windows.Controls;
using System.Windows.Input;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class AdjustmentView : UserControl
{
    public AdjustmentView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(AdjustmentViewModel viewModel)
    {
        viewModel.NavigateBack += OnNavigateBack;
        DataContext = viewModel;
    }

    private async void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is AdjustmentViewModel viewModel)
        {
            await viewModel.ExecuteSearchAsync();
        }
    }

    private void OnNavigateBack()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = new Shell.MainShellView
            {
                DataContext = App.CurrentShellViewModel
            };
        }
    }
}