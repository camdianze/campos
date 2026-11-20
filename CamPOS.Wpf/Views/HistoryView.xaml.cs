using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using CamPOS.Application.Inventory;
using CamPOS.Application.Receipts;
using CamPOS.Application.Reports;
using CamPOS.Application.Usage;
using CamPOS.Domain.Enums;
using CamPOS.Wpf.Shell;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

public partial class HistoryView : UserControl
{
    private readonly string _facilityId;
    private readonly string _userId;
    private readonly UserRole _role;

    public HistoryView(string facilityId, string userId, UserRole role)
    {
        // 역할을 InitializeComponent보다 먼저 넣는다. Analysis 카드의 IsEnabled가
        // 이 인스턴스의 IsAdministrator에 바인딩돼 있어서다 — 바인딩이 실제로 읽히는
        // 시점은 레이아웃 때라 순서가 뒤여도 지금은 동작하지만, 그건 보장이 아니다.
        _facilityId = facilityId;
        _userId     = userId;
        _role       = role;

        InitializeComponent();
    }

    /// <summary>
    /// Analysis 카드를 쓸 수 있는지. XAML에서 직접 바인딩하는 값이라 속성으로 둔다.
    ///
    /// 이 화면에는 ViewModel이 없다 — 카드 세 장과 클릭 핸들러뿐이라 둘 이유가 없었다.
    /// 잠금은 보이는 쪽 처리이고, 매출 조회를 실제로 막는 것은 Analysis 화면에 들어가는
    /// 길이 여기와 관리자 대시보드뿐이라는 사실이다(대시보드 자체가 관리자 전용).
    /// </summary>
    public bool IsAdministrator => _role == UserRole.Administrator;

    /// <summary>화면을 열었다고 적는다. 기다리지 않고, 던지지도 않는다.</summary>
    private static void Opened(string screen) =>
        _ = App.Services.GetRequiredService<IUsageRecorder>()
            .RecordAsync(screen, UsageOutcome.Shown);

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        if (App.CurrentShellViewModel is MainShellViewModel shellVm)
            parentWindow.Content = new MainShellView { DataContext = shellVm };
    }

    private void OnSaleHistoryClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        try
        {
            var salesHistoryService    = App.Services.GetRequiredService<ISalesHistoryService>();
            var receiptPrintingService = App.Services.GetRequiredService<IReceiptPrintingService>();

            var vm = new SalesHistoryViewModel(
                salesHistoryService, receiptPrintingService,
                App.Services.GetRequiredService<IUsageRecorder>(),
                _facilityId, _userId);

            Opened(UsageEventNames.SalesHistory);

            var view = new SalesHistoryView();
            view.AttachViewModel(vm);
            parentWindow.Content = view;
        }
        catch (Exception ex)
        {
            AppDialog.Show("Error", $"Error: {ex.Message}");
        }
    }

    private void OnStockHistoryClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        try
        {
            var stockHistoryService = App.Services.GetRequiredService<IStockHistoryService>();

            var vm = new StockHistoryViewModel(stockHistoryService, _facilityId);
            vm.NavigateBack += () =>
            {
                parentWindow.Content = new HistoryView(_facilityId, _userId, _role);
            };

            Opened(UsageEventNames.StockHistory);

            var view = new StockHistoryView();
            view.AttachViewModel(vm);
            parentWindow.Content = view;
        }
        catch (Exception ex)
        {
            AppDialog.Show("Error", $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// 매출 분석. 항생제 리포트에서 갈라져 나온 쪽이고 여기 있는 이유는 다루는 것이
    /// 과거 기록이기 때문이다 — 판매 이력·재고 이력과 한 자리다.
    ///
    /// 사용 기록은 <c>reports</c>를 그대로 쓴다. 이름을 새로 만들면 동의서를 먼저
    /// 고쳐야 하는데, 지표에서 묻는 것("리포트를 보는가")은 갈라지기 전과 같다.
    /// </summary>
    private void OnAnalysisClick(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this) as MainWindow;
        if (parentWindow is null) return;

        // 잠긴 카드는 눌리지 않지만, 길이 하나 더 생겼을 때를 대비해 여기서도 막는다.
        if (!IsAdministrator) return;

        try
        {
            var vm = new SalesAnalysisViewModel(
                App.Services.GetRequiredService<IReportService>(),
                App.Services.GetRequiredService<IReceiptSettingsService>(),
                _facilityId);

            Opened(UsageEventNames.Reports);

            var view = new SalesAnalysisView();
            view.AttachViewModel(
                vm,
                onBack: () => parentWindow.Content = new HistoryView(_facilityId, _userId, _role));

            parentWindow.Content = view;
        }
        catch (Exception ex)
        {
            AppDialog.Show("Error", $"Error: {ex.Message}");
        }
    }
}
