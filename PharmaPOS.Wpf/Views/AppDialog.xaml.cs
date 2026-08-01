using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

/// <summary>
/// 앱 테마에 맞춘 공용 알림/확인 창.
///
/// 시스템 MessageBox를 그대로 쓰면 팝업만 다른 프로그램처럼 보인다.
/// 색·모서리·버튼 모양을 App.xaml 팔레트에서 가져와 나머지 화면과 맞춘다.
///
/// 사용법:
///   AppDialog.Show("Receipt", text, monospace: true);
///   if (AppDialog.Confirm("Confirm", "…?")) { … }
/// </summary>
public partial class AppDialog : Window
{
    private AppDialog()
    {
        InitializeComponent();
    }

    /// <summary>알림. 확인 버튼 하나만 있다.</summary>
    public static void Show(string title, string message, bool monospace = false)
    {
        Build(title, message, monospace, confirmText: "OK", cancelText: null).ShowDialog();
    }

    /// <summary>확인. 사용자가 확인을 누르면 true.</summary>
    public static bool Confirm(
        string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        return Build(title, message, monospace: false, confirmText, cancelText).ShowDialog() == true;
    }

    private static AppDialog Build(
        string title, string message, bool monospace, string confirmText, string? cancelText)
    {
        var dialog = new AppDialog
        {
            // 소유 창을 지정해야 가운데 정렬되고, 뒤 창을 가리지 않는다.
            // 시작 화면처럼 아직 창이 없을 때를 대비해 없으면 화면 중앙에 띄운다.
            Owner = Application.Current?.MainWindow is { IsLoaded: true } main ? main : null
        };

        if (dialog.Owner is null)
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmText;

        if (monospace)
        {
            // 영수증이나 복약안내처럼 자리를 맞춰 만든 내용은 고정폭이라야 줄이 어긋나지 않는다.
            dialog.MessageText.FontFamily = new FontFamily("Consolas, Courier New, Malgun Gothic");
            dialog.MessageText.FontSize = 12;
            dialog.Width = 560;
        }

        if (cancelText is not null)
        {
            dialog.CancelButton.Content = cancelText;
            dialog.CancelButton.Visibility = Visibility.Visible;
        }

        return dialog;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    /// <summary>창 테두리를 없앴으므로 본문을 끌어 옮길 수 있게 해 준다.</summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
