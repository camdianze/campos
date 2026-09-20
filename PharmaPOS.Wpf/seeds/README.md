# AWaRe 시드 데이터

`aware_2025.csv`는 WHO AWaRe 분류표를 담는 참조 데이터 파일이다.
동봉본은 **실제 WHO 2025 목록 384행**이다 (ACCESS 93 / WATCH 145 / RESERVE 30,
합계 268개 분류 항생제 + NOT_RECOMMENDED 116개 복합제).
이 개수는 `ShippedSeed_HasExactlyTheRowsWhoPublished`가 고정한다 — 개정판으로 교체할 때
그 숫자도 함께 고치게 되고, 고치려면 새 원문을 세어 보게 된다.

출처: WHO, *The selection and use of essential medicines, 2025: WHO AWaRe
classification of antibiotics for evaluation and monitoring of use* (2025-09-05)

**원문은 [docs/reference/who-aware-2025.pdf](../../docs/reference/who-aware-2025.pdf)에
함께 두었다.** 대조할 때마다 같은 판본을 보기 위해서다 — 직전 대조는 포털 화면을
긁어서 했고, 거기서 없는 항목 2건이 "빠진 것"으로 보여 시드에 들어갔다.
`docs/`는 게시물(exe)에 실리지 않으므로 제품에는 포함되지 않는다.
라이선스는 CC BY-NC-SA 3.0 IGO이고, 이 사본은 시드를 검증하기 위한 사내 참고본이다.

분류값은 코드에 하드코딩하지 않는다. 개정판이 나오면 이 파일만 교체한다.
**값을 추정해서 채우지 말 것.**

## 파일 형식

```csv
id,atc_code,antibiotic_name,match_key,route,aware_group,antibiotic_class,on_eml,is_systemic,source_version
1,J01GB06,Amikacin,amikacin,ANY,ACCESS,Aminoglycosides,Yes,true,WHO AWaRe 2025
101,,amoxicillin/cloxacillin,amoxicillin/cloxacillin,ANY,NOT_RECOMMENDED,Fixed-dose combination,No,true,WHO AWaRe 2025
```

프로그램이 **읽는 컬럼은 5개**뿐이다. 나머지는 사람이 보기 위한 것이며 무시된다.

| 컬럼 | 사용 | 설명 |
|---|:--:|---|
| `atc_code` | ✅ | WHO ATC 코드. **비어 있어도 된다** — 고유 ATC가 없는 복합제가 116행 있다. 값이 있으면 성분명보다 우선해서 매칭에 쓰인다. |
| `antibiotic_name` | ✅ | 항생제명. 필수. 쉼표가 들어가면 큰따옴표로 감싼다. |
| `aware_group` | ✅ | `ACCESS` / `WATCH` / `RESERVE` / `NOT_RECOMMENDED`. 필수. **번역하지 않는다.** |
| `is_systemic` | ✅ | `true`면 전신 제제. `false`면 국소 제제로 보고 복약안내 대상에서 제외된다. |
| `source_version` | ✅ | 출처 표기. 인쇄물과 로그에 그대로 찍힌다. |
| `id` | — | 행 번호. |
| `match_key` | — | 사전 정규화된 이름. 프로그램은 `antibiotic_name`에 자체 정규화 규칙을 적용하므로 쓰지 않는다. 상품 쪽과 시드 쪽에 **같은 함수**를 적용해야 표기 흔들림이 흡수되기 때문이다. |
| `route` | ⚠️ | `ANY` / `IV` / `ORAL`. 아래 "제형 문제" 참조. |
| `antibiotic_class` | — | 계열명. |
| `on_eml` | — | WHO 필수의약품목록 등재 여부. |

컬럼 순서는 상관없다 (헤더 이름으로 찾는다). 인코딩은 UTF-8.

## 매칭은 성분명만으로 된다

상품에 ATC 코드를 채우지 않아도 된다. 있으면 먼저 보고, 없거나 못 찾으면
`generic_name`을 정규화해서 찾는다. 이 파일의 116행은 애초에 ATC 코드가 없어
이름으로만 찾을 수 있으므로, 이름 경로가 주 경로다.

ATC는 표기 흔들림이 없다는 장점 때문에 우선할 뿐이며, 상품 등록 시 선택 사항이다.
상품마다 ATC를 입력하게 만들 생각이라면 그 전에 다시 생각할 것 —
그 부담이 곧 기능을 안 쓰게 되는 이유가 된다.

