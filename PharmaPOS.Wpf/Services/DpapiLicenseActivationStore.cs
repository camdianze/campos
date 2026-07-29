using System.IO;
using System.Security.Cryptography;
using System.Text;
using PharmaPOS.Application.Licensing;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 활성화 기록을 %APPDATA%\PharmaPOS\license.dat에 DPAPI로 암호화해 남긴다.
///
/// 여기서는 DPAPI가 맞는 선택이다. 이 PC에서 쓰고 이 PC에서 읽기 때문이다.
/// 덕분에 license.dat만 다른 PC로 복사해도 그쪽에서는 복호화되지 않아
/// 활성화 상태를 그대로 옮길 수 없다.
///
/// 반대로 "정답 코드"를 DPAPI로 암호화해 exe에 넣는 것은 불가능하다.
/// 개발 PC 계정 키로 암호화한 값이라 고객 PC에서는 복호화가 실패한다.
/// 그래서 코드 검증은 LicenseService의 내장 해시 비교로 처리한다.
/// </summary>
public class DpapiLicenseActivationStore : ILicenseActivationStore
{
    private readonly string _licenseFilePath;

    public DpapiLicenseActivationStore(string licenseFilePath)
    {
        _licenseFilePath = licenseFilePath;
    }

    public bool IsActivated()
    {
        if (!File.Exists(_licenseFilePath))
            return false;

        try
        {
            var encryptedBytes = File.ReadAllBytes(_licenseFilePath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes).Length > 0;
        }
        catch (Exception)
        {
            // 파일이 깨졌거나, 다른 PC/계정에서 복사해 온 파일이라 복호화가 안 되는 경우.
            // 활성화되지 않은 것으로 보고 코드를 다시 묻는다.
            return false;
        }
    }

    public void SaveActivation(string licenseCode)
    {
        var directory = Path.GetDirectoryName(_licenseFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // 언제 어떤 코드로 활성화했는지 남겨두면 나중에 문의가 왔을 때 확인할 수 있다.
        var record = $"{licenseCode}|{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        var plainBytes = Encoding.UTF8.GetBytes(record);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(_licenseFilePath, encryptedBytes);
    }
}
