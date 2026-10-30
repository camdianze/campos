using CamPOS.Application.Repositories;
using CamPOS.Domain.Enums;

namespace CamPOS.Application.Usage;

/// <summary>
/// <see cref="IUsageRecorder"/>의 구현. 하는 일은 세 가지뿐이다 — 이름을 검사하고,
/// 시각·역할·버전을 찍고, 어떤 실패도 삼킨다.
///
/// Singleton인 이유는 로그인한 역할을 들고 있기 때문이다. 저장소는 Transient라
/// 생성자로 받지 않고 팩토리로 매번 받는다 — Singleton이 Transient를 붙잡으면
/// 그 하나가 앱이 꺼질 때까지 살아남는다.
/// </summary>
public sealed class UsageRecorder : IUsageRecorder
{
    private readonly Func<IUsageEventRepository> _repositoryFactory;
    private readonly string _appVersion;

    /// <summary>
    /// 로그인한 사람의 역할. 쓰기와 읽기가 다른 스레드에서 일어날 수 있어
    /// <c>volatile</c>로 둔다 — 틀린 역할이 한 줄 적히는 것은 큰일이 아니지만,
    /// 그 한 줄을 막는 데 자물쇠를 거는 것은 판매 경로에 자물쇠를 거는 일이다.
    /// </summary>
    private volatile string? _role;

    public UsageRecorder(Func<IUsageEventRepository> repositoryFactory, string appVersion)
    {
        _repositoryFactory = repositoryFactory;
        _appVersion = appVersion;
    }

    public void StartSession(UserRole role) => _role = role.ToString();

    public void EndSession() => _role = null;

    public async Task RecordAsync(string eventName, string outcome)
    {
        try
        {
            // 목록에 없는 이름은 적지 않는다. 호출부가 문자열을 지어낼 수 있으면
            // 동의서에 적힌 목록은 지켜지는지 확인할 수 없는 약속이 된다.
            if (!UsageEventNames.All.Contains(eventName) || !UsageOutcome.All.Contains(outcome))
            {
                return;
            }

            await _repositoryFactory().InsertAsync(new UsageEvent
            {
                EventId = Guid.NewGuid().ToString(),
                OccurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Role = _role,
                EventName = eventName,
                Outcome = outcome,
                AppVersion = _appVersion
            });
        }
        catch
        {
            // 일부러 삼킨다. 이 줄이 없으면 디스크가 찼을 때 계산대에서 판매가
            // 멈춘다 — 기록을 남기지 못한 것과 판매를 못 한 것은 비교할 수 없다.
            // 로그조차 남기지 않는 이유는, 기록이 안 되는 상황이면 로그도 안 될
            // 가능성이 높고 그 시도가 다시 던질 수 있기 때문이다.
        }
    }
}
