using System.Windows.Controls;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

public partial class SalesAnalysisView : UserControl
{
    public SalesAnalysisView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ViewModel은 시설 ID를 생성자로 받으므로 DI가 만들 수 없다.
    /// 다른 화면과 같이 호출부가 만들어 넘긴다.
    ///
    /// 돌아갈 곳을 <b>호출부가 정한다</b>. 이 화면에 들어오는 길이 둘이다 —
    /// History 화면의 카드와 관리자 대시보드의 타일. 여기서 한 곳을 박아 두면
    /// 다른 쪽으로 들어온 사람이 ← Back을 눌렀을 때 오지 않은 화면으로 간다.
    /// (판매 이력·재고 이력 화면이 같은 방식이다.)
    /// </summary>
    public void AttachViewModel(SalesAnalysisViewModel viewModel, Action onBack)
    {
        viewModel.NavigateBack += onBack;
        DataContext = viewModel;
    }
}
