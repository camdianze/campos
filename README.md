# PharmaPOS

소규모 약국·보건시설을 위한 **재고 관리 및 POS(판매 시점 관리) 데스크톱 애플리케이션**입니다.

인터넷 연결이 불안정하거나 없는 환경을 전제로 설계되었습니다. 서버가 필요 없고, 모든 데이터는 PC 한 대의 로컬 SQLite 파일에 저장됩니다. 네트워크는 비밀번호 복구 메일 발송에만 선택적으로 사용됩니다.

---

## 주요 기능

### 재고 관리
- **배치(Batch) 단위 재고 추적** — 같은 상품이라도 배치번호별로 유통기한과 수량을 따로 관리합니다.
- **유통기한 임박 우선 출고** — 배치 목록이 항상 유통기한 오름차순으로 정렬되어, 먼저 만료될 재고부터 판매하도록 유도합니다.
- **입고(Stock-In)** — 배치번호·유통기한·수량 입력. 유통기한이 입고일보다 미래인지 검증합니다.
- **재고 조정(Adjustment)** — 파손·분실·실사 차이 등을 사유와 함께 기록하며, 모든 조정은 이력으로 남습니다.
- **알림** — 안전재고 미달(`LowStock`)과 유통기한 임박(`Expiry`)을 `Critical` / `Warning` / `Normal` 우선순위로 구분해 표시합니다.

### 판매 (POS)
- 바코드 또는 상품명 검색으로 장바구니 구성
- 결제 수단: 현금, 모바일 결제, 보험, 외상, 기타
- 현금 결제 시 받은 금액과 거스름돈 계산
- **원가 이하 판매 시 확인 요구** — 판매가가 원가보다 낮으면 경고 후 사용자 확인을 거칩니다.
- **판매 확정 시점의 재고 재검증** — 장바구니에 담은 뒤 다른 판매가 먼저 재고를 가져갔을 경우를 트랜잭션 내부에서 다시 확인합니다.
- 판매 이력 조회

### 상품 관리
- 상품 마스터 등록·수정 (일반명, 함량, 단위, 제조사, 원산지, 원가, 판매가, 안전재고)
- **내부 바코드 자동 생성** — 제조사 바코드가 없는 상품에 `INT-XXXXXXXX` 형식의 바코드를 발급합니다.

### 관리자 기능
- **관리자 대시보드** — 재고·판매 요약 지표
- **사용자 관리** — 계정 추가, 비밀번호 초기화, 상태 변경
- **백업 및 내보내기** — DB 파일 백업/복원, 테이블별 Excel(.xlsx)·CSV 내보내기
- **계정 복구 설정** — 보안 질문 및 복구 이메일 등록

### 계정
- 역할 구분: `FacilityStaff`(일반 직원) / `Administrator`(관리자)
- 아이디 찾기, 비밀번호 재설정(보안 질문 또는 이메일 OTP)

---

## 기술 스택

| 구분 | 내용 |
|---|---|
| 런타임 | .NET 10 |
| UI | WPF (`net10.0-windows`) |
| 데이터베이스 | SQLite (WAL 모드) |
| DI 컨테이너 | `Microsoft.Extensions.DependencyInjection` 10.0.9 |
| DB 접근 | `Microsoft.Data.Sqlite` 10.0.9 — **ORM 없이 raw ADO.NET** |
| 비밀번호 해싱 | `BCrypt.Net-Next` 4.2.0 |
| Excel 내보내기 | `ClosedXML` 0.105.0 |
| 암호화 | Windows DPAPI (`CurrentUser` 스코프) |

외부 의존성을 최소화한 것이 특징입니다. MVVM 툴킷, ORM, 네비게이션 프레임워크, 로깅 라이브러리를 쓰지 않고 필요한 부분만 직접 구현했습니다.

---

## 아키텍처

클린 아키텍처 기반의 4개 프로젝트로 구성되며, **의존성은 항상 안쪽을 향합니다.**

```
WPF ──────> Application ──> Domain
 └────────> DataAccess  ──> Application (인터페이스 구현)
                         └> Domain
```

| 프로젝트 | 역할 |
|---|---|
| `PharmaPOS.Domain` | 엔티티와 열거형. 의존성 없음, 로직 없음 |
| `PharmaPOS.Application` | 비즈니스 규칙, 서비스 구현체, 리포지토리 **인터페이스** |
| `PharmaPOS.DataAccess` | 리포지토리 **구현체**, 스키마 생성, 백업/내보내기 |
| WPF 프로젝트 | 화면(View/ViewModel), DI 조립, 플랫폼 종속 구현(DPAPI 등) |

