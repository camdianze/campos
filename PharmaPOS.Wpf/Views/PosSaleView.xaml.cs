using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;
using PharmaPOS.Application.Inventory;

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

    // ── 장바구니 수량 편집 ────────────────────────────────────────────────
    //
    // Enter 또는 칸을 떠나는 순간이 적용이다. Esc는 원래 수량으로 되돌린다.
    // 적용이 재고 검사에서 막히면 ViewModel이 수량을 바꾸지 않으므로 칸의 글자만
    // 되돌리면 된다. 적용에 성공하면 ViewModel이 그 줄을 다시 그리는데, 그때 이 TextBox는
    // 없어지므로 LostFocus가 한 번 더 와도 "바뀐 게 없다"로 끝난다.

    private bool _isApplyingCartQuantity;

    private async void OnCartQuantityKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not SaleLineItem line)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            box.Text = line.Quantity.ToString();
            e.Handled = true;
            SearchBox.Focus();
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ApplyCartQuantityAsync(box, line);

        // 다음 스캔을 곧바로 받으려면 커서가 검색창에 있어야 한다.
        SearchBox.Focus();
    }

    private async void OnCartQuantityLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox box && box.Tag is SaleLineItem line)
        {
            await ApplyCartQuantityAsync(box, line);
        }
    }

    private async Task ApplyCartQuantityAsync(TextBox box, SaleLineItem line)
    {
        if (_isApplyingCartQuantity || DataContext is not PosSaleViewModel viewModel)
        {
            return;
        }

        var text = box.Text.Trim();

        if (text == line.Quantity.ToString())
        {
            return;
        }

        _isApplyingCartQuantity = true;

        try
        {
            if (!int.TryParse(text, out var quantity))
            {
                viewModel.Message = "Quantity must be a whole number.";
                box.Text = line.Quantity.ToString();
                return;
            }

            if (!await viewModel.ChangeCartQuantityAsync(line, quantity))
            {
                box.Text = line.Quantity.ToString();
            }
        }
        finally
        {
            _isApplyingCartQuantity = false;
        }
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