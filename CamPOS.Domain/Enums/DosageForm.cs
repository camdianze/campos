namespace PharmaPOS.Domain.Enums;

/// <summary>
/// 제형. 다른 enum들과 같이 이름 그대로 TEXT로 저장한다.
///
/// <b>판매 단위(Product.Unit)와 다른 값이다.</b> Unit은 "낱개 하나를 무엇이라 부르는가"이고
/// (Tablet, Bottle, Tube — 그래서 "Tablets Per Box"처럼 셀 수 있어야 한다),
/// 이것은 "약이 어떤 형태인가"다. 고형 경구약에서만 우연히 같아 보인다 —
/// 시럽은 제형이 Syrup이지만 세는 단위는 Bottle이고, 연고는 Ointment지만 Tube다.
///
/// 선택 입력이라 Product.DosageForm은 nullable이다. 비의약품(마스크 등)에는 제형이 없고,
/// ProductCategory와 같은 이유로 "아직 안 적음"과 "없다고 정함"을 섞지 않는다.
///
/// 자유 입력이 아니라 고정 목록으로 둔 이유: Unit이 자유 입력이라 이미 Tablet/tablet처럼
/// 표기가 흔들린다. 제형은 나중에 AWaRe 경로별 분류(정맥 RESERVE / 경구 WATCH)와
/// 국소 제제 제외에 쓸 수 있는 값이라, 그때 기계가 읽을 수 있어야 한다.
/// (현재는 표시 전용이다. 복약안내 판정에는 아직 쓰지 않는다.)
/// </summary>
public enum DosageForm
{
    Tablet,
    Capsule,
    Syrup,
    Suspension,
    Powder,
    Injection,
    Infusion,
    Ointment,
    Cream,
    Drops,
    Suppository,
    Inhaler,
    Other
}
