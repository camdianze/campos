namespace PharmaPOS.Application.Counselling;

/// <summary>복약안내 용지를 어디로 내보낼지.</summary>
public enum CounsellingOutput
{
    /// <summary>기본 프린터로 인쇄한다.</summary>
    Printer,

    /// <summary>
    /// 인쇄하는 대신 텍스트 파일로 저장한다.
    /// 프린터가 없거나 드라이버가 변수인 상황에서 용지 내용 자체를 확인하기 위한 것이다.
    /// </summary>
    File
}
