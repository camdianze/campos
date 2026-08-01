namespace PharmaPOS.Application.Counselling;

/// <summary>복약안내 용지 분량.</summary>
public enum CounsellingSheetFormat
{
    /// <summary>전체 안내. 58mm 기준 약 20cm.</summary>
    Full,

    /// <summary>
    /// 축약본. 58mm 기준 약 10cm.
    /// 용지 원가가 약사의 인쇄 거부 사유가 될 수 있어 준비한 선택지다.
    /// </summary>
    Compact
}
