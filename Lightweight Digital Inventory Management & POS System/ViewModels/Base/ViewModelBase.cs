using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lightweight_Digital_Inventory_Management___POS_System.ViewModels.Base;

/// <summary>
/// 모든 ViewModel의 공통 부모 클래스.
/// 속성 값이 바뀌었을 때 화면(View)에 자동으로 알려주는 기능(INotifyPropertyChanged)을 구현한다.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 필드 값을 변경하고, 값이 실제로 바뀌었을 때만 PropertyChanged 이벤트를 발생시킨다.
    /// </summary>
    /// <param name="field">백킹 필드(실제 값이 저장된 변수)에 대한 참조</param>
    /// <param name="value">새로 설정할 값</param>
    /// <param name="propertyName">속성 이름 (호출부에서 생략하면 자동으로 채워짐)</param>
    /// <returns>값이 실제로 바뀌었으면 true</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}