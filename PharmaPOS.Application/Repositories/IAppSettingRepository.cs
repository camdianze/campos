namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 앱 전역 설정 키-값 저장소.
/// </summary>
public interface IAppSettingRepository
{
    Task<string?> GetAsync(string key);

    /// <summary>
    /// 여러 키를 한 번에 읽는다. 값이 없는 키는 결과에 들어오지 않는다.
    /// 영수증 설정처럼 화면 하나가 스무 개 넘는 키를 읽을 때, 키마다 연결을
    /// 새로 여는 것을 피하려고 둔다.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetManyAsync(IReadOnlyList<string> keys);

    /// <summary>
    /// 값을 저장한다.
    /// valueType은 값의 종류(text/enum/bool/number)를, updatedBy는 누가 바꿨는지를 남긴다.
    /// 둘 다 기록용이라 없어도 저장은 된다 — 기존 호출부는 그대로 둔 채 새 설정만 채운다.
    /// </summary>
    Task SetAsync(string key, string value, string? valueType = null, string? updatedBy = null);
}
