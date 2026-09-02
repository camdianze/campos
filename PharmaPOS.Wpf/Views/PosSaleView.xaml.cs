using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

public partial class PosSaleView : UserControl
{
    public PosSaleView()
    {
        InitializeComponent();

        // 계산대는 화면에 들어오자마자 스캔을 받아야 한다. 검색창을 한 번 눌러야
        // 첫 상품이 잡히면, 그 클릭은 매 판매마다 되풀이되는 헛일이다.
        Loaded += (_, _) => SearchBox.Focus();
    }

    /// <summary>
    /// 어디에 포커스가 있든 스캔을 검색창으로 돌린다.
    ///
    /// 버튼이나 배치 드롭다운을 한 번 누르는 순간 포커스가 검색창을 떠나고,
    /// 그 뒤의 스캔은 아무 데도 들어가지 않은 채 사라진다. 계산대에서는 찍었는데
    /// 아무 일도 안 일어난 것으로 보이고, 원인이 포커스라는 걸 알 방법이 없다.
    ///
    /// 사람이 직접 치고 있는 칸(수량·단가·받은 돈)은 건드리지 않는다.
    /// </summary>
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || char.IsWhiteSpace(e.Text[0]))
        {
            // 스페이스는 버튼을 누르는 키라서 가로채면 키보드 조작이 막힌다.
            return;
        }

        if (Keyboard.FocusedElement is TextBoxBase)
        {
            return;
        }

        // 드롭다운을 펼쳐 놓고 배치를 고르는 중이면 그쪽이 우선이다.
        if (Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true })
        {
            return;
        }

        SearchBox.Focus();

        // 포커스만 옮기면 이 글자가 사라진다 — 바코드 첫 자리가 빠진 채로 검색된다.
        SearchBox.Text += e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    public void AttachViewModel(PosSaleViewModel viewModel)
    {
        viewModel.SaleCompleted += OnSaleCompleted;
        viewModel.SaleCancelled += OnSaleCancelled;
        DataContext = viewModel;
    }

    private async void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not PosSaleViewModel viewModel)
        {
            return;
        }

        await viewModel.ExecuteSearchAsync();

        // 다음 스캔을 곧바로 받으려면 커서가 여기 있어야 한다. 바코드가 장바구니까지
        // 한 번에 들어가는 경로에서는 중간에 "Open a Box" 같은 창이 떴다 닫힐 수 있고,
        // 그러면 포커스가 검색창으로 돌아오지 않는다.
        SearchBox.Focus();
    }

    private void OnSaleCompleted()
    {
        // 판매 완료 후 같은 화면 유지
    }

    private void OnSaleCancelled()
    {
        NavigateBack();
    }

    private void OnBackClick(object sender, System.Windows.RoutedEventArgs e)
    {
        NavigateBack();
    }

    private void NavigateBack()
    {
        var parentWindow = System.Windows.Window.GetWindow(this) as MainWindow;
        if (parentWindow is not null)
            parentWindow.Content = new Shell.MainShellView
            {
                DataContext = App.CurrentShellViewModel
            };
    }
}