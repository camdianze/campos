using System.Windows;
using System.Windows.Controls;

namespace Lightweight_Digital_Inventory_Management___POS_System.Controls;

/// <summary>
/// 비밀번호 칸 + 눈 버튼. 화면의 <c>PasswordBox</c>를 통째로 대신한다.
///
/// WPF의 PasswordBox는 내용을 평문으로 보여 주는 방법이 아예 없다 — 마스킹이
/// 컨트롤에 박혀 있어 템플릿으로도 풀 수 없다. 그래서 PasswordBox와 TextBox를
/// 같은 자리에 겹쳐 두고 보이는 쪽만 바꾼다. 값은 <b>바꾸는 순간에만</b> 옮긴다.
/// 두 칸을 계속 동기화하면 한쪽의 변경이 다른 쪽을 바꾸고 그게 되돌아오는 고리가
/// 생겨서, 막으려면 플래그가 필요해지고 그 플래그가 틀리면 타이핑이 씹힌다.
///
/// 이 기능이 필요한 이유는 오타다. 별표만 보이는 칸에 한 글자를 잘못 치면 화면에는
/// 아무 단서가 없고, 틀렸다는 말만 돌아온다. 계산대 뒤에서 혼자 로그인하는 상황이라
/// 어깨너머로 볼 사람도 없는데, 그 대가로 매번 다시 치게 된다.
///
/// 평문을 TextBox에 담는 것이 PasswordBox보다 덜 안전한 것은 사실이지만, 이 앱은
/// 이미 <c>PasswordBox.Password</c>를 string으로 꺼내 서비스까지 넘긴다(bcrypt가
/// string을 받는다). 새로 생기는 노출이 아니라 같은 값이 잠깐 더 머무는 것이고,
/// 눈 버튼을 누른 사람이 그 순간을 정한다.
/// </summary>
public partial class PasswordField : UserControl
{
    public PasswordField()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 입력된 값. 어느 칸이 보이는 중인지에 따라 그쪽에서 읽는다 —
    /// 보이는 칸이 곧 사용자가 방금 친 칸이다.
    /// </summary>
    public string Password
    {
        get => RevealToggle.IsChecked == true ? Revealed.Text : Masked.Password;
        set
        {
            Masked.Password = value;
            Revealed.Text = value;
        }
    }

    /// <summary>두 칸을 함께 비운다. 보이지 않는 쪽에 값이 남으면 다음에 되살아난다.</summary>
    public void Clear()
    {
        Masked.Clear();
        Revealed.Clear();
    }

    /// <summary>지금 보이는 칸에 커서를 둔다. UserControl 자신에 포커스를 줘도 소용이 없다.</summary>
    public new bool Focus() => ActiveInput.Focus();

    private Control ActiveInput => RevealToggle.IsChecked == true ? Revealed : Masked;

    private void OnRevealChanged(object sender, RoutedEventArgs e)
    {
        var revealing = RevealToggle.IsChecked == true;

        if (revealing)
        {
            Revealed.Text = Masked.Password;
            Revealed.Visibility = Visibility.Visible;
            Masked.Visibility = Visibility.Collapsed;
            Revealed.CaretIndex = Revealed.Text.Length;
        }
        else
        {
            Masked.Password = Revealed.Text;
            Masked.Visibility = Visibility.Visible;
            Revealed.Visibility = Visibility.Collapsed;
        }

        RevealToggle.ToolTip = revealing ? "Hide password" : "Show password";

        // 버튼을 누르면 포커스가 칸을 떠난다. 돌려주지 않으면 이어서 치던 사람이
        // 허공에 입력하게 되고, 그게 Enter면 폼이 그대로 제출된다.
        ActiveInput.Focus();
    }
}
