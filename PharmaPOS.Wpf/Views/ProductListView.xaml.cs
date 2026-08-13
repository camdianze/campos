using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using Lightweight_Digital_Inventory_Management___POS_System.Shell;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class ProductListView : UserControl
{
    public ProductListView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ViewModel까지 붙여서 만든다.
    ///
    /// 입고 저장에 시설/사용자 ID가 필요해지면서 DI가 이 ViewModel을 만들 수 없게 됐는데,
    /// 그 뒤로 화면을 여는 곳이 다섯 군데라 한 곳이라도 AttachViewModel을 빠뜨리면
    /// 빈 화면이 뜬다. 만드는 방법을 여기 한 곳으로 모아 그 실수를 없앤다.
    /// </summary>
    /// <param name="preselectProductId">
    /// 열자마자 고를 상품. 재고 화면의 "View Product Details"가 넘긴다.
    /// </param>
    /// <param name="origin">← Back이 돌아갈 곳. 넘기지 않으면 종전대로 메인 셸이다.</param>
    public static ProductListView Create(
        string? preselectProductId = null,
        ProductListOrigin origin = ProductListOrigin.MainShell)
    {
        var view = new ProductListView();

        var shellViewModel = App.CurrentShellViewModel;

        view.AttachViewModel(new ProductListViewModel(
            App.Services.GetRequiredService<IProductRepository>(),
            App.Services.GetRequiredService<IProductService>(),
            App.Services.GetRequiredService<IAntibioticMatchingService>(),
            App.Services.GetRequiredService<IStockInService>(),
            shellViewModel?.CurrentUser.FacilityId ?? string.Empty,
            shellViewModel?.CurrentUser.UserId ?? string.Empty,
            preselectProductId,
            origin));

        return view;
    }

    /// <summary>ViewModel을 직접 만들어 넘기는 경우에 쓴다.</summary>
    public void AttachViewModel(ProductListViewModel viewModel)
    {
        viewModel.NavigateToAddProduct += OnNavigateToAddProduct;
        viewModel.NavigateToEditProduct += OnNavigateToEditProduct;
        viewModel.NavigateToPrintBarcode += OnNavigateToPrintBarcode;
        viewModel.RequestScrollToRow += ScrollToRow;

        DataContext = viewModel;

        // 목록을 읽는 동안 스크롤 요청이 이미 지나갔을 수 있다. 그 경우를 위해 한 번 더 본다.
        if (viewModel.RowToScrollTo is { } row)
        {
            ScrollToRow(row);
        }
    }

    /// <summary>알림 화면으로 돌아갈 때 쓴다. 알림은 볼 때마다 새로 계산하므로 평소대로 만든다.</summary>
    private static AlertsView CreateAlertsView()
    {
        var alertsView = new AlertsView();

        alertsView.AttachViewModel(new AlertsViewModel(
            App.Services.GetRequiredService<IAlertService>(),
            App.CurrentShellViewModel?.CurrentUser.FacilityId ?? string.Empty));

        return alertsView;
    }

    /// <summary>
    /// 미리 고른 줄을 화면 안으로 끌어온다. 목록이 길면 선택만 해서는 보이지 않는다.
    /// 아직 화면이 그려지기 전이면 ScrollIntoView가 아무 일도 하지 않으므로,
    /// 레이아웃이 끝난 뒤(Loaded)에 한 번 더 시도한다.
    /// </summary>
    private void ScrollToRow(ProductRow row)
    {
        if (IsLoaded)
        {
            ProductsGrid.ScrollIntoView(row);
            return;
        }

        void OnLoadedOnce(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoadedOnce;
            ProductsGrid.ScrollIntoView(row);
        }

        Loaded += OnLoadedOnce;
    }

    private void OnNavigateToAddProduct()
    {
        NavigateToProductEdit(existingProduct: null);
    }

    private void OnNavigateToEditProduct(Product product)
    {
        NavigateToProductEdit(existingProduct: product);
    }

    private void NavigateToProductEdit(Product? existingProduct)
    {
        var productService = App.Services.GetRequiredService<IProductService>();
        var editViewModel = new ProductEditViewModel(productService, existingProduct);

        var editView = new ProductEditView();
        editView.AttachViewModel(editViewModel);

        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = editView;
        }
    }

    private void OnNavigateToPrintBarcode(Product product)
    {
        var barcodeService = App.Services.GetRequiredService<IInternalBarcodeService>();
        var barcodeViewModel = new InternalBarcodeViewModel(barcodeService, product);

        var barcodeView = new InternalBarcodeView();
        barcodeView.AttachViewModel(barcodeViewModel);

        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = barcodeView;
        }
    }

    /// <summary>
    /// 들어온 곳으로 돌아간다. 재고 화면에서 상품 상세를 보러 온 경우에는
    /// 메인 셸이 아니라 재고 화면으로 돌아가야 흐름이 끊기지 않는다.
    /// 재고 화면은 평소대로 새로 연다 — 직전 필터나 스크롤까지 되살릴 이유는 없다.
    /// </summary>
    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;

        if (parentWindow is null)
        {
            return;
        }

        if (DataContext is ProductListViewModel { Origin: var origin } && origin != ProductListOrigin.MainShell)
        {
            parentWindow.Content = origin switch
            {
                ProductListOrigin.InventoryStatus => new InventoryStatusView(),
                ProductListOrigin.Alerts => CreateAlertsView(),
                _ => parentWindow.Content
            };

            return;
        }

        parentWindow.Content = new MainShellView
        {
            DataContext = App.CurrentShellViewModel
        };
    }
}