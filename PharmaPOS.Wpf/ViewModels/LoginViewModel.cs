using System.IO;
using PharmaPOS.Application.Authentication;
using PharmaPOS.Domain.Entities;
using Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private static readonly string SavedUsernamePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PharmaPOS", "remember_me.txt");

    private string _username = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _rememberMe;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
                LoginCommand.RaiseCanExecuteChanged();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public RelayCommand LoginCommand { get; }
    public RelayCommand ForgotPasswordCommand { get; }
    public RelayCommand ForgotUsernameCommand { get; }

    public event Action<User>? LoginSucceeded;
    public event Action? ForgotPasswordRequested;
    public event Action? ForgotUsernameRequested;

    public LoginViewModel(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
        LoginCommand = new RelayCommand(ExecuteLogin, CanExecuteLogin);
        ForgotPasswordCommand = new RelayCommand(_ => ForgotPasswordRequested?.Invoke());
        ForgotUsernameCommand = new RelayCommand(_ => ForgotUsernameRequested?.Invoke());

        LoadSavedUsername();
    }

    private void LoadSavedUsername()
    {
        try
        {
            if (File.Exists(SavedUsernamePath))
            {
                var saved = File.ReadAllText(SavedUsernamePath).Trim();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    Username = saved;
                    RememberMe = true;
                }
            }
        }
        catch { }
    }

    private void SaveOrClearUsername()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavedUsernamePath)!);

            if (RememberMe)
                File.WriteAllText(SavedUsernamePath, Username);
            else if (File.Exists(SavedUsernamePath))
                File.Delete(SavedUsernamePath);
        }
        catch { }
    }

    private bool CanExecuteLogin(object? parameter) => !IsBusy;

    private async void ExecuteLogin(object? parameter)
    {
        if (parameter is not System.Windows.Controls.PasswordBox passwordBox)
        {
            ErrorMessage = "Internal error: password box not found.";
            return;
        }

        var password = passwordBox.Password;

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Please enter your username.";
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Please enter your password.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _authenticationService.LoginAsync(Username, password);

            if (result.IsSuccess)
            {
                SaveOrClearUsername();
                LoginSucceeded?.Invoke(result.AuthenticatedUser!);
            }
            else
            {
                ErrorMessage = MapErrorToMessage(result.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string MapErrorToMessage(AuthenticationError error) => error switch
    {
        AuthenticationError.UsernameEmpty => "Please enter your username.",
        AuthenticationError.PasswordEmpty => "Please enter your password.",
        AuthenticationError.InvalidCredentials => "Invalid username or password.",
        AuthenticationError.AccountInactive => "This account is inactive. Please contact the administrator.",
        AuthenticationError.FacilityInactive => "This facility is inactive. Please contact the administrator.",
        _ => "An unexpected error occurred."
    };
}