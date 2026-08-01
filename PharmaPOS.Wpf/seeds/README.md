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

## 알려진 한계 두 가지

### 1. 제형(route)에 따라 분류가 갈리는 항목

WHO 목록에는 같은 ATC 코드가 제형별로 다르게 분류된 항목이 있다.

| ATC | 성분 | 주사 | 경구 |
|---|---|---|---|
| `J01AA08` | Minocycline | RESERVE | WATCH |
| `J01XX01` | Fosfomycin | RESERVE | WATCH |

**상품 마스터에 제형 필드가 없어 둘을 구분할 수 없다.** 그래서 조회 시
더 강한 안내가 필요한 쪽(RESERVE)을 고른다 — 경구 제품을 RESERVE로 표시하는 것은
과한 경고에 그치지만, 주사 제품을 WATCH로 낮추면 필요한 경고를 놓치기 때문이다.
지표에서는 이 두 성분의 경구 판매가 RESERVE로 집계된다.
정확히 구분하려면 `Product_Master`에 제형 컬럼을 추가해야 한다.

(`J01CR02`도 두 행이 쓰지만 Amoxicillin/clavulanic acid와 Amoxicillin/sulbactam
둘 다 ACCESS라 분류에는 영향이 없다.)

### 2. `is_systemic`이 전 행 `true`다

WHO 목록은 전신 항생제만 다루므로, 이 파일에는 국소 제제가 아예 없다.
**즉 국소 제제 제외 경로가 이 데이터로는 동작하지 않는다.**

실무상 의미: 연고·점안액 같은 국소 항생제 상품에 **ATC 코드를 반드시 채워야 한다.**
예를 들어 Neomycin 연고에 `D06AX04`를 넣으면 목록에 없는 코드라 unmatched로
빠지지만, ATC를 비운 채 성분명만 "Neomycin"으로 두면 경구/주사용
Neomycin(WATCH) 행에 매칭돼 연고에도 복약안내지가 나간다.

국소 제제를 명시적으로 걸러내고 싶으면 해당 행을 `is_systemic=false`로 직접
추가하면 된다 — 프로그램은 이 컬럼 값을 그대로 신뢰한다. ATC 접두사(J01 등)로
자동 판별하지 않는 이유는 전신 항생제가 `A07AA`·`J04`·`P01AB` 등에도 걸쳐 있어
접두사 필터가 틀리기 때문이다.

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
