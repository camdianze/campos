using System.Text;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// AMR 연구에 제출하는 항생제 내보내기 파일을 만든다.
///
/// <b>이 파일은 약국 밖으로 나간다.</b> 매출 리포트의 다른 표들과 달리 의무 제출물이고,
/// 받는 쪽은 약국이 아니다. 그래서 지켜야 하는 규칙이 하나 있다:
///
///   <b>금액을 한 글자도 넣지 않는다.</b>
///
/// 항생제 소비 지표는 수량(과 그로부터 계산하는 DDD)으로 세는 것이라 연구 쪽에서
/// 금액을 쓸 일이 없다. 반면 약국에게 매출액은 남에게 넘기기를 꺼리는 정보다.
/// 의무 제출을 하려다 영업 정보까지 함께 넘어가게 되면, 그 사실을 알아차린 약국은
/// 제출 자체를 꺼리게 된다 — 그러면 연구 쪽이 잃는 것이 더 크다.
///
/// 이 클래스가 ViewModel이 아니라 Application에 있는 이유가 그 규칙 때문이다.
/// 주석만으로는 나중에 누가 금액 열을 다시 넣는 것을 막을 수 없어서,
/// 테스트가 닿는 자리에 둔다(AntibioticExportCsvTests).
///
/// 약국이 볼 성분별 매출은 매출 파일 쪽에 있다. 그쪽은 약국이 갖는 파일이다.
/// </summary>
public static class AntibioticExportCsv
{
    /// <summary>사이트 코드가 설정되지 않았을 때 그 자리에 적는 값.</summary>
    public const string MissingSiteCode = "(not set)";

    /// <param name="siteCode">
    /// 연구기관이 등록 때 부여한 사이트 코드. 이 파일에서 약국을 가리키는 유일한 값이다.
    ///
    /// 코드 자체는 아무것도 드러내지 않는다 — 코드와 약국의 대응표는 연구기관만 갖는다.
    /// 그래서 시설명·지역·시설 유형은 <b>넣지 않는다</b>. 그것들이 들어가면 판매량이 적은
    /// 달에는 후보가 몇 곳까지 좁혀져서, 코드를 가명으로 둔 의미가 사라진다.
    /// 지역별 집계가 필요하면 연구기관이 대응표 쪽에 지역을 적어 두고 붙이면 된다.
    ///
    /// 비어 있으면 "(not set)"으로 적는다. 출처 없는 파일이 나가는 것 자체는 막지 않되,
    /// 받는 쪽이 그 사실을 파일만 보고 알 수 있어야 한다.
    /// </param>
    public static string Build(ReportData report, string? siteCode = null)
    {
        var builder = new StringBuilder();

        // 파일 하나만 열어도 언제 것인지, 어디 것인지 알 수 있어야 한다.
        // 파일 이름은 옮기다 보면 바뀐다.
        builder.AppendLine($"Report period,{report.Range.Label}");
        builder.AppendLine($"Site code,{Escape(NormalizeSiteCode(siteCode))}");
        builder.AppendLine($"Compared with,{report.Range.PreviousLabel}");
        builder.AppendLine();

        AppendGroupShares(builder, report);
        AppendIngredients(builder, report);
        AppendTrend(builder, report);

        return builder.ToString();
    }

    private static void AppendGroupShares(StringBuilder builder, ReportData report)
    {
        builder.AppendLine("AwareGroup,Quantity,Share");

        foreach (var share in report.GroupShares)
        {
            builder.AppendLine($"{share.Group},{share.Quantity},{Escape(share.ShareDisplay)}");
        }

        builder.AppendLine();
    }

    private static void AppendIngredients(StringBuilder builder, ReportData report)
    {
        // 화면에서 뺀 복약안내 출력률(PrintRate)은 파일에 남긴다 — 성분별로 되짚어 보는 것은
        // 화면에서 기간 전체를 훑는 것과 다른 용도다. 이건 금액이 아니라 이행률이다.
        builder.AppendLine(
            "Ingredient,Strength,AwareGroup,Quantity,QuantityShare," +
            "Counselled,Sales,PrintRate,PrevQuantity,QuantityChange");

        var total = report.AntibioticQuantity;

        foreach (var row in report.Antibiotics)
        {
            // 비중의 분모는 화면과 같은 값이다. 한 줄만으로는 알 수 없어 여기서 채운다
            // (화면에서 ViewModel이 하는 것과 같은 자리·같은 이유).
            row.TotalQuantityInPeriod = total;

            builder.AppendLine(
                $"{Escape(row.Ingredient)},{Escape(row.Strength)},{row.AwareGroup}," +
                $"{row.Quantity},{Escape(row.QuantityShare)}," +
                $"{row.CounsellingPrinted},{row.SaleCount}," +
                $"{Escape(row.PrintedPercentDisplay)},{row.PreviousQuantity},{Escape(row.QuantityChange)}");
        }

        builder.AppendLine();
    }

    private static void AppendTrend(StringBuilder builder, ReportData report)
    {
        // 추이는 고른 기간이 아니라 그 기간이 끝나는 달까지의 12개월이다.
        // 파일에서도 헷갈리지 않도록 표 이름에 실제 구간을 적는다.
        var months = report.AntibioticTrend.Count > 0
            ? $"{report.AntibioticTrend[0].FullLabel} ~ {report.AntibioticTrend[^1].FullLabel}"
            : report.Range.Label;

        builder.AppendLine($"AntibioticTrend ({months})");
        builder.AppendLine("Month,Total,ACCESS,WATCH,RESERVE,NOT_RECOMMENDED");

        foreach (var point in report.AntibioticTrend)
        {
            builder.AppendLine(
                $"{point.FullLabel},{point.TotalQuantity},{point.AccessQuantity}," +
                $"{point.WatchQuantity},{point.ReserveQuantity},{point.NotRecommendedQuantity}");
        }
    }

    /// <summary>
    /// 앞뒤 공백을 털고, 비었으면 표시용 값으로 바꾼다.
    /// </summary>
    private static string NormalizeSiteCode(string? siteCode)
    {
        // 제어 문자를 공백으로 바꾼다. 붙여넣기로 들어온 코드에 줄바꿈이나 탭이 섞이면
        // CSV 한 줄이 두 줄로 쪼개져 받는 쪽에서 파일이 깨진다.
        var trimmed = new string(
            (siteCode ?? string.Empty).Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();

        return string.IsNullOrEmpty(trimmed) ? MissingSiteCode : trimmed;
    }

    /// <summary>성분명에 쉼표가 들어가는 복합제가 있어 CSV 값은 감싸 준다.</summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
