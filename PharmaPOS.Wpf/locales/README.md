# 복약안내 로케일 파일

복약안내 용지의 **현지어 레이어**를 담는다. 영어는 고정 레이어이므로
이 파일들과 무관하게 항상 인쇄된다. 여기 있는 문구는 영어 줄 옆에 덧붙는다.

파일 이름은 BCP 47 코드 + `.json`이며 **국가 코드가 필수**다:
`km-KH.json`, `lo-LA.json`, `my-MM.json`, `vi-VN.json`.

## 형식

```json
{
  "locale": "km-KH",
  "language_name": "ភាសាខ្មែរ",
  "script": "Khmer",
  "render_mode": "raster",
  "review_status": "pending",
  "reviewed_by": null,
  "content_version": "1.0.0",
  "strings": {
    "sheet.subtitle": "…",
    "label.dose": "…"
  }
}
```

| 필드 | 규칙 |
|---|---|
| `review_status` | **`approved`가 아니면 현지어를 한 글자도 인쇄하지 않는다.** 영어 단독으로 출력된다. 검수를 마친 뒤에만 `approved`로 바꾼다. |
| `reviewed_by` | 누가 검수했는지. `approved`로 바꿀 때 함께 채운다. |
| `content_version` | 문구 개정 버전. AWaRe `source_version`과 **별개로** 관리한다. |
| `render_mode` | `text` / `raster`. 복합 문자 계열(크메르·라오·미얀마·태국·싱할라)은 `raster`. |
| `strings` | 키는 고정이다. 아래 표 참조. |

### 키 목록

**복약안내 용지**

`sheet.subtitle`, `label.dose`, `label.frequency`, `label.duration`, `label.take`,
`take.before`, `take.after`, `take.either`, `section.important`,
`important.1` ~ `important.5`, `qr.caption`

**판매 영수증** — 접두어는 `receipt.` 로 통일한다.

`receipt.lbl.receiptNo`, `receipt.lbl.date`, `receipt.lbl.servedBy`, `receipt.lbl.payment`,
`receipt.lbl.vatTin`, `receipt.col.item`, `receipt.col.qty`, `receipt.col.price`,
`receipt.col.amount`, `receipt.lbl.totalQty`, `receipt.lbl.vat`, `receipt.lbl.total`,
`receipt.lbl.inRiel`, `receipt.lbl.fxRate`, `receipt.lbl.cashTendered`,
`receipt.lbl.changeDue`, `receipt.unit.box`, `receipt.unit.each`, `receipt.lbl.pieces`,
`receipt.brand.tagline`

- 값 안의 `{tin}` `{rate}` `{count}` 는 **변수 자리표시자**다. 이름을 바꾸거나 지우면
  그 자리에 값이 들어가지 않는다. 문장 안에서 위치는 언어에 맞게 옮겨도 된다 —
  옮길 수 있게 하려고 문자열을 조각내지 않고 통째로 두는 것이다.
- **영수증의 수량·금액은 아라비아 숫자로 인쇄된다.** 크메르 숫자(០១២៣)를 쓰지 않는다.
  거래 금액을 두 가지 숫자 체계로 적으면 대조가 불가능해진다.
- **약품명은 번역하지 않는다.** 라틴 문자 국제일반명(INN)이 그대로 나간다.
  로케일 파일이 번역하는 것은 제형·단위와 라벨뿐이다.
- 약국 이름·주소·맺음 문구는 이 파일이 아니라 **영수증 설정 화면**에 크메르어/영어
  두 벌로 들어 있다. 약국마다 다른 값이라 동봉 파일에 넣을 수 없다.

> `review_status`는 파일 전체에 걸린다. `approved`로 바꾸면 복약안내와 영수증의
> 크메르어가 **함께** 켜진다. 한쪽만 검수하고 승인해서는 안 된다.

- **확정된 키는 바꾸지 않는다.** 추가만 하고, 폐기할 때는 `deprecated.` 접두를 붙인다.
- 키가 없거나 값이 비어 있으면 **그 줄만** 영어로 인쇄된다.
  빈칸이나 키 문자열이 용지에 찍히는 일은 없다.
- **AWaRe 분류명(`ACCESS` / `WATCH` / `RESERVE` / `NOT_RECOMMENDED`)은 번역 대상이 아니다.**
  이 값들은 로케일 파일에 아예 넣지 않는다. 번역하면 국가 간 지표 집계가 불가능해진다.

## 파일을 놓는 위치

1. `%APPDATA%\PharmaPOS\locales\` — 현장 교체용. 재빌드 없이 갱신 가능.
2. `(설치 폴더)\locales\` — 빌드에 동봉되는 기본본.

같은 코드가 양쪽에 있으면 1번이 이긴다. 검수를 마친 번역은 1번 자리에 놓으면 된다.

## render_mode 에 대하여

이 프로그램은 Windows 인쇄 파이프라인(WPF `FixedDocument`)으로 출력한다.
자소 결합·문자 셰이핑이 렌더링 단계에서 처리되므로 `text`와 `raster`가
실제로는 같은 경로를 탄다. 필드를 남겨 둔 이유는 로케일 파일이 프린터 방식과
무관하게 형식을 유지해야 하기 때문이다 — 나중에 ESC/POS 직결 어댑터를 붙이면
그때 이 값으로 분기한다.

크메르어 문자열이 깨져 보인다면 원인은 이 필드가 아니라 **글꼴**이다.
해당 문자를 지원하는 글꼴이 PC에 설치돼 있어야 한다.
