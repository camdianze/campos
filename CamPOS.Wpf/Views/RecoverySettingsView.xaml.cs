using System.Windows.Controls;
using CamPOS.Wpf.Shell;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

public partial class RecoverySettingsView : UserControl
{
    private Action? _navigateBack;

    public RecoverySettingsView()
    {
        InitializeComponent();
    }

    /// <param name="navigateBack">
    /// 뒤로 갈 화면을 호출부가 정한다. 지금은 My Page에서만 들어오지만,
    /// 넘기지 않으면 예전처럼 메인 셸로 돌아간다.
    /// </param>
    public void AttachViewModel(RecoverySettingsViewModel viewModel, Action? navigateBack = null)
    {
        _navigateBack = navigateBack;
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
        if (_navigateBack is not null)
        {
            _navigateBack();
            return;
        }

        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = new MainShellView { DataContext = App.CurrentShellViewModel };
        }
    }
}