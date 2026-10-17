using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 용지를 뽑을 준비가 끝난 한 건.
/// 용지는 이미 그려져 있고, 인쇄 여부만 남았다.
///
/// 한 건은 "한 거래에서 팔린 하나의 상품"이지 판매 줄 하나가 아니다.
/// 같은 항생제가 배치나 박스/낱개 때문에 여러 줄로 갈라져도 안내문은 한 장이다.
/// </summary>
public class CounsellingCandidate
{
    /// <summary>대표 판매 줄. 파일로 저장할 때 파일 이름에 쓴다.</summary>
    public required string TransactionId { get; init; }

    /// <summary>
    /// 이 안내문 한 장이 대신하는 판매 줄 전부(대표 줄 포함).
    /// 종이는 한 장만 나가지만 로그는 줄마다 남겨야 한다 — 리포트의 항생제 판매 건수와
    /// 인쇄율은 판매 줄 단위로 세므로, 합쳐진 줄을 빼면 지표가 실제보다 낮게 잡힌다.
    /// </summary>
    public required IReadOnlyList<string> TransactionIds { get; init; }

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
