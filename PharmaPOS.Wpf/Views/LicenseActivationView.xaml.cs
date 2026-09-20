using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels;
using PharmaPOS.Application.Licensing;

namespace Lightweight_Digital_Inventory_Management___POS_System.Views;

/// <summary>
/// 첫 실행 시 라이선스 코드를 받는 화면.
/// 활성화에 성공하면 ActivationSucceeded를 올리고, 다음 화면 결정은 App이 한다.
/// </summary>
public partial class LicenseActivationView : UserControl
{
    /// <summary>활성화 성공. 구독자가 다음 화면으로 바꿔준다.</summary>
    public event Action? ActivationSucceeded;

    /// <param name="status">
    /// 이 화면이 뜬 이유. 기한이 지나서 다시 열린 경우에는 그 사실을 화면에 적어야 한다 —
    /// 어제까지 쓰던 앱이 코드를 묻는데 이유가 없으면 고장으로 읽힌다.
    /// </param>
    public LicenseActivationView(LicenseStatus? status = null)
    {
        InitializeComponent();

        var viewModel = App.Services.GetRequiredService<LicenseActivationViewModel>();
        viewModel.ActivationSucceeded += () => ActivationSucceeded?.Invoke();
        viewModel.ApplyStatus(status);

        DataContext = viewModel;

        Loaded += (_, _) => LicenseCodeInput.Focus();
    }

    /// <summary>
    /// 인터넷도 메일도 없는 현장에서 설치하는 경우를 위한 통로.
    /// 코드가 124자라 손으로 치기 어려우므로 USB에 담아 온 텍스트 파일에서 읽어 넣는다.
    /// </summary>
    private void OnLoadFromFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select license file",
            Filter = "License file (*.txt;*.lic)|*.txt;*.lic|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        if (DataContext is not LicenseActivationViewModel viewModel)
            return;

        try
        {
            // 코드에 줄바꿈이나 공백이 섞여 있어도 디코더가 무시하므로 그대로 넣는다.
            viewModel.LicenseCode = File.ReadAllText(dialog.FileName).Trim();
        }
        catch (Exception)
        {
            viewModel.ErrorMessage = "The selected file could not be read.";
        }
    }
}