## 알아둘 점 두 가지

### 1. 제형(route)에 따라 분류가 갈리는 항목이 둘 있다

| ATC | 성분 | 주사 | 경구 |
|---|---|---|---|
| `J01AA08` | Minocycline | RESERVE | WATCH |
| `J01XX01` | Fosfomycin | RESERVE | WATCH |

조회는 더 강한 쪽(RESERVE)을 고른다. 경구를 RESERVE로 표시하는 것은 과한 경고에
그치지만, 주사를 WATCH로 낮추면 필요한 경고를 놓치기 때문이다.

`Product_Master.dosage_form`이 생겼으므로 기술적으로는 둘을 가릴 수 있다. 그래도
**지금은 복약안내 판정에 제형을 쓰지 않는다.** 제형은 뒤늦게 추가한 선택 입력이라
기존 상품 대부분이 아직 비어 있고, 비어 있는 값으로 등급을 가리면 "적지 않았다"가
"경구다"로 읽혀 주사 제품의 경고가 조용히 낮아진다. 386행 중 2행 이야기이므로
데이터가 채워질 때까지 서둘 이유도 없다.

(`J01CR02`도 두 행이 쓰지만 Amoxicillin/clavulanic acid와 Amoxicillin/sulbactam
둘 다 ACCESS라 분류에는 영향이 없다.)

### 2. `is_systemic`이 전 행 `true`다

WHO 목록은 전신 항생제만 다루므로 이 파일에는 국소 제제가 없다.
그래서 국소 제제 제외 경로는 이 데이터로는 동작하지 않고, 연고·점안액에
성분명이 적혀 있으면 복약안내지가 나갈 수 있다.

**대개는 그대로 둬도 된다.** 용지에 적히는 내용(끝까지 복용, 남기지 말 것,
나눠 쓰지 말 것)은 국소 항생제에도 어긋나지 않고, 국소 항생제 오남용 역시
내성의 원인이다. 나가는 만큼 용지를 쓸 뿐이다.

정말 빼고 싶으면 **해당 국소 제제 몇 줄만 `is_systemic=false`로 이 파일에
추가**하면 된다. 프로그램은 이 컬럼 값을 그대로 신뢰한다.
상품마다 ATC 코드를 채워서 해결하려 들지 말 것 — 몇 줄 고치면 될 일을
상품 수백 건의 입력 노동으로 옮기는 셈이다.

ATC 접두사(J01 등)로 전신 여부를 자동 판별하지 않는 이유는 전신 항생제가
`A07AA`·`J04`·`P01AB` 등에도 걸쳐 있어 접두사 필터가 틀리기 때문이다.

## 파일을 놓는 위치

앱은 다음 순서로 찾고, 먼저 발견한 파일 하나만 쓴다.

1. `%APPDATA%\PharmaPOS\seeds\aware_2025.csv` — 현장 교체용. 재빌드 없이 갱신 가능.
2. `(설치 폴더)\seeds\aware_2025.csv` — 빌드에 동봉되는 기본본.

파일 내용이 이전 실행과 같으면 다시 적재하지 않는다. AWaRe 개정판이 나오면
1번 자리에 새 파일을 놓고 앱을 재시작하면 된다.

## 적재 실패 시 동작

파일이 없거나 읽을 수 없어도 **앱은 정상 실행되고 판매도 정상 진행된다.**
참조 데이터가 없으면 모든 상품이 unmatched로 기록될 뿐이다.
적재 상태는 설정 화면(관리자 → Counselling)에서 확인할 수 있다.

형식이 잘못된 줄이 있으면 그 줄만 건너뛰고 나머지는 적재한다.
`PharmaPOS.Tests`의 `ShippedSeedFileTests`가 동봉본 전체를 실제로 적재해
건너뛴 줄이 하나도 없는지 검사하므로, 파일을 교체하면 `dotnet test`로 먼저 확인할 것.

## 2026-09-19 WHO 포털 대조

`aware.essentialmeds.org/list`의 376건과 행 단위로 대조했다.

- 이름이 맞는 항목끼리 **등급 불일치 0건**.
- WHO에 있는데 시드에 없다고 보고 **Capreomycin(ACCESS)**, **Sulfamethizole/trimethoprim(ACCESS)** 2건을 추가했다(id 385, 386). **→ 2026-09-20 대조에서 둘 다 WHO 목록에 없는 것으로 확인되어 제거했다. 아래 절을 볼 것.**
- `ceftazidime/tobramicin` 오타를 `tobramycin`으로 고쳤다(id 164).
- 시드에만 있는 NR 복합제 7건은 그대로 뒀다. WHO 포털에 없어도 NR로 잡히는 것이 옳은 방향이다.

