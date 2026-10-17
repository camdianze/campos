using PharmaPOS.Application.Counselling;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Tests.Counselling;

public class AwareSeedCsvParserTests
{
    private const string Header = "atc_code,antibiotic_name,aware_group,is_systemic,source_version";

    [Fact]
    public void Parse_ReadsAllFourGroups()
    {
        var csv = string.Join("\n",
            Header,
            "J01CA04,Amoxicillin,ACCESS,true,WHO AWaRe 2025",
            "J01FA10,Azithromycin,WATCH,true,WHO AWaRe 2025",
            "J01XX09,Daptomycin,RESERVE,true,WHO AWaRe 2025",
            ",Amoxicillin/clavulanic acid FDC,NOT_RECOMMENDED,true,WHO AWaRe 2025");

        var result = AwareSeedCsvParser.Parse(csv);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Rows.Count);
        Assert.Equal(AwareGroup.Access, result.Rows[0].AwareGroup);
        Assert.Equal(AwareGroup.Watch, result.Rows[1].AwareGroup);
        Assert.Equal(AwareGroup.Reserve, result.Rows[2].AwareGroup);
        Assert.Equal(AwareGroup.NotRecommended, result.Rows[3].AwareGroup);
    }

    /// <summary>
    /// 고유 ATC 코드가 없는 복합제 행이 들어와도 적재돼야 한다.
    /// 이 행들이 바로 NOT_RECOMMENDED 그룹이라, 여기서 걸러지면
    /// 정작 안내가 가장 필요한 상품에서 기능이 죽는다.
    /// </summary>
    [Fact]
    public void Parse_AcceptsRowsWithoutAtcCode()
    {
        var csv = string.Join("\n",
            Header,
            ",Ampicillin/cloxacillin FDC,NOT_RECOMMENDED,true,WHO AWaRe 2025");

        var result = AwareSeedCsvParser.Parse(csv);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Rows);
        Assert.Null(row.AtcCode);
        Assert.Equal(AwareGroup.NotRecommended, row.AwareGroup);
    }

    [Fact]
    public void Parse_HandlesQuotedFieldContainingComma()
    {
        var csv = string.Join("\n",
            Header,
            ",\"Amoxicillin/clavulanic acid, fixed-dose combination\",NOT_RECOMMENDED,true,WHO AWaRe 2025");

        var result = AwareSeedCsvParser.Parse(csv);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Amoxicillin/clavulanic acid, fixed-dose combination", row.AntibioticName);
    }

    [Fact]
    public void Parse_IgnoresColumnOrder()
    {
        var csv = string.Join("\n",
            "source_version,aware_group,antibiotic_name,is_systemic,atc_code",
            "WHO AWaRe 2025,ACCESS,Amoxicillin,true,J01CA04");

        var result = AwareSeedCsvParser.Parse(csv);

        var row = Assert.Single(result.Rows);
        Assert.Equal("J01CA04", row.AtcCode);
        Assert.Equal("Amoxicillin", row.AntibioticName);
        Assert.Equal("WHO AWaRe 2025", row.SourceVersion);
    }

    [Fact]
    public void Parse_ReadsIsSystemicFalseForTopicalAgents()
    {
        var csv = string.Join("\n",
            Header,
            "D06AX04,Neomycin,ACCESS,false,WHO AWaRe 2025");

        var result = AwareSeedCsvParser.Parse(csv);

        Assert.False(Assert.Single(result.Rows).IsSystemic);
    }

    /// <summary>
    /// 잘못된 줄 하나가 파일 전체를 버리게 하면 안 된다.
    /// 그 줄만 건너뛰고 나머지는 적재하되, 사유는 남긴다.
    /// </summary>
    [Fact]
    public void Parse_SkipsBadRowButKeepsTheRest()
    {
        var csv = string.Join("\n",
            Header,
            "J01CA04,Amoxicillin,ACESS,true,WHO AWaRe 2025",
            "J01FA10,Azithromycin,WATCH,true,WHO AWaRe 2025");

        var result = AwareSeedCsvParser.Parse(csv);

        Assert.True(result.IsSuccess);
        Assert.Equal("Azithromycin", Assert.Single(result.Rows).AntibioticName);
        Assert.Contains(result.Errors, e => e.Contains("ACESS"));
    }

    [Fact]
    public void Parse_FailsWhenRequiredColumnIsMissing()
    {
        var csv = string.Join("\n",
            "atc_code,antibiotic_name,is_systemic,source_version",
            "J01CA04,Amoxicillin,true,WHO AWaRe 2025");

        var result = AwareSeedCsvParser.Parse(csv);

        Assert.False(result.IsSuccess);
        Assert.Contains("aware_group", result.Message);
    }

    [Fact]
    public void Parse_FailsOnEmptyFile()
    {
        Assert.False(AwareSeedCsvParser.Parse("").IsSuccess);
    }

    [Fact]
    public void Parse_ReturnsNoRowsForHeaderOnlyTemplate()
    {
        // 저장소에 동봉된 빈 템플릿을 읽은 상황. 파싱은 성공하지만 행이 없다.
        var result = AwareSeedCsvParser.Parse(Header + "\n");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Rows);
    }
}
