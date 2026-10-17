using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Receipts;

/// <summary>
/// 영수증 설정을 읽고 저장한다.
/// </summary>
public interface IReceiptSettingsService
{
    /// <summary>
    /// 저장된 설정을 읽는다. 키가 없거나 값이 비었거나 형식이 깨졌으면
    /// 그 항목만 코드에 정의된 기본값으로 대체한다. 예외를 던지지 않는다 —
    /// 설정 하나 때문에 영수증이 안 나오면 계산대가 멈춘다.
    /// </summary>
    Task<ReceiptSettings> GetAsync();

    /// <summary>
    /// 설정을 저장한다.
    ///
    /// 권한 검사를 여기서 한다. 화면에서 버튼을 숨기는 것만으로는 부족하다 —
    /// 설정 화면을 여는 경로는 코드 몇 줄이면 우회할 수 있고, 영수증에 찍히는
    /// 상호·세금 정보는 아무나 바꿔서는 안 되는 값이다.
    /// </summary>
    /// <param name="actingUserRole">저장을 시도하는 사용자의 역할.</param>
    /// <param name="actingUserId">누가 바꿨는지 기록에 남길 사용자 ID.</param>
    Task<ReceiptSettingsSaveResult> SaveAsync(
        ReceiptSettings settings, UserRole actingUserRole, string actingUserId);
}
