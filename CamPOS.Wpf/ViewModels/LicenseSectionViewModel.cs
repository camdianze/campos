using System.Globalization;
using PharmaPOS.Application.Licensing;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 관리자 대시보드의 라이선스 구역.
///
/// 기한이 지나면 앱이 아예 열리지 않는데, 그때는 계산대 앞에서 영업이 멈춘 상태다.
/// 그 전에 연장할 수 있는 자리가 화면 어딘가에 있어야 하고, 그것이 여기다.
/// 만료 화면에서만 코드를 받을 수 있게 두면 "잠긴 뒤에야 고칠 수 있는" 기능이 된다.
///
/// 영수증 설정과 같은 이유로 대시보드 ViewModel과 분리한다 — 지표 화면과 설정 화면이
/// 한 클래스에 섞이지 않게.
/// </summary>
public class LicenseSectionViewModel : ViewModelBase
{
    private readonly ILicenseService _licenseService;

    private LicenseStatus _status;
    private string _licenseCode = string.Empty;
    private string _message = string.Empty;
    private bool _isError;

    public LicenseSectionViewModel(ILicenseService licenseService)
    {
        _licenseService = licenseService;
        _status = licenseService.GetStatus();

        ActivateCommand = new RelayCommand(_ => ExecuteActivate());
    }

    public string LicenseCode
    {
        get => _licenseCode;
        set
        {
            if (SetProperty(ref _licenseCode, value))
            {
                Message = string.Empty;
            }
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>실패 메시지는 붉게, 성공 메시지는 그렇지 않게 보여주기 위한 값.</summary>
    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public string SerialDisplay => _status.SerialDisplay;

    public string ExpiryDisplay => _status.ExpiryDisplay;

    public string StatusSummary => _status.Summary;

    /// <summary>만료가 가깝거나 이미 지났으면 구역 머리에 눈에 띄게 적는다.</summary>
    public bool ShouldWarn => _status.ShouldWarn || _status.IsExpired;

    /// <summary>
    /// 접힌 상태에서도 보이는 한 줄. 펴 보지 않아도 기한을 알 수 있어야
    /// 이 구역이 제 일을 한다.
    /// </summary>
    public string HeaderSummary => _status.Summary;

    public string DaysRemainingDisplay => _status.DaysRemaining switch
    {
        null => _status.IsPerpetual ? "—" : "—",
        { } days when days < 0 => $"{-days} days ago",
        { } days => days.ToString("N0", CultureInfo.InvariantCulture)
    };

    public RelayCommand ActivateCommand { get; }

    private void ExecuteActivate()
    {
        var result = _licenseService.Activate(LicenseCode);

        if (!result.IsSuccess)
        {
            IsError = true;
            Message = result.Message;
            return;
        }

        // 새 코드가 들어갔으니 화면의 날짜도 그것을 따라야 한다. 저장된 코드를 다시
        // 읽어 만든다 — 방금 넣은 코드의 내용을 여기서 또 해석하면 규칙이 두 벌이 된다.
        _status = _licenseService.GetStatus();
        LicenseCode = string.Empty;

        IsError = false;
        Message = $"License updated. {_status.Summary}.";

        OnPropertyChanged(nameof(SerialDisplay));
        OnPropertyChanged(nameof(ExpiryDisplay));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(HeaderSummary));
        OnPropertyChanged(nameof(DaysRemainingDisplay));
        OnPropertyChanged(nameof(ShouldWarn));
    }
}
