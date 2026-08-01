using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 용지를 뽑을 준비가 끝난 한 건.
/// 용지는 이미 그려져 있고, 인쇄 여부만 남았다.
/// </summary>
public class CounsellingCandidate
{
    public required string TransactionId { get; init; }

    public required string ProductId { get; init; }

    public required string ProductName { get; init; }

    public string? AtcCode { get; init; }

    public required AwareGroup AwareGroup { get; init; }

    public string? SourceVersion { get; init; }

    /// <summary>로그에 남길 로케일. 영어 단독이면 "en".</summary>
    public required string LocaleCode { get; init; }

    public required CounsellingSheetDocument Document { get; init; }

    /// <summary>프린터로 보낼지 파일로 저장할지. 준비 시점의 설정을 그대로 들고 간다.</summary>
    public CounsellingOutput Output { get; init; } = CounsellingOutput.Printer;

    /// <summary>Output이 File일 때 저장 폴더.</summary>
    public string? FileOutputFolder { get; init; }

    /// <summary>true면 인쇄 전에 약사에게 물어야 한다 (설정이 ask인 경우).</summary>
    public required bool RequiresPrompt { get; init; }
}
