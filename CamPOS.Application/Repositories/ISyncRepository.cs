using CamPOS.Application.Sync;

namespace CamPOS.Application.Repositories;

/// <summary>
/// 동기화가 보낼 행을 읽어 오고, 어디까지 보냈는지 기억한다.
///
/// 조회가 <see cref="SyncPayload"/>의 타입을 바로 돌려주는 이유: 중간에 엔티티를 거치면
/// 그 엔티티에 있는 값이 전부 "보낼 수 있는 값"처럼 보인다. 비밀번호 해시가 담긴
/// User를 받아다 골라 담는 코드는, 한 줄 잘못 쓰면 그것을 보낸다. 담을 자리가 없는
/// 타입으로 바로 읽으면 그 실수를 할 수가 없다.
/// </summary>
public interface ISyncRepository
{
    /// <summary>
    /// 상품 전체. 수백 행뿐이라 변경분을 가리지 않고 매번 보낸다 —
    /// 시각을 기준으로 고르면 PC 시계를 뒤로 돌렸을 때 그 사이 고친 상품이 조용히 빠진다.
    /// 사진은 담기지 않는다(별도 단계).
    /// </summary>
    Task<IReadOnlyList<SyncProduct>> GetProductsAsync();

    /// <summary>
    /// 그 시설의 재고 전체. 서버는 이것으로 그 약국의 재고를 통째로 바꾼다 —
    /// 수량 0인 배치는 PC에서 삭제될 수 있는데, 업로드만 하는 구조에서는
    /// "지워졌다"를 알릴 방법이 없기 때문이다. 전체를 보내면 없어진 행은 그냥 없다.
    /// </summary>
    Task<IReadOnlyList<SyncInventory>> GetInventoryAsync(string facilityId);

    /// <summary>직원 목록. 이름·역할·상태만 담긴다.</summary>
    Task<IReadOnlyList<SyncUser>> GetUsersAsync(string facilityId);

    /// <summary>
    /// <paramref name="afterRowId"/> 다음에 들어온 원장 행을, 들어온 순서로.
    /// 기준이 transaction_time이 아니라 rowid인 이유는 입고 날짜를 사용자가 고르기
    /// 때문이다 — 지난주 날짜로 오늘 입력한 행은 시각 기준으로는 영영 빠진다.
    /// </summary>
    Task<IReadOnlyList<(long RowId, SyncTransaction Row)>> GetTransactionsAfterAsync(
        string facilityId, long afterRowId, int limit);

    /// <inheritdoc cref="GetTransactionsAfterAsync"/>
    Task<IReadOnlyList<(long RowId, SyncCounsellingLog Row)>> GetCounsellingLogsAfterAsync(
        long afterRowId, int limit);

    /// <inheritdoc cref="GetTransactionsAfterAsync"/>
    Task<IReadOnlyList<(long RowId, SyncUsageEvent Row)>> GetUsageEventsAfterAsync(
        long afterRowId, int limit);

    /// <summary>어디까지 보냈는지. 보낸 적이 없으면 0.</summary>
    /// <summary>
    /// 아직 보내지 않은 원장·상담 행을 합해서 센다.
    ///
    /// 화면에 "밀린 행 수"를 적기 위한 것이고, 그 숫자가 이 기능에서 제일 중요한
    /// 표시다. 자동 동기화가 조용히 실패하면 앱은 평소와 똑같아 보이는데, 밀린
    /// 수가 늘어나는 것만은 눈에 보인다.
    /// </summary>
    /// <param name="includePharmacyData">
    /// 약국 데이터 전송을 끈 약국에서는 false다. 그 행들은 보내지 않기로 했으므로
    /// "밀린 것"이 아니다 — 세면 영영 줄지 않는 숫자가 화면에 남고, 상단 바
    /// 배지가 그것을 고장으로 읽어 계속 경고한다.
    /// </param>
    Task<int> CountPendingAsync(string facilityId, bool includePharmacyData = true);

    // ── 사진 ─────────────────────────────────────────────────────────────
    // 사진은 원장과 달리 rowid 워터마크로 고를 수 없다. 덧붙기만 하는 것이
    // 아니라 같은 제품의 사진이 바뀌기 때문이다. 그래서 "지금 사진의 해시"와
    // "마지막으로 올린 사진의 해시"를 비교해 고른다.

    /// <summary>
    /// 아직 올리지 않았거나 올린 뒤에 바뀐 사진. 바이트까지 함께 읽으므로
    /// 한 번에 많이 가져오지 않는다 — 장당 수백 KB다.
    /// </summary>
    Task<IReadOnlyList<SyncPhoto>> GetPhotosToUploadAsync(int limit);

    /// <summary>올라간 사진을 "올림"으로 표시한다. 전송이 확인된 뒤에만 부른다.</summary>
    Task MarkPhotoUploadedAsync(string productId, string sha256);

    /// <summary>아직 안 올린 사진 수. 바이트는 읽지 않는다.</summary>
    Task<int> CountPhotosToUploadAsync();

    Task<long> GetPositionAsync(string stream);

    /// <summary>한 묶음이 서버에 들어간 뒤에만 부른다. 보내기 전에 올리면 그 구간을 잃는다.</summary>
    Task SavePositionAsync(string stream, long position);
}
