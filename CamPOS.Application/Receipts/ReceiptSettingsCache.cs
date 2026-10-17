namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증 설정의 메모리 캐시.
///
/// 판매 한 건마다 설정 21개를 DB에서 다시 읽을 이유가 없다. 설정은 관리자가
/// 가끔 바꾸는 값이라 10분쯤 묵어도 문제가 없고, 저장할 때 곧바로 버리므로
/// 방금 바꾼 설정이 다음 영수증에 반영되지 않는 일은 생기지 않는다.
///
/// 설정 서비스는 화면을 열 때마다 새로 만들어지므로(Transient) 캐시는 여기
/// 따로 두고 Singleton으로 등록한다. 저장 잠금도 같은 이유로 여기 있다 —
/// 두 창이 동시에 저장을 눌러도 21개 키가 섞여 쓰이지 않아야 한다.
/// </summary>
public class ReceiptSettingsCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly object _gate = new();

    private ReceiptSettings? _cached;
    private DateTimeOffset _loadedAt;

    /// <summary>저장을 직렬화한다. 스크립트 잠금(LockService)에 해당하는 자리다.</summary>
    public SemaphoreSlim SaveLock { get; } = new(1, 1);

    /// <summary>
    /// 살아 있는 캐시가 있으면 그 사본을 돌려준다. 사본을 주는 이유는
    /// 설정 화면이 편집 중인 값을 캐시에 흘려 넣지 못하게 하기 위해서다.
    /// </summary>
    public ReceiptSettings? TryGet()
    {
        lock (_gate)
        {
            if (_cached is null || DateTimeOffset.UtcNow - _loadedAt > Lifetime)
            {
                return null;
            }

            return _cached.Clone();
        }
    }

    public void Set(ReceiptSettings settings)
    {
        lock (_gate)
        {
            _cached = settings.Clone();
            _loadedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _cached = null;
        }
    }
}
