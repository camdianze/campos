using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Authentication;

/// <summary>
/// F-02 초기 시설 설정 로직을 담당하는 인터페이스.
/// </summary>
public interface IInitialSetupService
{
    Task<InitialSetupResult> CompleteSetupAsync(
        string facilityName,
        string country,
        string district,
        FacilityType facilityType,
        string adminUsername,
        string adminPassword,
        string confirmAdminPassword,
        string securityQuestion,
        string securityAnswer);
}