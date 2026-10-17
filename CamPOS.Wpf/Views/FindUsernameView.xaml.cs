using System.Windows.Controls;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

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