using CamPOS.Application.Counselling;
using CamPOS.Application.Settings;
using CamPOS.Domain.Enums;
using CamPOS.Tests.Receipts;

namespace CamPOS.Tests.Counselling;

/// <summary>
/// 안내지 설정 저장은 관리자만 할 수 있다.
///
/// 거부가 <b>Application 계층</b>에 있는 것이 요점이다. 이 설정을 고칠 수 있는 화면이
/// 둘이 됐다(기존 Counselling 설정 화면, 그리고 항생제 리포트의 오른쪽 패널). 뒤쪽은
/// 직원도 여는 화면이다. 화면에서 단추를 숨기는 식으로 막으면 화면을 하나 더 만들 때마다
/// 같은 숨김을 되풀이해야 하고, 한 번 빼먹으면 시설 전체에 적용되는 설정이 누구에게나
/// 열린다 — 그리고 그 사실은 아무 화면에도 나타나지 않는다.
///
/// 실패 방식도 확인한다. 거부는 <b>예외가 아니라 결과</b>이고, 화면은 그 문구를 그대로
/// 띄운다. 전에는 저장이 예외만 안 나면 화면이 "Settings saved."를 자기 판단으로 적었다.
/// </summary>
public class CounsellingSettingsPermissionTests
{
    private static (CounsellingSettingsService Service, FakeAppSettingRepository Settings) Build()
    {
        var settings = new FakeAppSettingRepository();

        return (
            new CounsellingSettingsService(settings, new FakeAwareClassificationRepository()),
            settings);
    }

    private static CounsellingSettings Changed() => new()
    {
        PrintMode = CounsellingPrintMode.Ask,
        SheetFormat = CounsellingSheetFormat.Compact,
        LocaleCode = "km-KH",
        QrUrl = "https://example.org/amr"
    };

    [Fact]
    public async Task AnAdministratorCanSave()
    {
        var (service, settings) = Build();

        var result = await service.SaveAsync(Changed(), UserRole.Administrator);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            CounsellingSheetFormat.Compact.ToString(),
            await settings.GetAsync(AppSettingKeys.CounsellingSheetFormat));
        Assert.Equal("km-KH", await settings.GetAsync(AppSettingKeys.CounsellingLocale));
    }

    /// <summary>
    /// 직원이 저장을 시도하면 거부되고, <b>아무 키도 쓰이지 않는다.</b> 일부만 쓰이고
    /// 거부되면 설정이 반쯤 바뀐 채로 남는데, 그게 가장 알아차리기 어려운 상태다.
    /// </summary>
    [Fact]
    public async Task StaffCannotSaveAndNothingIsWritten()
    {
        var (service, settings) = Build();

        var result = await service.SaveAsync(Changed(), UserRole.FacilityStaff);

        Assert.False(result.IsSuccess);
        Assert.Empty(settings.Audit);
        Assert.Null(await settings.GetAsync(AppSettingKeys.CounsellingSheetFormat));
    }

    /// <summary>
    /// 거부 문구는 무엇을 하면 되는지까지 말한다. "권한이 없습니다"에서 끝나면
    /// 읽은 사람이 할 수 있는 일이 없고, 설정이 왜 안 바뀌는지 묻게 된다.
    /// </summary>
    [Fact]
    public async Task TheRefusalSaysWhoCanDoIt()
    {
        var (service, _) = Build();

        var result = await service.SaveAsync(Changed(), UserRole.FacilityStaff);

        Assert.Contains("administrator", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("owner", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 성공해도 문구가 비어 있지 않다. 화면이 결과 문구를 그대로 띄우므로,
    /// 비어 있으면 저장을 눌러도 아무 반응이 없는 화면이 된다.
    /// </summary>
    [Fact]
    public async Task ASuccessfulSaveStillCarriesAMessage()
    {
        var (service, _) = Build();

        var result = await service.SaveAsync(Changed(), UserRole.Administrator);

        Assert.NotEmpty(result.Message);
    }

    /// <summary>
    /// 저장이 실패하면 결과로 알린다 — 예외는 호출부로 나가지 않는다. 설정 저장 하나
    /// 때문에 예외가 올라오면 리포트를 보던 화면까지 같이 죽는다.
    /// </summary>
    [Fact]
    public async Task AStorageFailureBecomesAResultNotAnException()
    {
        var (service, settings) = Build();
        settings.FailWrites = true;

        var result = await service.SaveAsync(Changed(), UserRole.Administrator);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Message);
    }

    /// <summary>
    /// 저장 실패와 권한 거부는 다른 문구여야 한다. 같으면 약국이 프로그램 고장과
    /// 권한 문제를 가릴 수 없고, 둘의 대처가 전혀 다르다.
    /// </summary>
    [Fact]
    public async Task AFailureAndARefusalDoNotReadTheSame()
    {
        var (failing, settings) = Build();
        settings.FailWrites = true;

        var (allowed, _) = Build();

        var failure = await failing.SaveAsync(Changed(), UserRole.Administrator);
        var refusal = await allowed.SaveAsync(Changed(), UserRole.FacilityStaff);

        Assert.NotEqual(failure.Message, refusal.Message);
    }
}