시드의 `-proxetil` `-pivoxil` `-fosamil` `-medocaril` 접미는 WHO 원본에는 없다. 지우지 않고 정규화기(`AntibioticNameNormalizer`, RuleVersion 3)가 벗기게 했다 — 상품 쪽 "Cefuroxime Axetil"과 시드 쪽 "Cefuroxime"이 같은 이름이 돼야 하므로, 양쪽에 같은 규칙이 걸리는 쪽이 맞다.

## 2026-09-20 WHO 원문(PDF) 대조

출처를 포털 화면이 아니라 **간행물 PDF**(`The selection and use of essential medicines, 2025`)로
바꿔 384행 전체를 행 단위로 다시 맞췄다. 결과:

| 검사 | 결과 |
|---|---|
| 이름이 같은 항목의 **등급** | **불일치 0건** |
| 이름이 같은 항목의 **ATC 코드** | **불일치 0건** |
| 경로로 갈리는 성분 25행(IV/ORAL) | **등급·ATC 모두 일치** |
| `on_eml` 플래그 50건 | **WHO EML 2025 목록과 정확히 일치** |
| 그룹별 개수 | ACCESS 93 / WATCH 145 / RESERVE 30 / NOT_RECOMMENDED 116 — **원문과 일치** |

**고친 것 — WHO에 없는 2행 제거(id 385, 386).**
직전 대조(2026-09-19)에서 포털을 훑다 넣은 행인데, 원문에는 둘 다 없다.

- **Capreomycin(J04AB30)** — 실재하는 항결핵 주사제지만 AWaRe 분류 대상이 아니다.
  AWaRe가 다루는 것은 J01 계열과 일부 A07AA·P01AB·J04AB(리팜피신류)이고, 다른
  항결핵제는 들어 있지 않다. ACCESS로 둔 채 남겨 두면 2차 주사제에 "가장 안전한 등급"
  안내가 붙는다 — 틀린 방향으로 틀린 것이라 더 나쁘다.
- **Sulfamethizole/trimethoprim** — 원문의 sulfonamide/trimethoprim 복합제는 7건이고
  (sulfadiazine/tetroxoprim, sulfadiazine/trimethoprim, sulfadimidine/trimethoprim,
  sulfamerazine/trimethoprim, sulfamethoxazole/trimethoprim, sulfametrole/trimethoprim,
  sulfamoxole/trimethoprim) 여기에 없다. Sulfamethizole **단독**은 J01EB02 ACCESS로 있고
  시드에도 그대로 있다.

두 이름은 `ShippedSeed_DoesNotCarryEntriesWhoNeverClassified`가 이름으로 막는다.
개수 검사만 두면 다른 행을 빼고 이것을 도로 넣어도 통과하기 때문이다.

**남겨 둔 차이 1 — `ceftazidime/tobramycin`(id 164).** 원문 PDF는 `tobramicin`으로
적혀 있다(WHO 쪽 오타). 시드는 올바른 철자를 쓴다. 매칭 대상은 WHO 표기가 아니라
약국이 입력하는 성분명이고, 어떤 제품도 `tobramicin`으로 적지 않는다.

**남겨 둔 차이 2 — 경로 표기.** WHO는 `Vancomycin_IV` / `Vancomycin_oral`처럼 이름에
경로를 붙이지만, 시드는 이름을 `Vancomycin` 하나로 두고 `route` 열로 갈라 둔다.
성분명으로 매칭하는 구조라 이름에 `_IV`가 붙으면 어떤 제품과도 맞지 않는다.
25행 전부 등급·ATC가 원문과 같은 것을 확인했다.

**이 대조에서 얻은 교훈:** 앞선 대조는 포털 HTML을 긁어 비교했는데, 그때 없던 항목이
"빠진 것"으로 보여 2행이 들어갔다. **출처는 간행물 PDF로 고정한다.** 그리고 행을
더하는 방향의 수정은 빼는 방향보다 조용하다 — 없는 분류가 붙어도 아무 화면에도
표시가 나지 않으므로, 근거 없이는 더하지 말 것.
