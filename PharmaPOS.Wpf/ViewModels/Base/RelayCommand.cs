using System.Windows.Input;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

/// <summary>
/// XAML의 Button.Command 등에 바인딩할 수 있는 ICommand 구현체.
/// 버튼 클릭 시 실행할 동작(execute)과, 버튼 활성화 여부를 결정하는 조건(canExecute)을 받는다.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// 버튼 등의 활성화 상태를 다시 계산하도록 강제로 요청한다.
    /// 예: 로그인 처리 중에는 버튼을 비활성화했다가, 끝나면 다시 활성화할 때 사용.
    /// </summary>
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}