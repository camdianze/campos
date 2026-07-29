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

    public RelayCommand ActivateCommand { get; }

    public event Action? ActivationSucceeded;

    public LicenseActivationViewModel(ILicenseService licenseService)
    {
        _licenseService = licenseService;

        ActivateCommand = new RelayCommand(_ => ExecuteActivate());
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
