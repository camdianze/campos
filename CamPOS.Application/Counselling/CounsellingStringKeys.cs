namespace PharmaPOS.Application.Counselling;

/// <summary>
/// 복약안내 용지에서 쓰는 로케일 키.
///
/// 한번 확정한 키는 바꾸지 않는다. 추가만 하고, 폐기할 때는 "deprecated." 접두를 붙인다.
/// 이미 배포된 로케일 파일들이 이 이름으로 값을 담고 있기 때문에,
/// 키를 바꾸면 그 줄이 조용히 영어로 되돌아간다.
/// </summary>
public static class CounsellingStringKeys
{
    public const string SheetSubtitle = "sheet.subtitle";

    public const string LabelDose = "label.dose";
    public const string LabelFrequency = "label.frequency";
    public const string LabelDuration = "label.duration";
    public const string LabelTake = "label.take";

    public const string TakeBefore = "take.before";
    public const string TakeAfter = "take.after";
    public const string TakeEither = "take.either";

    public const string SectionImportant = "section.important";

    public const string Important1 = "important.1";
    public const string Important2 = "important.2";
    public const string Important3 = "important.3";
    public const string Important4 = "important.4";
    public const string Important5 = "important.5";

    public const string QrCaption = "qr.caption";
}
