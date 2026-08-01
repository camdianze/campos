# AWaRe 시드 데이터

`aware_2025.csv`는 WHO AWaRe 분류표를 담는 참조 데이터 파일이다.
**이 저장소에 동봉된 파일은 헤더만 있는 빈 템플릿이다.** 실제 분류 데이터는
WHO 발행본에서 옮겨 채워야 한다. 분류값을 추정해서 채우면 안 된다.

출처: WHO, *The selection and use of essential medicines, 2025: WHO AWaRe
classification of antibiotics for evaluation and monitoring of use* (2025-09-05)

## 파일 형식

```csv
atc_code,antibiotic_name,aware_group,is_systemic,source_version
J01CA04,Amoxicillin,ACCESS,true,WHO AWaRe 2025
J01FA10,Azithromycin,WATCH,true,WHO AWaRe 2025
,"Amoxicillin/clavulanic acid, fixed-dose combination",NOT_RECOMMENDED,true,WHO AWaRe 2025
D06AX04,Neomycin,ACCESS,false,WHO AWaRe 2025
```

| 컬럼 | 설명 |
|---|---|
| `atc_code` | WHO ATC 코드. **비어 있어도 된다** — 고유 ATC가 부여되지 않은 고정용량복합제(FDC)가 있기 때문이다. 값이 있으면 성분명보다 우선해서 매칭에 쓰인다. |
| `antibiotic_name` | 항생제명. 필수. 쉼표가 들어가면 큰따옴표로 감싼다. |
| `aware_group` | `ACCESS` / `WATCH` / `RESERVE` / `NOT_RECOMMENDED` 넷 중 하나. 필수. **번역하지 않는다.** |
| `is_systemic` | `true`/`false` (`1`/`0`, `yes`/`no`도 인식). 전신 제제면 true. **false면 국소 제제로 보고 복약안내 대상에서 제외된다.** |
| `source_version` | 출처 표기. 인쇄물과 로그에 그대로 찍힌다. 예: `WHO AWaRe 2025`. |

- 컬럼 순서는 상관없다 (헤더 이름으로 찾는다). 인코딩은 UTF-8.
- **`NOT_RECOMMENDED` 그룹을 빠뜨리지 말 것.** 복합제가 여기에 속하는데,
  이 행들이 없으면 정작 안내가 가장 필요한 상품에서 기능이 동작하지 않는다.
- `is_systemic`은 WHO 원본에 없는 컬럼이다. 제형 정보를 보고 채워야 한다.
  ATC 접두사(J01 등)로 자동 판별하지 않는 이유는 전신 항생제가
  A07AA·J04·P01AB 등에도 걸쳐 있어 접두사 필터가 틀리기 때문이다.
  코드는 이 컬럼 값을 그대로 신뢰한다.

## 파일을 놓는 위치

앱은 다음 순서로 찾고, 먼저 발견한 파일 하나만 쓴다.

1. `%APPDATA%\PharmaPOS\seeds\aware_2025.csv` — 현장 교체용. 재빌드 없이 갱신 가능.
2. `(설치 폴더)\seeds\aware_2025.csv` — 빌드에 동봉되는 기본본.

파일 내용이 이전 실행과 같으면 다시 적재하지 않는다. AWaRe 개정판이 나오면
1번 자리에 새 파일을 놓고 앱을 재시작하면 된다.

## 적재 실패 시 동작

파일이 없거나 읽을 수 없어도 **앱은 정상 실행되고 판매도 정상 진행된다.**
참조 데이터가 없으면 모든 상품이 unmatched로 기록될 뿐이다.
적재 상태는 설정 화면에서 확인할 수 있다.
