using System.Windows.Controls;
using CamPOS.Wpf.Shell;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ReportsViewModel은 시설 ID를 생성자로 받으므로 DI가 만들 수 없다.
    /// 다른 화면과 같이 호출부가 만들어 넘긴다.
    /// </summary>
    public void AttachViewModel(ReportsViewModel viewModel)
    {
        viewModel.NavigateBack += OnNavigateBack;
        DataContext = viewModel;
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
