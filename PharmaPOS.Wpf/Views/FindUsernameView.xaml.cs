using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class FindUsernameView : UserControl
{
    public FindUsernameView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(FindUsernameViewModel viewModel)
    {
        viewModel.NavigateBackToLogin += OnNavigateBackToLogin;
        DataContext = viewModel;
    }

    private void OnNavigateBackToLogin()
    {
        var parentWindow = System.Windows.Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
        {
            mainWindow.Content = new LoginView();
        }
    }
}