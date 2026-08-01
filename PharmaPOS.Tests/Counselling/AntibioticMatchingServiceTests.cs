using PharmaPOS.Application.Counselling;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Counselling;

public class AntibioticMatchingServiceTests
{
    /// <summary>수용 기준의 시드 상황을 그대로 옮긴 참조 데이터.</summary>
    private static FakeAwareClassificationRepository BuildRepository()
    {
        return new FakeAwareClassificationRepository()
            .Add("J01CA04", "Amoxicillin", AwareGroup.Access)
            .Add("J01FA10", "Azithromycin", AwareGroup.Watch)
            .Add("J01XX09", "Daptomycin", AwareGroup.Reserve)
            .Add("J01DB01", "Cefalexin", AwareGroup.Access)
            // 고유 ATC가 없는 고정용량복합제
            .Add(null, "Amoxicillin/clavulanic acid FDC", AwareGroup.NotRecommended)
            // 국소 제제
            .Add("D06AX04", "Neomycin", AwareGroup.Access, isSystemic: false)
            // J01 계열이 아닌 전신 항생제
            .Add("A07AA11", "Rifaximin", AwareGroup.Watch);
    }

    private static AntibioticMatchingService BuildService(FakeAwareClassificationRepository? repository = null)
        => new(repository ?? BuildRepository());

    [Fact]
    public async Task MatchAsync_FindsAccessAgentByAtcCode()
    {
        var match = await BuildService().MatchAsync("J01CA04", null);

        Assert.Equal(AntibioticMatchOutcome.Matched, match.Outcome);
        Assert.Equal(AwareGroup.Access, match.Classification!.AwareGroup);
    }

    [Fact]
    public async Task MatchAsync_FindsWatchAgentByAtcCode()
    {
        var match = await BuildService().MatchAsync("J01FA10", null);

        Assert.Equal(AwareGroup.Watch, match.Classification!.AwareGroup);
    }

    /// <summary>
    /// 수용 기준: 고정용량복합제가 NOT_RECOMMENDED로 잡혀야 한다.
    /// ATC 코드가 없으므로 성분명 경로로만 찾을 수 있다.
    /// </summary>
    [Fact]
    public async Task MatchAsync_FindsFixedDoseCombinationWithoutAtcCode()
    {
        var match = await BuildService().MatchAsync(null, "Amoxicillin/clavulanic acid FDC");

        Assert.Equal(AntibioticMatchOutcome.Matched, match.Outcome);
        Assert.Equal(AwareGroup.NotRecommended, match.Classification!.AwareGroup);
    }

    /// <summary>수용 기준: 염 형태가 붙어 있어도 매칭돼야 한다.</summary>
    [Fact]
    public async Task MatchAsync_MatchesSaltFormByGenericName()
    {
        var match = await BuildService().MatchAsync(null, "Azithromycin dihydrate");

        Assert.Equal(AwareGroup.Watch, match.Classification!.AwareGroup);
    }

    /// <summary>수용 기준: Cephalexin / Cefalexin 양쪽 다 매칭돼야 한다.</summary>
    [Theory]
    [InlineData("Cefalexin")]
    [InlineData("Cephalexin")]
    public async Task MatchAsync_MatchesBothCefAndCephSpellings(string genericName)
    {
        var match = await BuildService().MatchAsync(null, genericName);

        Assert.Equal(AntibioticMatchOutcome.Matched, match.Outcome);
        Assert.Equal("Cefalexin", match.Classification!.AntibioticName);
    }

    /// <summary>수용 기준: 국소 제제(Neomycin 연고)는 안내를 출력하지 않는다.</summary>
    [Fact]
    public async Task MatchAsync_ExcludesTopicalAgent()
    {
        var match = await BuildService().MatchAsync("D06AX04", "Neomycin");

        Assert.Equal(AntibioticMatchOutcome.ExcludedTopical, match.Outcome);
        Assert.False(match.RequiresCounselling);
    }

    /// <summary>
    /// J01 접두사로 거르면 안 된다는 것을 못박아 두는 테스트.
    /// 접두사 필터를 넣는 순간 이 테스트가 깨진다.
    /// </summary>
    [Fact]
    public async Task MatchAsync_MatchesSystemicAgentOutsideJ01()
    {
        var match = await BuildService().MatchAsync("A07AA11", "Rifaximin");

        Assert.Equal(AntibioticMatchOutcome.Matched, match.Outcome);
    }

    /// <summary>수용 기준: 미등록 항생제는 unmatched로 통과시킨다 (판매는 정상 진행).</summary>
    [Fact]
    public async Task MatchAsync_ReturnsUnmatchedForUnknownAgent()
    {
        var match = await BuildService().MatchAsync(null, "Some Unlisted Antibiotic");

        Assert.Equal(AntibioticMatchOutcome.Unmatched, match.Outcome);
        Assert.Null(match.Classification);
    }

    [Fact]
    public async Task MatchAsync_ReturnsUnmatchedForNonAntibioticProduct()
    {
        var match = await BuildService().MatchAsync(null, "Paracetamol");

        Assert.Equal(AntibioticMatchOutcome.Unmatched, match.Outcome);
    }

    [Fact]
    public async Task MatchAsync_ReturnsUnmatchedWhenBothInputsAreEmpty()
    {
        var match = await BuildService().MatchAsync(null, null);

        Assert.Equal(AntibioticMatchOutcome.Unmatched, match.Outcome);
    }

    /// <summary>ATC 코드가 우선한다 — 성분명이 다른 항생제를 가리켜도 ATC 쪽을 따른다.</summary>
    [Fact]
    public async Task MatchAsync_PrefersAtcCodeOverGenericName()
    {
        var match = await BuildService().MatchAsync("J01FA10", "Amoxicillin");

        Assert.Equal("Azithromycin", match.Classification!.AntibioticName);
    }

    /// <summary>ATC 코드가 참조 데이터에 없으면 성분명으로 한 번 더 시도한다.</summary>
    [Fact]
    public async Task MatchAsync_FallsBackToGenericNameWhenAtcCodeIsUnknown()
    {
        var match = await BuildService().MatchAsync("Z99ZZ99", "Amoxicillin");

        Assert.Equal(AntibioticMatchOutcome.Matched, match.Outcome);
        Assert.Equal("Amoxicillin", match.Classification!.AntibioticName);
    }

    /// <summary>
    /// 참조 데이터가 아예 적재되지 않은 상태(시드 파일 미설치)에서도
    /// 예외 없이 unmatched를 돌려줘야 한다.
    /// </summary>
    [Fact]
    public async Task MatchAsync_ReturnsUnmatchedWhenReferenceDataIsEmpty()
    {
        var match = await BuildService(new FakeAwareClassificationRepository())
            .MatchAsync("J01CA04", "Amoxicillin");

        Assert.Equal(AntibioticMatchOutcome.Unmatched, match.Outcome);
    }

    /// <summary>
    /// 조회가 실패해도 예외를 밖으로 던지지 않는다.
    /// 여기서 예외가 새면 판매 화면이 멈춘다.
    /// </summary>
    [Fact]
    public async Task MatchAsync_SwallowsRepositoryFailure()
    {
        var repository = BuildRepository();
        repository.ThrowOnQuery = true;

        var match = await BuildService(repository).MatchAsync("J01CA04", "Amoxicillin");

        Assert.Equal(AntibioticMatchOutcome.Unmatched, match.Outcome);
    }
}
