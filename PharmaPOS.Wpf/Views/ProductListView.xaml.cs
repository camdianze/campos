using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using PharmaPOS.Application.Products;
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
    /// 입고 저장에 시설/사용자 ID가 필요해져서 DI로 직접 해결할 수 없게 됐다.
    /// 다른 화면들과 같은 방식으로 호출부가 만들어 넘긴다.
    /// </summary>
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