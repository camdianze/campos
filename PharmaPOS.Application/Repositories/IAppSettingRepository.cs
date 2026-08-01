namespace PharmaPOS.Application.Repositories;

/// <summary>
/// 앱 전역 설정 키-값 저장소.
/// </summary>
public interface IAppSettingRepository
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);
}
