using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Lightweight_Digital_Inventory_Management___POS_System.Services;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class AlertsView : UserControl
{
    /// <summary>
    /// 마지막 우클릭이 실제 줄 위에서 일어났는지.
    /// 빈 곳을 우클릭해도 이전 선택이 남아 있어, 선택 여부만으로는 알 수 없다.
    /// </summary>
    private bool _rightClickHitRow;

    public AlertsView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(AlertsViewModel viewModel)
    {
        viewModel.NavigateToInventory += OnNavigateToInventory;
        viewModel.NavigateToProduct += OnNavigateToProduct;
        DataContext = viewModel;

        // 종류·우선순위 열은 변환기가 그리는데, 변환기는 값이 바뀔 때만 다시 돈다.
        // 언어만 바뀌면 값은 그대로라 목록을 다시 읽어야 새 말로 나온다.
        // 언어 서비스는 앱과 수명이 같으니 약한 구독으로 걸어 둔다.
        //
        // 핸들러는 인스턴스 메서드여야 한다. 람다를 넘기면 WeakEventManager가 그 람다의
        // 클로저를 약하게만 붙들어, 다음 GC에서 걷히고 나면 아무 일도 안 일어난다 —
        // 오류 없이 그냥 멈춘다. 이 뷰가 살아 있는 동안은 뷰의 메서드도 살아 있다.
        var uiLanguage = App.Services.GetRequiredService<UiLanguageService>();
        WeakEventManager<UiLanguageService, EventArgs>.AddHandler(
            uiLanguage, nameof(UiLanguageService.LanguageChanged), OnLanguageChanged);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (DataContext is AlertsViewModel viewModel)
        {
            _ = viewModel.ReloadAsync();
        }
    }

    /// <summary>
    /// 우클릭한 줄을 선택 상태로 만든다.
    /// DataGrid도 TreeView처럼 우클릭으로는 선택이 바뀌지 않아, 그냥 두면
    /// 메뉴가 엉뚱한 알림에 작용한다.
    /// </summary>
    private void OnGridRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);

        _rightClickHitRow = row is not null;

        if (row is not null)
        {
            row.IsSelected = true;
        }
    }

    /// <summary>빈 곳에서 우클릭했으면 메뉴를 띄우지 않는다.</summary>
    private void OnGridContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!_rightClickHitRow || DataContext is not AlertsViewModel { HasSelection: true })
        {
            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null and not T)
        {
            // 줄 안의 요소가 시각 트리에 없는 경우가 있어 논리 부모도 함께 본다.
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return source as T;
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

    private void OnNavigateToInventory(string productName)
    {
        var inventoryView = new InventoryStatusView();

        if (inventoryView.DataContext is InventoryStatusViewModel viewModel)
        {
            viewModel.SearchTerm = productName;
        }

        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = inventoryView;
        }
    }

    /// <summary>
    /// 알림의 상품이 이미 선택된 채로 상품 목록 화면을 연다.
    /// ← Back으로 이 화면에 돌아올 수 있도록 진입 출처를 함께 넘긴다.
    /// </summary>
    private void OnNavigateToProduct(string productId)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;

        if (parentWindow is null)
        {
            return;
        }

        parentWindow.Content = ProductListView.Create(productId, ProductListOrigin.Alerts);
    }
}
