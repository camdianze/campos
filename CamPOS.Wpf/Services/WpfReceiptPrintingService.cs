using PharmaPOS.Application.Counselling;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Receipts;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 판매 영수증을 Windows 인쇄 파이프라인으로 출력한다.
///
/// 이전 구현(SimulatedReceiptPrintingService)은 팝업으로 내용만 보여 주고 성공을 돌려줬다.
/// 프린터 기종이 미정이라 ESC/POS를 짤 수 없다는 이유였는데, 복약안내 용지가 이미
/// 드라이버를 거쳐 인쇄되고 있었으므로 같은 경로를 쓰면 될 일이었다.
///
/// 이 클래스가 하는 일은 "재료를 모아 렌더러에 넘기고 종이로 보내는 것"뿐이다.
/// 무엇을 어떻게 그릴지는 ReceiptRenderer가, 어떤 문구를 쓸지는 receipt.* 리소스가 정한다.
/// 그래야 설정 화면의 미리보기와 실제 인쇄물이 같은 그림을 낸다.
///
/// 실패는 판매를 막지 않는다. 이 메서드가 불릴 때 거래는 이미 커밋돼 있고,
/// 종이가 안 나온 것과 판매가 안 된 것은 다른 이야기다.
/// </summary>
public class WpfReceiptPrintingService : IReceiptPrintingService
{
    /// <summary>
    /// 크메르어 문구는 로케일 파일에서 온다. 파일 이름이 고정인 이유는
    /// print.lang이 km / km_en 두 값으로 크메르어를 지목하기 때문이다
    /// (복약안내의 counselling.locale과 달리 언어를 자유롭게 고르는 설정이 아니다).
    /// </summary>
    private const string KhmerLocaleCode = "km-KH";

    private readonly IReceiptSettingsService _settingsService;
    private readonly IReceiptNumberService _numberService;
    private readonly ICounsellingLocaleProvider _localeProvider;

    public WpfReceiptPrintingService(
        IReceiptSettingsService settingsService,
        IReceiptNumberService numberService,
        ICounsellingLocaleProvider localeProvider)
    {
        _settingsService = settingsService;
        _numberService = numberService;
        _localeProvider = localeProvider;
    }

    public async Task<ReceiptPrintResult> PrintReceiptAsync(ReceiptPrintRequest request)
    {
        var settings = await _settingsService.GetAsync();
        var text = new ReceiptText(settings.PrintLanguage, await LoadLocaleAsync(settings));

        var saleKey = ReceiptSaleKey.For(request.TransactionTime, request.UserId);

        // 번호 표기를 꺼 두었으면 발번 자체를 하지 않는다. 쓰지도 않을 번호로
        // 일련번호를 올리면 나중에 표기를 켰을 때 번호가 건너뛴 것처럼 보인다.
        var receiptNumber = settings.ShowReceiptNumber
            ? await _numberService.IssueAsync(saleKey, settings, request.TransactionTime)
            : null;

        var document = ReceiptRenderer.Render(new ReceiptRenderRequest
        {
            Settings = settings,
            Text = text,
            Lines = request.Lines,
            TotalAmount = request.TotalAmount,
            CashTendered = request.CashTendered,
            ChangeDue = request.ChangeDue,
            TransactionTime = request.TransactionTime,
            ReceiptNumber = receiptNumber,
            StaffName = request.Username,
            PaymentMethod = request.PaymentMethod
        });

        // 크메르어가 들어간 영수증은 줄 간격을 넓힌다. 기본 간격으로 찍으면
        // 위아래로 붙는 모음·발음 기호가 앞뒤 줄에 닿아 잘려 보인다.
        var printed = ThermalTextPrinter.TryPrint(
            document.Lines,
            "Sales receipt",
            document.ContainsKhmer
                ? ThermalTextPrinter.ComplexScriptLineHeightFactor
                : ThermalTextPrinter.DefaultLineHeightFactor);

        return printed
            ? ReceiptPrintResult.Success()
            : ReceiptPrintResult.Failure("The receipt could not be printed.");
    }

    private async Task<CounsellingLocale> LoadLocaleAsync(ReceiptSettings settings)
    {
        if (settings.PrintLanguage == ReceiptPrintLanguage.English)
        {
            return CounsellingLocale.EnglishOnly;
        }

        return await _localeProvider.GetLocaleAsync(KhmerLocaleCode);
    }
}
