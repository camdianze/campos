using System.Windows.Controls;
using CamPOS.Wpf.Shell;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

public partial class CounsellingSettingsView : UserControl
{
    public CounsellingSettingsView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(CounsellingSettingsViewModel viewModel)
    {
        viewModel.NavigateBack += OnNavigateBack;
        DataContext = viewModel;

        // 참조 데이터 상태와 지표는 DB를 읽어야 하므로 화면을 띄운 뒤 채운다.
        _ = viewModel.LoadAsync();
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
