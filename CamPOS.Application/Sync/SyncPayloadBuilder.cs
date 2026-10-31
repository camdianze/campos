using CamPOS.Application.Repositories;

namespace CamPOS.Application.Sync;

/// <summary>
/// 보낼 묶음을 만든다. 서버로 보내지는 않는다 — 1단계에서는 이 결과를 파일로 적어
/// 눈으로 확인하고, 전송은 다음 단계에서 붙인다.
///
/// 나누는 이유는 순서다. 무엇을 보내는지가 먼저 확정되어야 한다. 전송을 먼저 만들면
/// 잘못된 내용이 서버에 들어간 뒤에야 알게 되고, 보낸 것은 되돌릴 수 없다.
/// </summary>
public sealed class SyncPayloadBuilder
{
    /// <summary>
    /// 한 묶음에 담을 원장 행 수. 500행을 넘기면 요청 하나가 커져 끊긴 연결에서
    /// 통째로 다시 보내야 한다 — 잘게 나눌수록 다시 보내는 양이 적다.
    /// </summary>
    public const int BatchRows = 500;

    private readonly ISyncRepository _repository;

    public SyncPayloadBuilder(ISyncRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 보낼 묶음 하나를 만든다.
    ///
    /// <paramref name="includeCatalogue"/>를 끄면 상품·재고·직원을 빼고 원장만 담는다.
    /// 밀린 행이 한 묶음에 안 들어가 여러 번 보낼 때, 수백 개의 상품을 묶음마다
    /// 다시 보낼 이유가 없기 때문이다. 첫 묶음은 켜서 보낸다.
    ///
    /// <paramref name="includePharmacyData"/>를 끄면 <b>사용 기록만</b> 남는다 —
    /// 상품·재고·직원·원장·상담이 전부 빠진다. "매출은 보내고 싶지 않다"는 약국을
    /// 위한 것이고, 둘을 한 통로에 묶어 두면 그런 약국에게서는 아무것도 못 받는다.
    ///
    /// 통로를 따로 내지 않고 <b>무엇을 담을지</b>만 정하는 이유: 통로가 둘이면
    /// 워터마크도 둘, 실패 지점도 둘이 되고 "사용 기록은 올라오는데 판매는
    /// 안 올라온다"는 상태가 생긴다. 지금은 한 묶음이 전부 가거나 전부 안 간다.
    ///
    /// 빼는 것은 담지 않는 것으로 끝난다 — 쿼리를 아예 돌리지 않으므로 위치도
    /// 적히지 않고, 그래서 그 행들의 워터마크는 제자리에 멈춘다. 나중에 다시
    /// 켜면 그동안의 것이 전부 올라간다.
    /// </summary>
    public async Task<SyncPayload> BuildAsync(
        string facilityId, bool includeCatalogue = true, bool includePharmacyData = true)
    {
        if (!includePharmacyData)
        {
            return await BuildUsageOnlyAsync();
        }

        var transactionFrom = await _repository.GetPositionAsync(SyncStreams.Transactions);
        var counsellingFrom = await _repository.GetPositionAsync(SyncStreams.CounsellingLogs);

        var transactions = await _repository.GetTransactionsAfterAsync(
            facilityId, transactionFrom, BatchRows);

        var counselling = await _repository.GetCounsellingLogsAfterAsync(
            counsellingFrom, BatchRows);

        var usage = await _repository.GetUsageEventsAfterAsync(
            await _repository.GetPositionAsync(SyncStreams.UsageEvents), BatchRows);

        // 담은 것이 있을 때만 위치를 적는다. 빈 묶음에 옛 위치를 적으면 뜻은 같지만,
        // "이 묶음이 어디까지 책임지는가"가 흐려진다.
        var positions = new Dictionary<string, long>();

        if (transactions.Count > 0)
        {
            positions[SyncStreams.Transactions] = transactions[^1].RowId;
        }

        if (counselling.Count > 0)
        {
            positions[SyncStreams.CounsellingLogs] = counselling[^1].RowId;
        }

        if (usage.Count > 0)
        {
            positions[SyncStreams.UsageEvents] = usage[^1].RowId;
        }

        return new SyncPayload
        {
            AppVersion = AppVersion.Display,
            ClientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Products = includeCatalogue ? await _repository.GetProductsAsync() : [],
            Inventory = includeCatalogue ? await _repository.GetInventoryAsync(facilityId) : [],
            Users = includeCatalogue ? await _repository.GetUsersAsync(facilityId) : [],
            Transactions = transactions.Select(t => t.Row).ToList(),
            CounsellingLogs = counselling.Select(c => c.Row).ToList(),
            UsageEvents = usage.Select(u => u.Row).ToList(),
            Positions = positions
        };
    }

    /// <summary>
    /// 사용 기록만 담은 묶음. 약국 데이터 전송을 끈 약국에서 쓴다.
    ///
    /// 원장 쿼리를 아예 돌리지 않는 것이 요점이다. 읽어 놓고 버리면 언젠가
    /// 누군가 "이미 읽었으니 담자"고 고치게 되고, 그 한 줄이 약국과의 약속을
    /// 깬다. 읽지 않으면 담을 수가 없다.
    /// </summary>
    private async Task<SyncPayload> BuildUsageOnlyAsync()
    {
        var usage = await _repository.GetUsageEventsAfterAsync(
            await _repository.GetPositionAsync(SyncStreams.UsageEvents), BatchRows);

        var positions = new Dictionary<string, long>();

        if (usage.Count > 0)
        {
            positions[SyncStreams.UsageEvents] = usage[^1].RowId;
        }

        return new SyncPayload
        {
            AppVersion = AppVersion.Display,
            ClientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            UsageEvents = usage.Select(u => u.Row).ToList(),
            Positions = positions
        };
    }
}
