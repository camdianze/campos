using System.Security.Cryptography;
using System.Text;
using PharmaPOS.Application.Security;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// IRecoveryDataProtector의 Windows DPAPI 구현체.
/// DPAPI는 현재 Windows 사용자 계정에 종속된 키로 암호화하므로,
/// 같은 PC의 같은 Windows 계정에서만 복호화가 가능하다 (다른 PC로 DB만 복사해가면 복호화 불가).
/// 이 앱의 배포 특성(PC 1대에 고정 설치)과 잘 맞는 선택이다.
/// </summary>
public class DpapiRecoveryDataProtector : IRecoveryDataProtector
{
    public string Protect(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    public string Unprotect(string encryptedText)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedText);
        var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}