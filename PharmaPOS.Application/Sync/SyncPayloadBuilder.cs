using PharmaPOS.Application.Repositories;

namespace PharmaPOS.Application.Sync;

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

    public async Task<SyncPayload> BuildAsync(string facilityId)
    {
        var transactionFrom = await _repository.GetPositionAsync(SyncStreams.Transactions);
        var counsellingFrom = await _repository.GetPositionAsync(SyncStreams.CounsellingLogs);

        var transactions = await _repository.GetTransactionsAfterAsync(
            facilityId, transactionFrom, BatchRows);

        var counselling = await _repository.GetCounsellingLogsAfterAsync(
            counsellingFrom, BatchRows);

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

        return new SyncPayload
        {
            AppVersion = AppVersion.Display,
            ClientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Products = await _repository.GetProductsAsync(),
            Inventory = await _repository.GetInventoryAsync(facilityId),
            Users = await _repository.GetUsersAsync(facilityId),
            Transactions = transactions.Select(t => t.Row).ToList(),
            CounsellingLogs = counselling.Select(c => c.Row).ToList(),
            Positions = positions
        };
    }
}
