namespace PharmaPOS.Application.Counselling;

/// <summary>
/// AWaRe 시드 파일을 참조 테이블로 적재한다. 앱 시작 시 한 번 호출한다.
/// </summary>
public interface IAwareSeedLoader
{
    /// <summary>
    /// 시드 파일이 이전에 적재한 것과 달라졌을 때만 다시 적재한다.
    /// 매 실행마다 수백 행을 다시 넣지 않기 위한 것이다.
    /// </summary>
    Task<AwareSeedLoadResult> LoadIfChangedAsync();
}
