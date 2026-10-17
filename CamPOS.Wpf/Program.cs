using System.Windows;
using Velopack;

namespace CamPOS.Wpf;

/// <summary>
/// 진입점. WPF가 만들어 주는 <c>Main</c> 대신 이것을 쓴다.
///
/// <see cref="VelopackApp"/>이 <b>무엇보다 먼저</b> 돌아야 하기 때문이다. 업데이트를
/// 설치하거나 되돌리는 순간에는 이 실행 파일이 그 일만 하고 바로 끝나는데, 그때
/// WPF 화면을 띄우고 데이터베이스를 열면 할 일도 없이 무거워지고 파일을 붙들어
/// 교체를 방해한다. WPF가 만든 Main은 App 생성이 먼저라 그 자리를 내줄 수 없다.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 설치·업데이트·삭제 중이면 여기서 처리하고 프로세스가 끝난다.
        // 평소 실행에서는 아무 일도 하지 않고 지나간다.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
