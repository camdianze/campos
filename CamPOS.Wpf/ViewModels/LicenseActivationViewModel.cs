using PharmaPOS.Application.Licensing;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 라이선스 코드 입력 화면의 ViewModel.
/// 다른 화면과 같이 화면 전환은 직접 하지 않고 이벤트만 올린다.
/// </summary>
public class LicenseActivationViewModel : ViewModelBase
{
    private readonly ILicenseService _licenseService;

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

    public LicenseActivationViewModel(ILicenseService licenseService)
    {
        _licenseService = licenseService;

        ActivateCommand = new RelayCommand(_ => ExecuteActivate());
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

    private void ExecuteActivate()
    {
        var result = _licenseService.Activate(LicenseCode);

        if (result.IsSuccess)
        {
            ActivationSucceeded?.Invoke();
            return;
        }

        ErrorMessage = result.Message;
    }
}
