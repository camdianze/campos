using System.Collections.ObjectModel;
using System.Windows;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;
using Lightweight_Digital_Inventory_Management___POS_System.Views;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

/// <summary>
/// 사용자 관리 화면(SCR-USER-016)의 ViewModel.
/// </summary>
public class UserManagementViewModel : ViewModelBase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserManagementService _userManagementService;
    private readonly string _facilityId;
    private readonly string _currentUserId;

    private string _searchTerm = string.Empty;
    private EntityStatus? _selectedStatusFilter;
    private User? _selectedUser;
    private string _message = string.Empty;

    public ObservableCollection<User> Users { get; } = new();

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
            {
                _ = ReloadAsync();
            }
        }
    }

    public EntityStatus? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                _ = ReloadAsync();
            }
        }
    }

    /// <summary>null=All 옵션을 포함하기 위해 nullable 리스트로 구성한다.</summary>
    public IReadOnlyList<EntityStatus?> AvailableStatusFilters { get; } = new EntityStatus?[]
    {
        null, EntityStatus.Active, EntityStatus.Inactive
    };

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsActivateVisible));
                OnPropertyChanged(nameof(IsDeactivateVisible));
            }
        }
    }

    /// <summary>우클릭 메뉴 활성화 조건. 고른 줄이 없으면 할 수 있는 일이 없다.</summary>
    public bool HasSelection => SelectedUser is not null;

    /// <summary>
    /// 활성/비활성 버튼은 한 자리를 나눠 쓴다. 고른 계정의 상태에 따라 둘 중 하나만 보인다 —
    /// 이미 비활성인 계정에 Deactivate를 누르게 두면 무슨 일이 일어나는지 알 수 없다.
    /// 아무것도 고르지 않았을 때는 Deactivate 쪽이 남아 자리가 비지 않는다.
    /// </summary>
    public bool IsActivateVisible => SelectedUser is { Status: EntityStatus.Inactive };

    public bool IsDeactivateVisible => !IsActivateVisible;

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public RelayCommand AddUserCommand { get; }
    public RelayCommand EditRoleCommand { get; }
    public RelayCommand DeactivateCommand { get; }
    public RelayCommand ActivateCommand { get; }
    public RelayCommand ResetPasswordCommand { get; }
    public RelayCommand BackCommand { get; }

    public event Action? RequestAddUserDialog;
    public event Action<User>? RequestResetPasswordDialog;
    public event Action? NavigateBack;

    public UserManagementViewModel(
        IUserRepository userRepository,
        IUserManagementService userManagementService,
        string facilityId,
        string currentUserId)
    {
        _userRepository = userRepository;
        _userManagementService = userManagementService;
        _facilityId = facilityId;
        _currentUserId = currentUserId;

        AddUserCommand = new RelayCommand(_ => RequestAddUserDialog?.Invoke());

        EditRoleCommand = new RelayCommand(async _ => await ExecuteEditRoleAsync());

        DeactivateCommand = new RelayCommand(async _ => await ExecuteDeactivateAsync());

        ActivateCommand = new RelayCommand(async _ => await ExecuteActivateAsync());

        ResetPasswordCommand = new RelayCommand(_ =>
        {
            if (SelectedUser is null)
            {
                Message = "Please select a user.";
                return;
            }

            RequestResetPasswordDialog?.Invoke(SelectedUser);
        });

        BackCommand = new RelayCommand(_ => NavigateBack?.Invoke());

        _ = ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        var results = await _userRepository.SearchUsersAsync(_facilityId, SearchTerm, SelectedStatusFilter);

        Users.Clear();
        foreach (var user in results)
        {
            Users.Add(user);
        }

        Message = results.Count == 0 ? "No users found." : string.Empty;
    }

    private async Task ExecuteEditRoleAsync()
    {
        if (SelectedUser is null)
        {
            Message = "Please select a user.";
            return;
        }

        // 단순 토글: Administrator <-> Facility Staff.
        var newRole = SelectedUser.Role == UserRole.Administrator
            ? UserRole.FacilityStaff
            : UserRole.Administrator;

        if (!AppDialog.Confirm("Confirm", $"Change role of '{SelectedUser.Username}' to {newRole}?"))
        {
            return;
        }

        var result = await _userManagementService.UpdateRoleAsync(SelectedUser.UserId, newRole);

        if (result.IsSuccess)
        {
            await ReloadAsync();
        }
        else
        {
            Message = result.Message!;
        }
    }

    private async Task ExecuteDeactivateAsync()
    {
        if (SelectedUser is null)
        {
            Message = "Please select a user.";
            return;
        }

        var result = await _userManagementService.DeactivateUserAsync(SelectedUser.UserId, _currentUserId);

        if (result.IsSuccess)
        {
            await ReloadAsync();
        }
        else
        {
            Message = result.Message!;
        }
    }

    private async Task ExecuteActivateAsync()
    {
        if (SelectedUser is null)
        {
            Message = "Please select a user.";
            return;
        }

        var result = await _userManagementService.ActivateUserAsync(SelectedUser.UserId);

        if (result.IsSuccess)
        {
            // 상태 필터가 Inactive면 방금 되살린 계정이 목록에서 사라진다. 어디로 갔는지
            // 알 수 없으므로 그 경우에만 한 줄 남긴다.
            var username = SelectedUser.Username;

            await ReloadAsync();

            if (SelectedStatusFilter == EntityStatus.Inactive)
            {
                Message = $"'{username}' is active again.";
            }
        }
        else
        {
            Message = result.Message!;
        }
    }
}