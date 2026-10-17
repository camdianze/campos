using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class InitialSetupView : UserControl
{
    public InitialSetupView()
    {
        InitializeComponent();

        var viewModel = App.Services.GetRequiredService<InitialSetupViewModel>();
        viewModel.SetupCompleted += OnSetupCompleted;

        DataContext = viewModel;
    }

    private void OnCompleteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is InitialSetupViewModel viewModel)
        {
            viewModel.CompleteSetup(
                AdminPasswordInput.Password,
                ConfirmAdminPasswordInput.Password,
                SecurityAnswerInput.Text);
        }
    }

    private void OnSetupCompleted()
    {
        var parentWindow = System.Windows.Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
        {
            mainWindow.Content = new LoginView();
        }
    }
}