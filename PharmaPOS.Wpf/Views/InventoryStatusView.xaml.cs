using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Repositories;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

using Lightweight_Digital_Inventory_Management___POS_System.Services;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class InventoryStatusView : UserControl
{
    public InventoryStatusView()
    {
        InitializeComponent();

        var viewModel = BuildViewModel();
        viewModel.NavigateToProductDetails += OnNavigateToProductDetails;
        viewModel.NavigateToPosSale += OnNavigateToPosSale;

        DataContext = viewModel;
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

    /// <summary>
    /// 마지막 우클릭이 실제 행 위에서 일어났는지.
    /// 빈 곳을 우클릭해도 이전 선택이 그대로 남아 있어, 선택 여부만으로는
    /// "행을 눌렀는지"를 알 수 없다.
    /// </summary>
    private bool _rightClickHitRow;

    /// <summary>
    /// 우클릭한 행을 선택 상태로 만든다.
    /// TreeView는 우클릭으로 선택이 바뀌지 않아, 그냥 두면 메뉴가 엉뚱한 행에 작용한다.
    /// </summary>
    private void OnTreeRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindTreeViewItem(e.OriginalSource as DependencyObject);

        _rightClickHitRow = item is not null;

        if (item is null)
        {
            return;
        }

        item.IsSelected = true;
        item.Focus();
    }

    /// <summary>빈 곳에서 우클릭했으면 메뉴를 띄우지 않는다.</summary>
    private void OnTreeContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!_rightClickHitRow || DataContext is not InventoryStatusViewModel { HasSelection: true })
        {
            e.Handled = true;
        }
    }

    /// <summary>누른 지점에서 위로 올라가며 행(TreeViewItem)을 찾는다. 없으면 빈 곳이다.</summary>
    private static TreeViewItem? FindTreeViewItem(DependencyObject? source)
    {
        while (source is not null and not TreeViewItem)
        {
            // 행 안의 요소가 Run·Path처럼 시각 트리에 없는 경우가 있어 논리 부모도 함께 본다.
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return source as TreeViewItem;
    }

    /// <summary>
    /// 고른 상품이 이미 선택된 채로 상품 목록 화면을 연다.
    /// ← Back으로 이 화면에 돌아올 수 있도록 진입 출처를 함께 넘긴다.
    /// </summary>
    private void OnNavigateToProductDetails(string productId)
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;

        if (parentWindow is null)
        {
            return;
        }

        parentWindow.Content = ProductListView.Create(productId, ProductListOrigin.InventoryStatus);
    }

    /// <summary>
    /// 고른 상품을 들고 판매 화면을 연다. 바코드가 안 읽히는 상품을 파는 길이다.
    /// 판매 화면 자체는 메인 셸에서 여는 것과 똑같이 만든다 — 상품만 미리 넣어 준다.
    /// </summary>
    private async void OnNavigateToPosSale(string productId)
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;

        if (parentWindow is null || App.CurrentShellViewModel is not { } shellViewModel)
        {
            return;
        }

        var posSaleViewModel = new PosSaleViewModel(
            App.Services.GetRequiredService<IProductRepository>(),
            App.Services.GetRequiredService<IInventoryRepository>(),
            App.Services.GetRequiredService<ISaleService>(),
            App.Services.GetRequiredService<IReceiptPrintingService>(),
            App.Services.GetRequiredService<ICounsellingService>(),
            shellViewModel.CurrentUser.FacilityId,
            shellViewModel.CurrentUser.UserId,
            shellViewModel.CurrentUser.Username,
            shellViewModel.CurrentUser.Role,
            App.Services.GetRequiredService<UiLanguageService>());

        var posSaleView = new PosSaleView();
        posSaleView.AttachViewModel(posSaleViewModel);

        parentWindow.Content = posSaleView;

        await posSaleViewModel.PreselectProductAsync(productId);
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