리포지토리 인터페이스를 `Application`에 두고 구현을 `DataAccess`에 둠으로써 의존 방향을 뒤집었습니다. 그 결과 **Domain·Application·DataAccess 세 프로젝트는 플랫폼 독립적(`net10.0`)** 이며, Windows에 묶인 것은 WPF 프로젝트뿐입니다.

### 설계 규칙

- **예외 대신 결과 객체** — 서비스는 예외를 던지지 않고 `Success()` / `Failure(message)` / `NeedsConfirmation(message)` 를 반환합니다.
- **ViewModel은 화면을 전환하지 않습니다** — 이벤트만 발생시키고, 실제 전환은 View의 코드비하인드가 담당합니다.
- **모든 시각 값은 Unix epoch 밀리초**로 저장합니다.
- 코드 주석은 한국어, 사용자에게 보이는 문자열은 영어로 작성합니다.

---

## 시작하기

### 요구 사항

- Windows 10 이상
- .NET 10 SDK

### 빌드 및 실행

솔루션 파일 경로에 공백과 `&` 가 포함되어 있어 **PowerShell에서는 반드시 따옴표로 감싸야 합니다.**

```powershell
# 빌드
dotnet build "Lightweight Digital Inventory Management & POS System\Lightweight Digital Inventory Management & POS System.slnx"

# 실행
dotnet run --project "Lightweight Digital Inventory Management & POS System"

# 배포용 게시 (자체 포함 단일 실행 파일, win-x64)
dotnet publish "Lightweight Digital Inventory Management & POS System" -p:PublishProfile=FolderProfile
```

### 최초 실행

처음 실행하면 **초기 설정 화면**이 나타납니다. 여기서 시설 정보(약국 / 약품점 / 보건지소 / 보건소)와 첫 번째 관리자 계정을 등록하면 이후부터는 로그인 화면으로 진입합니다.

---

## 데이터 저장

데이터베이스는 첫 실행 시 아래 경로에 자동 생성됩니다.

```
%APPDATA%\PharmaPOS\pharmapos.db
```

이 폴더를 삭제하면 앱이 초기 설정 상태로 돌아갑니다. 개발 중 초기화가 필요할 때 사용하세요.

모든 연결에 `PRAGMA journal_mode = WAL` 과 `PRAGMA foreign_keys = ON` 이 적용됩니다.

> ⚠️ 백업·내보내기 기능으로 생성한 `.db` 파일에는 계정 해시와 판매 기록이 그대로 담깁니다. `.gitignore` 에서 `*.db` 를 제외하고 있으니 **저장소에 커밋하지 마세요.**

---

## 보안

- 비밀번호와 보안 질문 답변은 **bcrypt**로 해싱합니다. 평문은 저장하지 않습니다.
- 로그인 실패 시 "존재하지 않는 아이디"와 "비밀번호 불일치"를 **구분하지 않고** 동일한 메시지를 반환합니다.
- 복구 메일 발송용 SMTP 앱 비밀번호는 **Windows DPAPI**(`CurrentUser`)로 암호화해 저장합니다. 따라서 DB 파일만 다른 PC나 다른 Windows 계정으로 복사하면 이 값은 복호화되지 않습니다.
- 비밀번호 정책: 8자 이상, 영문자와 숫자 포함, 공백 불가, 아이디와 동일 불가

---

## 알려진 제한사항

- **테스트 코드가 없습니다.** 검증은 `dotnet build` 와 실제 실행에 의존합니다.
- **라벨 프린터 연동 미구현** — 입력값 검증까지만 수행합니다 (기종·프로토콜 미정).
- **영수증 출력 미구현** — 현재는 시뮬레이션 구현체가 동작합니다.
- **판매 비고(notes) 미저장** — `Stock_Transaction` 테이블에 해당 컬럼이 없습니다.
- **스키마 마이그레이션에 버전 관리가 없습니다.** 기존 테이블에 컬럼을 추가하려면 `CREATE TABLE` 문과 `ApplyMigrations()` 를 **양쪽 모두** 수정해야 합니다.
- **전이 의존성 취약점** — `SQLitePCLRaw.lib.e_sqlite3` 2.1.11에 High 등급 권고가 있으며, 현재 `NU1903` 경고가 억제되어 있습니다. 버전 갱신이 필요합니다.
