using Microsoft.Data.Sqlite;
using PharmaPOS.Application.Counselling;
using PharmaPOS.DataAccess.Database;
using PharmaPOS.DataAccess.Repositories;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Counselling;

/// <summary>
/// 저장소에 동봉된 실제 WHO AWaRe 2025 시드 파일을 그대로 적재해서 확인한다.
///
/// 앞선 테스트들은 손으로 만든 소규모 데이터를 쓰지만, 매칭이 실제로 통하는지는
/// 진짜 목록에 대고 봐야 안다. 파일이 교체되면 여기서 먼저 깨진다.
/// </summary>
public class ShippedSeedFileTests : IDisposable
{
    private readonly string _directory;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly AwareClassificationRepository _awareRepository;

    public ShippedSeedFileTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "pharmapos-seed-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_directory);

        _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        new DatabaseInitializer(_connectionFactory).Initialize();

        _awareRepository = new AwareClassificationRepository(_connectionFactory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static string FindSeedFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PharmaPOS.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "PharmaPOS.Wpf", "seeds", "aware_2025.csv");
    }

    private async Task<AwareSeedLoadResult> LoadSeedAsync()
    {
        var loader = new AwareSeedLoader(
            _awareRepository,
            new AppSettingRepository(_connectionFactory),
            new[] { FindSeedFile() });

        return await loader.LoadIfChangedAsync();
    }

    private Task<Domain.Entities.AwareClassification?> FindByNameAsync(string genericName)
        => _awareRepository.FindByNormalizedNameAsync(AntibioticNameNormalizer.Normalize(genericName));

    private Task<Domain.Entities.AwareClassification?> FindByAtcAsync(string atcCode)
        => _awareRepository.FindByAtcCodeAsync(AntibioticNameNormalizer.NormalizeAtcCode(atcCode));

    // ── 적재 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 파일 전체가 형식 오류 없이 들어가야 한다.
    /// 건너뛴 줄이 하나라도 있으면 그 항생제는 영원히 unmatched가 된다.
    /// </summary>
    [Fact]
    public async Task ShippedSeed_LoadsEveryRowWithoutErrors()
    {
        var result = await LoadSeedAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.Empty(result.RowErrors);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal("WHO AWaRe 2025", result.SourceVersion);
        Assert.Equal(result.LoadedCount, await _awareRepository.CountAsync());
    }

    /// <summary>
    /// 네 그룹이 모두 들어 있어야 한다. NOT_RECOMMENDED가 빠지면
    /// 복합제가 통째로 조회에서 사라진다.
    /// </summary>
    [Fact]
    public async Task ShippedSeed_ContainsAllFourGroups()
    {
        await LoadSeedAsync();

        Assert.Equal(AwareGroup.Access, (await FindByNameAsync("Amoxicillin"))!.AwareGroup);
        Assert.Equal(AwareGroup.Watch, (await FindByNameAsync("Azithromycin"))!.AwareGroup);
        Assert.Equal(AwareGroup.Reserve, (await FindByNameAsync("Daptomycin"))!.AwareGroup);
        Assert.Equal(
            AwareGroup.NotRecommended,
            (await FindByNameAsync("Amoxicillin/cloxacillin"))!.AwareGroup);
    }

    // ── 수용 기준을 실제 데이터로 재확인 ─────────────────────────────────────

    [Theory]
    [InlineData("J01CA04", AwareGroup.Access)]     // Amoxicillin
    [InlineData("J01FA10", AwareGroup.Watch)]      // Azithromycin
    [InlineData("J01XX09", AwareGroup.Reserve)]    // Daptomycin
    public async Task ShippedSeed_ResolvesAcceptanceCriteriaByAtcCode(string atcCode, AwareGroup expected)
    {
        await LoadSeedAsync();

        Assert.Equal(expected, (await FindByAtcAsync(atcCode))!.AwareGroup);
    }

    /// <summary>수용 기준: 염 형태가 붙어 있어도 실제 목록에서 찾아진다.</summary>
    [Theory]
    [InlineData("Azithromycin dihydrate", "Azithromycin")]
    [InlineData("Amoxicillin trihydrate", "Amoxicillin")]
    [InlineData("Clindamycin hydrochloride", "Clindamycin")]
    [InlineData("Gentamicin sulfate", "Gentamicin")]
    [InlineData("Doxycycline hyclate", "Doxycycline")]
    [InlineData("Cefazolin sodium", "Cefazolin")]
    public async Task ShippedSeed_MatchesSaltForms(string productGenericName, string expectedName)
    {
        await LoadSeedAsync();

        Assert.Equal(expectedName, (await FindByNameAsync(productGenericName))!.AntibioticName);
    }

    /// <summary>수용 기준: cef- / ceph- 양쪽 표기 모두 매칭된다.</summary>
    [Theory]
    [InlineData("Cefalexin")]
    [InlineData("Cephalexin")]
    [InlineData("Cefradine")]
    [InlineData("Cephradine")]
    public async Task ShippedSeed_MatchesBothCefAndCephSpellings(string genericName)
    {
        await LoadSeedAsync();

        Assert.NotNull(await FindByNameAsync(genericName));
    }

    /// <summary>영/미 철자 차이(sulph-/sulf-)도 흡수한다.</summary>
    [Theory]
    [InlineData("Sulphamethoxazole")]
    [InlineData("Sulfamethoxazole")]
    public async Task ShippedSeed_MatchesSulfonamideSpellingVariants(string genericName)
    {
        await LoadSeedAsync();

        Assert.Equal("Sulfamethoxazole", (await FindByNameAsync(genericName))!.AntibioticName);
    }

    /// <summary>
    /// 복합제는 구분자 표기가 제각각이라, 상품에 어떻게 적혀 있든 같은 행으로 모여야 한다.
    /// </summary>
    [Theory]
    [InlineData("Amoxicillin/clavulanic acid")]
    [InlineData("Amoxicillin + clavulanic acid")]
    [InlineData("amoxicillin-clavulanic-acid")]
    [InlineData("Amoxicillin/clavulanic acid 625mg")]
    public async Task ShippedSeed_MatchesCombinationRegardlessOfSeparator(string genericName)
    {
        await LoadSeedAsync();

        var match = await FindByNameAsync(genericName);

        Assert.Equal(AwareGroup.Access, match!.AwareGroup);
        Assert.Equal("J01CR02", match.AtcCode);
    }

    /// <summary>수용 기준: 목록에 없는 성분은 조회되지 않는다 (unmatched 경로).</summary>
    [Theory]
    [InlineData("Paracetamol")]
    [InlineData("Ibuprofen")]
    [InlineData("Some Unlisted Antibiotic")]
    public async Task ShippedSeed_DoesNotMatchNonAntibiotics(string genericName)
    {
        await LoadSeedAsync();

        Assert.Null(await FindByNameAsync(genericName));
    }

    // ── 실데이터에서 드러난 문제 ─────────────────────────────────────────────

    /// <summary>
    /// Minocycline(J01AA08)과 Fosfomycin(J01XX01)은 같은 ATC 코드로 두 번 나오고,
    /// 주사는 RESERVE, 경구는 WATCH다. 복약안내 판정에 제형(dosage_form)을 쓰지 않으므로
    /// 더 강한 안내가 필요한 쪽(RESERVE)을 고른다.
    ///
    /// 경구 제품을 RESERVE로 표시하는 것은 과한 경고에 그치지만,
    /// 주사 제품을 WATCH로 낮추면 필요한 경고를 놓친다.
    /// </summary>
    [Theory]
    [InlineData("J01AA08")]
    [InlineData("J01XX01")]
    public async Task ShippedSeed_PicksStricterGroupWhenRouteDecidesTheClassification(string atcCode)
    {
        await LoadSeedAsync();

        Assert.Equal(AwareGroup.Reserve, (await FindByAtcAsync(atcCode))!.AwareGroup);
    }

    [Theory]
    [InlineData("Minocycline")]
    [InlineData("Fosfomycin")]
    public async Task ShippedSeed_PicksStricterGroupByNameToo(string genericName)
    {
        await LoadSeedAsync();

        Assert.Equal(AwareGroup.Reserve, (await FindByNameAsync(genericName))!.AwareGroup);
    }

    /// <summary>
    /// 이 파일은 384행 전부 is_systemic이 true다. 즉 국소 제제 제외 경로가
    /// 이 데이터로는 한 번도 동작하지 않는다. 상품에 ATC 코드를 채우지 않으면
    /// 연고류가 성분명만으로 매칭돼 안내지가 나갈 수 있다는 뜻이라, 사실을 고정해 둔다.
    /// </summary>
    [Fact]
    public async Task ShippedSeed_ContainsNoTopicalEntries()
    {
        await LoadSeedAsync();

        var neomycin = await FindByNameAsync("Neomycin");

        Assert.NotNull(neomycin);
        Assert.True(neomycin!.IsSystemic);
    }
}
