using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using CamPOS.Application.Counselling;
using CamPOS.Application.Repositories;
using CamPOS.Wpf.Shell;
using CamPOS.Wpf.ViewModels;

namespace CamPOS.Wpf.Views;

public partial class AntibioticReportView : UserControl
{
    private AntibioticReportViewModel? _viewModel;

    public AntibioticReportView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ViewModel은 시설 ID와 역할을 생성자로 받으므로 DI가 만들 수 없다.
    /// 다른 화면과 같이 호출부가 만들어 넘긴다.
    /// </summary>
    public void AttachViewModel(AntibioticReportViewModel viewModel)
    {
        _viewModel = viewModel;

        viewModel.NavigateBack += OnNavigateBack;
        viewModel.NavigateToMoreSettings += OnNavigateToMoreSettings;

        DataContext = viewModel;
    }

    /// <summary>
    /// 저장하지 않은 안내지 설정이 있으면 물어본다. 나가도 된다고 하면 true.
    /// 영수증 설정 화면과 같은 방식이다 — 설정을 고치다 화면을 옮기는 일은
    /// 둘 다 흔하고, 조용히 버리면 바꾼 줄 알고 나간다.
    /// </summary>
    private bool ConfirmLeavingUnsavedChanges()
    {
        if (_viewModel is not { HasUnsavedChanges: true })
        {
            return true;
        }

        return AppDialog.Confirm(
            "Unsaved Changes",
            "The counselling sheet settings have been changed but not saved.\n\nLeave without saving?",
            confirmText: "Leave",
            cancelText: "Stay");
    }

    private void OnNavigateBack()
    {
        if (!ConfirmLeavingUnsavedChanges()) return;

        if (Window.GetWindow(this) is MainWindow parentWindow)
        {
            parentWindow.Content = new MainShellView { DataContext = App.CurrentShellViewModel };
        }
    }

    /// <summary>
    /// 인쇄 방식(always/ask)과 출력 대상(프린터/파일)은 기존 설정 화면에 남아 있다.
    /// 이 화면에서 다루는 세 칸과 달리 그 둘은 판매 흐름을 바꾸는 설정이라,
    /// 리포트를 보다 곁눈으로 건드릴 자리에 두지 않았다.
    /// </summary>
    private void OnNavigateToMoreSettings()
    {
        if (!ConfirmLeavingUnsavedChanges()) return;
        if (Window.GetWindow(this) is not MainWindow parentWindow) return;

        var currentUser = App.CurrentShellViewModel!.CurrentUser;

        var viewModel = new CounsellingSettingsViewModel(
            App.Services.GetRequiredService<ICounsellingSettingsService>(),
            App.Services.GetRequiredService<ICounsellingLocaleProvider>(),
            App.Services.GetRequiredService<ICounsellingLogRepository>(),
            currentUser.Role);

        var view = new CounsellingSettingsView();
        view.AttachViewModel(viewModel);

        parentWindow.Content = view;
    }
}
