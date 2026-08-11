using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class InventoryStatusView : UserControl
{
    public InventoryStatusView()
    {
        InitializeComponent();

        DataContext = BuildViewModel();
    }

    /// <summary>
    /// 입고·조정을 이 화면 안에서 처리하게 되면서 시설/사용자 ID가 필요해졌고,
    /// DI로는 그 두 값을 줄 수 없어 여기서 직접 만든다
    /// (ProductListView.Create와 같은 이유, 같은 방식).
    /// </summary>
    private static InventoryStatusViewModel BuildViewModel()
    {
        var shellViewModel = App.CurrentShellViewModel;

        return new InventoryStatusViewModel(
            App.Services.GetRequiredService<IInventoryRepository>(),
            App.Services.GetRequiredService<IAdjustmentService>(),
            App.Services.GetRequiredService<IStockInService>(),
            shellViewModel?.CurrentUser.FacilityId ?? string.Empty,
            shellViewModel?.CurrentUser.UserId ?? string.Empty);
    }

    /// <summary>
    /// TreeView.SelectedItem은 읽기 전용이라 바인딩할 수 없어, 여기서 ViewModel에 넣어 준다.
    /// 제품 행(ProductInventoryGroup)을 고르면 배치 선택은 null이 되어 삭제 버튼이 꺼진다.
    /// </summary>
    private void OnTreeSelectedItemChanged(
        object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is InventoryStatusViewModel viewModel)
        {
            viewModel.SelectedItem = e.NewValue as InventoryStatusItem;

            // 제품 행도 기억해 둔다. 배치를 고르지 않아도 그 제품으로 입고는 할 수 있다.
            viewModel.SelectedGroup = e.NewValue as ProductInventoryGroup;
        }
    }

    private void OnBackClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = new MainShellView
            {
                DataContext = App.CurrentShellViewModel
            };
        }
    }
}
