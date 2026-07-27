using System.Text;
using System.Windows;
using PharmaPOS.Application.Inventory;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// IReceiptPrintingService의 임시 구현체.
/// 실제 ESC/POS 58mm 열전사 프린터 기종/프로토콜이 아직 확정되지 않아
/// (F-15 하드웨어 미정, PRD 리스크 R-2 참고), 실제 출력 대신
/// 영수증 내용을 팝업으로 보여주는 것으로 대체한다.
/// TODO: 58mm 프린터 확보 후, 이 클래스를 실제 ESC/POS 라이브러리
/// (예: ESC-POS-.NET) 기반 구현체로 교체한다. 인터페이스는 그대로 유지될 예정이다.
///
/// 이 클래스가 Application이 아니라 Desktop(WPF) 프로젝트에 있는 이유:
/// MessageBox는 WPF(PresentationFramework)에 속한 타입이라,
/// UI 프레임워크를 몰라야 하는 Application 계층에는 둘 수 없다.
/// 인터페이스(IReceiptPrintingService)만 Application에 두고,
/// 구현체는 실제로 UI를 다루는 이 프로젝트에 둔다.
/// </summary>
public class SimulatedReceiptPrintingService : IReceiptPrintingService
{
    public Task<ReceiptPrintResult> PrintReceiptAsync(
        IReadOnlyList<SaleLineItem> cartItems,
        decimal totalAmount,
        decimal? cashTendered,
        decimal? changeDue)
    {
        var receipt = BuildReceiptText(cartItems, totalAmount, cashTendered, changeDue);

        MessageBox.Show(receipt, "Receipt (Simulated Print)");

        return Task.FromResult(ReceiptPrintResult.Success());
    }

    private static string BuildReceiptText(
        IReadOnlyList<SaleLineItem> cartItems,
        decimal totalAmount,
        decimal? cashTendered,
        decimal? changeDue)
    {
        var builder = new StringBuilder();
        builder.AppendLine("===== RECEIPT =====");

        foreach (var item in cartItems)
        {
            builder.AppendLine($"{item.ProductName} x{item.Quantity} @ {item.UnitPrice} = {item.LineTotal}");
        }

        builder.AppendLine("--------------------");
        builder.AppendLine($"Total: {totalAmount}");

        if (cashTendered is not null)
        {
            builder.AppendLine($"Cash Tendered: {cashTendered}");
            builder.AppendLine($"Change Due: {changeDue}");
        }

        builder.AppendLine("====================");

        return builder.ToString();
    }
}