using PharmaPOS.Application.Inventory;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 판매 확정 이후의 복약안내 흐름을 담당한다.
///
/// 이 인터페이스의 어떤 메서드도 예외를 던지지 않는다.
/// 판매는 이미 확정된 상태이고, 안내지 때문에 거래가 흔들려서는 안 된다.
/// </summary>
public interface ICounsellingService
{
    /// <summary>
    /// 확정된 판매 줄들을 훑어 항생제를 가려낸다.
    ///
    /// 매칭 실패(unmatched)와 설정상 인쇄 안 함(never)은 여기서 바로 로그만 남기고
    /// 결과에 포함하지 않는다. 국소 제제는 안내 대상이 아니므로 로그도 남기지 않는다.
    /// 돌려주는 것은 "인쇄해야 하거나, 인쇄할지 물어봐야 하는" 건들뿐이다.
    /// </summary>
    Task<IReadOnlyList<CounsellingCandidate>> PrepareAsync(IReadOnlyList<ConfirmedSaleLine> confirmedLines);

    /// <summary>용지를 인쇄하고 결과를 로그에 남긴다.</summary>
    Task<CounsellingPrintResult> PrintAsync(CounsellingCandidate candidate);

    /// <summary>약사가 건너뛰었을 때 사유와 함께 로그만 남긴다.</summary>
    Task LogSkipAsync(CounsellingCandidate candidate, string skipReason);
}
