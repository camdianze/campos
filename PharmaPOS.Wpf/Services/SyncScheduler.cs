using Microsoft.Extensions.DependencyInjection;
using PharmaPOS.Application.Sync;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 자동 전송의 시계. 로그인한 뒤에 한 번 시작하고, 앱이 살아 있는 동안 계속 돈다.
///
/// WPF에 두는 이유는 이것이 규칙이 아니라 <b>앱의 수명</b>에 매인 물건이기
/// 때문이다. 무엇을 보내고 언제 워터마크를 올리는지는 전부 SyncService가 알고
/// 있고, 여기는 "주기적으로 그걸 부른다"만 한다.
///
/// 화면과 무관하게 살아야 한다. ViewModel에 두면 Data Center를 나가는 순간
/// 멈추고, 약국은 그 화면을 하루에 한 번도 열지 않는다.
/// </summary>
public sealed class SyncScheduler : IDisposable
{
    /// <summary>
    /// 전송 간격. 약국은 아침에 켜서 저녁에 끄므로 30분이면 하루치가 그날 안에
    /// 올라간다. 더 짧게 잡아 봐야 느린 회선을 더 자주 쓸 뿐이고, 더 길게 잡으면
    /// 중간에 PC가 꺼졌을 때 잃는 구간이 길어진다.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 앱을 켜고 첫 전송까지의 틈. 켜자마자 보내면 로그인 직후가 가장 느려지고,
    /// 그때는 사람이 화면을 기다리고 있다.
    /// </summary>
    public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _services;
    private CancellationTokenSource? _stopping;
    private Task? _loop;

    public SyncScheduler(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// 돌기 시작한다. 이미 돌고 있으면 아무것도 하지 않는다 — 로그아웃했다가
    /// 다시 로그인해도 시계가 둘이 되지 않아야 한다.
    /// </summary>
    public void Start(string facilityId)
    {
        if (_loop is not null)
        {
            return;
        }

        _stopping = new CancellationTokenSource();
        _loop = RunAsync(facilityId, _stopping.Token);
    }

    public void Stop()
    {
        _stopping?.Cancel();
        _loop = null;
    }

    private async Task RunAsync(string facilityId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(StartupDelay, cancellationToken);

            using var timer = new PeriodicTimer(Interval);

            do
            {
                await TickAsync(facilityId, cancellationToken);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // 앱이 닫히는 중이다.
        }
        catch
        {
            // 시계가 죽어도 앱은 계속 돌아야 한다. 다음 실행에서 다시 시작된다.
        }
    }

    private async Task TickAsync(string facilityId, CancellationToken cancellationToken)
    {
        try
        {
            // 매번 새로 받는다. SyncService는 Transient이고, 설정은 그사이 바뀔 수 있다.
            var sync = _services.GetRequiredService<SyncService>();

            if (!await sync.IsAutomaticAsync())
            {
                return;
            }

            // 결과는 보지 않는다. 실패는 조용히 지나가고 다음 주기에 다시 시도한다 —
            // 인터넷이 잠깐 끊긴 약국에서 팝업이 하루 종일 뜨면 안 되고, 며칠째
            // 못 보내고 있다는 사실은 로그인할 때 상단 바가 말한다.
            //
            // 워터마크는 서버가 확인해 준 만큼만 올라가므로, 실패한 전송이 행을
            // 건너뛰게 만들지 않는다.
            await sync.RunAsync(facilityId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 한 번의 실패로 시계를 멈추지 않는다.
        }
    }

    public void Dispose()
    {
        _stopping?.Cancel();
        _stopping?.Dispose();
    }
}
