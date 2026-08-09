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
    public static ProductListView Create()
    {
        var view = new ProductListView();

        var shellViewModel = App.CurrentShellViewModel;

        view.AttachViewModel(new ProductListViewModel(
            App.Services.GetRequiredService<IProductRepository>(),
            App.Services.GetRequiredService<IProductService>(),
            App.Services.GetRequiredService<IAntibioticMatchingService>(),
            App.Services.GetRequiredService<IStockInService>(),
            shellViewModel?.CurrentUser.FacilityId ?? string.Empty,
            shellViewModel?.CurrentUser.UserId ?? string.Empty));

        return view;
    }

    /// <summary>ViewModel을 직접 만들어 넘기는 경우에 쓴다.</summary>
    public void AttachViewModel(ProductListViewModel viewModel)
    {
        viewModel.NavigateToAddProduct += OnNavigateToAddProduct;
        viewModel.NavigateToEditProduct += OnNavigateToEditProduct;
        viewModel.NavigateToPrintBarcode += OnNavigateToPrintBarcode;

        DataContext = viewModel;
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

    private void OnBackClick(object sender, RoutedEventArgs e)
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