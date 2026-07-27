using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class RecoverySettingsView : UserControl
{
    public RecoverySettingsView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(RecoverySettingsViewModel viewModel)
    {
        viewModel.NavigateBack += OnNavigateBack;
        DataContext = viewModel;
    }

    private void OnSaveClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is RecoverySettingsViewModel viewModel)
        {
            viewModel.EmailAppPassword = AppPasswordInput.Password;
            viewModel.SaveCommand.Execute(null);
        }
    }

    private void OnNavigateBack()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = new MainShellView { DataContext = App.CurrentShellViewModel };
        }
    }
}