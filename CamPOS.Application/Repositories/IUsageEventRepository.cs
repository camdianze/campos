using CamPOS.Application.Usage;

namespace CamPOS.Application.Repositories;

/// <summary>
/// 사용 기록을 적는 곳. 읽는 길은 없다 — 앱 안에서 이 기록을 보여주는 화면은
/// 없고, 읽는 것은 동기화가 <see cref="ISyncRepository"/>를 통해 하는 일이다.
/// </summary>
public interface IUsageEventRepository
{
    /// <summary>
    /// 한 줄 적는다. <b>실패하면 던진다</b> — 삼키는 것은 UsageRecorder의 일이고,
    /// 여기서까지 삼키면 저장소가 잘못되었는지 테스트가 확인할 방법이 없어진다.
    /// </summary>
    Task InsertAsync(UsageEvent usageEvent);
}
