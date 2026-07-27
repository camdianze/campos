using PharmaPOS.Application.Authentication;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 초기 시설 설정 화면(SCR-SETUP-003)의 ViewModel.
/// 2단계 마법사 구조: 1단계(시설 정보), 2단계(관리자 계정).
/// </summary>
public class InitialSetupViewModel : ViewModelBase
{
    private readonly IInitialSetupService _initialSetupService;

    private string _facilityName = string.Empty;
    private string _country = string.Empty;
    private string _district = string.Empty;
    private FacilityType _selectedFacilityType = FacilityType.Pharmacy;

    private string _adminUsername = string.Empty;

    private int _currentStep = 1;
    private string _message = string.Empty;
    private System.Windows.Media.Brush _messageColor = System.Windows.Media.Brushes.Red;

    public string FacilityName
    {
        get => _facilityName;
        set => SetProperty(ref _facilityName, value);
    }

    public string Country
    {
        get => _country;
        set => SetProperty(ref _country, value);
    }

    public string District
    {
        get => _district;
        set => SetProperty(ref _district, value);
    }

    public FacilityType SelectedFacilityType
    {
        get => _selectedFacilityType;
        set => SetProperty(ref _selectedFacilityType, value);
    }

    /// <summary>
    /// ComboBox의 선택 목록. FacilityType enum의 모든 값을 그대로 노출한다.
    /// </summary>
    public IReadOnlyList<FacilityType> AvailableFacilityTypes { get; } =
        Enum.GetValues<FacilityType>();

    public string AdminUsername
    {
        get => _adminUsername;
        set => SetProperty(ref _adminUsername, value);
    }

    private string _selectedSecurityQuestion = string.Empty;

    public string SelectedSecurityQuestion { get => _selectedSecurityQuestion; set => SetProperty(ref _selectedSecurityQuestion, value); }

    public IReadOnlyList<string> AvailableSecurityQuestions { get; } = new[]
    {
        "What was the name of your first pet?",
        "What is your mother's maiden name?",
        "What was the name of your first school?",
        "What city were you born in?"
    };

    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsStep1));
                OnPropertyChanged(nameof(IsStep2));
            }
        }
    }

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public System.Windows.Media.Brush MessageColor
    {
        get => _messageColor;
        set => SetProperty(ref _messageColor, value);
    }

    public RelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }

    /// <summary>
    /// 초기 설정 완료 시 발생. View(code-behind)가 구독해서 로그인 화면으로 전환한다.
    /// </summary>
    public event Action? SetupCompleted;

    public InitialSetupViewModel(IInitialSetupService initialSetupService)
    {
        _initialSetupService = initialSetupService;

        NextCommand = new RelayCommand(_ => ExecuteNext());
        BackCommand = new RelayCommand(_ =>
        {
            Message = string.Empty;
            CurrentStep = 1;
        });
    }

    private void ExecuteNext()
    {
        // Screen SCR-SETUP-003, 4.3절: 1단계 필수값 검증.
        // 최종 검증은 Complete 시 서비스에서 다시 하지만,
        // 1단계에서 미리 걸러주면 사용자가 2단계까지 갔다가 되돌아오는 걸 방지할 수 있다.
        if (string.IsNullOrWhiteSpace(FacilityName))
        {
            Message = "Please enter the facility name.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Country))
        {
            Message = "Please enter the country.";
            return;
        }

        if (string.IsNullOrWhiteSpace(District))
        {
            Message = "Please enter the province or district.";
            return;
        }

        Message = string.Empty;
        CurrentStep = 2;
    }

    /// <summary>
    /// code-behind(View)가 Complete 버튼 클릭 시 2개의 PasswordBox 값을 모아 호출한다.
    /// </summary>
    public async void CompleteSetup(string adminPassword, string confirmAdminPassword, string securityAnswer)
    {
        Message = string.Empty;

        var result = await _initialSetupService.CompleteSetupAsync(
            FacilityName, Country, District, SelectedFacilityType,
            AdminUsername, adminPassword, confirmAdminPassword,
            SelectedSecurityQuestion, securityAnswer);

        if (result.IsSuccess)
        {
            MessageColor = System.Windows.Media.Brushes.Green;
            Message = "Initial setup completed successfully.";
            SetupCompleted?.Invoke();
        }
        else
        {
            MessageColor = System.Windows.Media.Brushes.Red;
            Message = result.ErrorMessage!;
        }
    }
}