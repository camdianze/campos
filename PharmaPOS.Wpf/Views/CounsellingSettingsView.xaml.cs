using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

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
