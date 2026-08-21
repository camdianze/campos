using System.Globalization;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Reports;

/// <summary>
/// 항생제 판매 추이의 한 달치.
///
/// 판매가 없던 달도 0으로 채워 넣는다. 없는 달을 빼 버리면 그래프의 가로 간격이
/// 달마다 달라져서, 비어 있는 달과 이어지는 달을 눈으로 구분할 수 없다.
///
/// AWaRe 등급별로 나누어 담는 이유: 항생제 총량이 그대로여도 WATCH·RESERVE 쪽으로
/// 옮겨가는 중이면 상황이 나빠지고 있는 것이다. 총량만 보면 그 이동이 보이지 않는다.
/// </summary>
public class AntibioticTrendPoint
{
    /// <summary>그 달의 1일.</summary>
    public required DateTime Month { get; init; }

    /// <summary>낱개 기준 판매 수량.</summary>
    public int AccessQuantity { get; init; }
    public int WatchQuantity { get; init; }
    public int ReserveQuantity { get; init; }
    public int NotRecommendedQuantity { get; init; }

    public int TotalQuantity =>
        AccessQuantity + WatchQuantity + ReserveQuantity + NotRecommendedQuantity;

    /// <summary>가로축 눈금. 해가 바뀌는 달만 연도를 함께 적어 12칸이 빽빽해지지 않게 한다.</summary>
    public string Label => Month.Month == 1
        ? Month.ToString("yyyy", CultureInfo.InvariantCulture)
        : Month.ToString("MMM", CultureInfo.InvariantCulture);

    public string FullLabel => Month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>그룹 코드로 수량을 꺼낸다. 화면이 색과 순서를 한 곳에서 다루도록.</summary>
    public int QuantityOf(string awareGroup) => awareGroup switch
    {
        AwareGroupCodes.Access => AccessQuantity,
        AwareGroupCodes.Watch => WatchQuantity,
        AwareGroupCodes.Reserve => ReserveQuantity,
        AwareGroupCodes.NotRecommended => NotRecommendedQuantity,
        _ => 0
    };
}
