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

        var viewModel = App.Services.GetRequiredService<ProductListViewModel>();
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