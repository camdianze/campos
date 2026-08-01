# AWaRe 시드 데이터

`aware_2025.csv`는 WHO AWaRe 분류표를 담는 참조 데이터 파일이다.
동봉본은 **실제 WHO 2025 목록 384행**이다 (ACCESS 93 / WATCH 145 / RESERVE 30,
합계 268개 분류 항생제 + NOT_RECOMMENDED 116개 복합제).

출처: WHO, *The selection and use of essential medicines, 2025: WHO AWaRe
classification of antibiotics for evaluation and monitoring of use* (2025-09-05)

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

상품 마스터에 제형 정보가 없어 구분할 수 없으므로, 조회는 더 강한 쪽(RESERVE)을
고른다. 경구를 RESERVE로 표시하는 것은 과한 경고에 그치지만, 주사를 WATCH로
낮추면 필요한 경고를 놓치기 때문이다.

384행 중 2행 이야기다. 이것 때문에 스키마에 제형 컬럼을 늘릴 이유는 없다.

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
