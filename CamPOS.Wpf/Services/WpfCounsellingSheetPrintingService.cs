using PharmaPOS.Application.Counselling;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 복약안내 용지를 Windows 인쇄 파이프라인으로 출력한다.
/// 종이에 찍는 일 자체는 ThermalTextPrinter가 하고, 여기서는 무엇을 찍을지만 정한다
/// (판매 영수증도 같은 경로를 쓴다).
/// </summary>
public class WpfCounsellingSheetPrintingService : ICounsellingSheetPrintingService
{
    public Task<CounsellingPrintResult> PrintAsync(CounsellingSheetDocument document)
    {
        var printed = ThermalTextPrinter.TryPrint(
            BuildPrintableLines(document),
            $"Antibiotic counselling - {document.ProductName}");

        // 실패 사유를 프린터 없음/드라이버 오류로 나누지 않는다. 약사가 할 일은 어느 쪽이든 같고,
        // 판매는 이미 확정되어 있어 되돌릴 것도 없다.
        return Task.FromResult(printed
            ? CounsellingPrintResult.Success()
            : CounsellingPrintResult.Failure("The counselling sheet could not be printed."));
    }

    /// <summary>
    /// QR 이미지 대신 주소를 글자로 덧붙인다.
    /// QR 인코더를 넣으려면 외부 패키지가 필요한데, 아직 QR이 가리킬 주소가
    /// 정해지지 않아 의존성부터 늘리지 않았다. 주소가 설정돼 있으면
    /// 적어도 사람이 옮겨 칠 수는 있게 해 둔다.
    /// </summary>
    private static List<string> BuildPrintableLines(CounsellingSheetDocument document)
    {
        var lines = document.Lines.ToList();

        if (string.IsNullOrWhiteSpace(document.QrUrl))
        {
            return lines;
        }

        var markerIndex = lines.FindIndex(l => l.StartsWith("[QR]", StringComparison.Ordinal));

        if (markerIndex >= 0)
        {
            lines.Insert(markerIndex + 1, "     " + document.QrUrl);
        }

        return lines;
    }
}
