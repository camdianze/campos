using PharmaPOS.Application.Licensing;
using CamPOS.Application.Sync;
using CamPOS.Wpf.ViewModels.Base;

namespace CamPOS.Wpf.ViewModels;

/// <summary>
/// 라이선스 코드 입력 화면의 ViewModel.
/// 다른 화면과 같이 화면 전환은 직접 하지 않고 이벤트만 올린다.
/// </summary>
public class LicenseActivationViewModel : ViewModelBase
{
    private readonly ILicenseService _licenseService;
    private readonly ISyncRegistrationService? _registrationService;

    private string _licenseCode = string.Empty;
    private string _errorMessage = string.Empty;
    private string _title = "Activate CamPOS";
    private string _introduction =
        "Enter the license code that came with your copy. This is a one-time step and works without an internet connection.";

    public string LicenseCode
    {
        get => _licenseCode;
        set
        {
            if (SetProperty(ref _licenseCode, value))
                ErrorMessage = string.Empty;
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>화면 제목. 기한이 지나 다시 열린 경우에는 "연장"이라고 적는다.</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Introduction
    {
        get => _introduction;
        private set => SetProperty(ref _introduction, value);
    }

    public RelayCommand ActivateCommand { get; }

    public event Action? ActivationSucceeded;

    public LicenseActivationViewModel(
        ILicenseService licenseService,
        ISyncRegistrationService? registrationService = null)
    {
        _licenseService = licenseService;
        _registrationService = registrationService;

        ActivateCommand = new RelayCommand(async _ => await ExecuteActivateAsync());
    }

    /// <summary>
    /// 이 화면이 뜬 이유를 문구에 반영한다. 기한이 지나서 열린 것이라면 그 날짜를 적는다 —
    /// 데이터는 그대로 있고 코드만 새로 넣으면 된다는 사실까지 같이 말해야, 약국이
    /// 자료가 날아간 줄 알고 당황하지 않는다.
    /// </summary>
    public void ApplyStatus(LicenseStatus? status)
    {
        if (status is not { IsExpired: true })
        {
            return;
        }

        Title = "Renew CamPOS";
        Introduction =
            $"This license expired on {status.ExpiryDisplay}. Enter a renewal code to continue. "
            + "Your data is untouched and will be there as soon as the new code is accepted.";
    }

    private async Task ExecuteActivateAsync()
    {
        var result = _licenseService.Activate(LicenseCode);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            return;
        }

        // 활성화는 끝났다. 여기서부터는 동기화 설정이고, 실패해도 앱은 열린다 —
        // 그래서 activation 자체는 인터넷 없이도 되는 성질을 그대로 지킨다.
        await RegisterForSyncAsync();

        ActivationSucceeded?.Invoke();
    }

    /// <summary>
    /// 라이선스 코드를 서버에 보내 이 PC의 동기화 토큰을 받아 둔다.
    ///
    /// 받은 토큰은 화면을 거치지 않고 바로 저장된다. 사람이 보지 않으므로 잘못
    /// 붙여넣을 수 없고, 설치할 때 입력하는 것이 라이선스 코드 하나로 줄어든다.
    ///
    /// <b>실패해도 넘어간다.</b> 인터넷이 없는 자리에서 설치하는 경우가 있고,
    /// 그때 동기화가 안 되는 것과 앱이 안 열리는 것은 견줄 수 없다. 나중에
    /// Data Center에서 다시 하거나 토큰을 손으로 넣으면 된다.
    /// </summary>
    private async Task RegisterForSyncAsync()
    {
        if (_registrationService is null)
        {
            return;
        }

        try
        {
            await _registrationService.RegisterAsync(LicenseCode);
        }
        catch
        {
            // 동기화 설정 실패로 활성화를 되돌리지 않는다.
        }
    }
}
