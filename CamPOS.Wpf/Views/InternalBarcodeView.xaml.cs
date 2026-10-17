using System.Windows.Controls;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

public partial class InternalBarcodeView : UserControl
{
    public InternalBarcodeView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(InternalBarcodeViewModel viewModel)
    {
        viewModel.NavigateBack += OnNavigateBack;
        DataContext = viewModel;
    }

    private void OnNavigateBack()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
        {
            parentWindow.Content = ProductListView.Create();
        }
    }
}