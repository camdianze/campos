using System.Windows;
using System.Windows.Controls;
using CamPOS.Wpf.Shell;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

public partial class BackupExportView : UserControl
{
    private BackupExportViewModel? _viewModel;

    public BackupExportView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(BackupExportViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        _ = LoadSyncSettingsAsync();
    }

    // ── 서버 연결 ────────────────────────────────────────────────────────
    // PasswordField의 Password는 DependencyProperty가 아니라 바인딩되지 않는다.
    // 두 컨트롤을 계속 동기화하면 서로의 변경이 맞물려 키 입력을 삼키기 때문에
    // 그렇게 만들어진 컨트롤이고, 그래서 값은 필요한 순간에 한 번씩 읽고 쓴다.

    private async Task LoadSyncSettingsAsync()
    {
        if (_viewModel is null) return;

        var token = await _viewModel.LoadSyncSettingsAsync();

        // 저장된 토큰을 칸에 되돌려 넣는다. 비워 두면 주소만 고치고 Save를 누른
        // 사람이 토큰을 지우게 되고, 그 PC는 다음 전송에서야 조용히 거부된다.
        if (token.Length > 0)
        {
            SyncTokenInput.Password = token;
        }
    }

    private async void OnSaveSyncSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        await _viewModel.SaveSyncSettingsAsync(SyncTokenInput.Password);
    }

    private async void OnTestSyncConnectionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        await _viewModel.TestSyncConnectionAsync(SyncTokenInput.Password);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = new MainShellView
            {
                DataContext = App.CurrentShellViewModel
            };
        }
    }
}
