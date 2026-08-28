# PharmaPOS 화면별 입력 필드 · 목록 · 저장 흐름

매뉴얼 작성용으로 `.xaml`과 그에 딸린 ViewModel·서비스를 읽어 정리한 것이다. 코드는 수정하지 않았다.
기준 시점: 작업 트리 현재 상태 (`main`, 최근 커밋 `6a36a26` + 미커밋 변경분).

라벨·버튼·항목 문자열은 전부 코드에 있는 그대로다. 별표(`*`), 이모지, 화살표(`←`), 전각 기호(`＋`)까지 원문이므로 매뉴얼에 그대로 옮겨도 된다.

> 화면 이동 경로·오류 메시지 전문·파일 경로·권한표는 [manual-inventory.md](manual-inventory.md)에 있다. 이 문서는 **입력 칸 자체**를 다룬다.
> (manual-inventory.md는 커밋 `5830ffa` 기준이라, 그 뒤 바뀐 부분은 이 문서 쪽이 최신이다 — 특히 Sales History의 Refund 버튼, Backup/Export 화면 개편, 셸 하단 버튼 구성.)

---

## 0. 모든 화면에 공통으로 해당하는 것

매뉴얼을 쓰기 전에 알고 있어야 할 전제다.

| 항목 | 실제 동작 |
|---|---|
| **자리표시자(Placeholder) 문구는 하나도 없다** | 코드 전체에 watermark/placeholder 구현이 없다. 빈 입력칸은 그냥 빈 칸이다. 특히 **검색 상자에는 라벨도 자리표시자도 없어**, 화면만 보고는 무엇을 치는 칸인지 알 수 없다 (Products, Inventory Status, Sales History, Adjustment History, User Management). 매뉴얼에서 "여기에 상품명 또는 바코드를 입력합니다"처럼 보충해 주어야 한다. |
| **툴팁은 딱 두 개** | 재고 화면 상품명 앞의 AWaRe 색점(분류명이 뜬다), 메인 셸 직원 화면의 잠긴 `🔒 Reports` 버튼(`Administrators only.`). 그 외에는 없다. |
| **라벨의 `*` 표시는 일관되지 않다** | 실제로 필수인데 `*`가 없는 칸이 많다(초기 설정 1단계 전부, 보안 질문/답, 입고 배치번호·수량 등). 이 문서의 "필수" 열은 화면 표기가 아니라 **코드가 실제로 막는지**를 기준으로 적었다. |
| **검증 시점** | 입력 중에는 아무 검증도 하지 않는다. **저장/확인 버튼을 눌러야** 한 번에 검사하고, 첫 번째로 걸린 항목의 메시지 하나만 화면 하단(또는 패널 하단)에 빨간 글씨로 표시한다. 입력칸에 붉은 테두리가 생기거나 하지 않는다. |
| **숫자 칸도 전부 문자열 TextBox** | 스피너·숫자 전용 입력이 아니다. 문자를 쳐도 막히지 않고, 저장할 때 "숫자가 아니다"로 걸린다. |
| **비밀번호 규칙 안내문** | 비밀번호를 새로 정하는 모든 칸 아래에 같은 문구가 붙는다 — `At least 8 characters, including one letter and one number. Special characters are allowed. Spaces are not.` (출처: [PasswordPolicyValidator.RuleSummary](../PharmaPOS.Application/PasswordPolicy/PasswordPolicyValidator.cs#L20)) |
| **대화상자** | 시스템 MessageBox는 쓰지 않는다. 전부 자체 [AppDialog](../PharmaPOS.Wpf/Views/AppDialog.xaml.cs)다. |
| **ComboBox 항목은 enum 이름 그대로 표시된다** | `DrugShop`, `MobilePayment`, `Within7Days`, `NonMedicine`처럼 띄어쓰기 없이 나온다. 사람이 읽기 좋은 표시명으로 바꾸는 변환기가 없다. |

### 0-1. `Strength` · `Dosage Form` · `Unit`은 서로 다른 값이다

매뉴얼에서 가장 오해가 잦은 지점이라 먼저 못박아 둔다.

| 값 | 뜻 | 입력 | 예 |
|---|---|---|---|
| `Strength` | 함량 | 자유 입력, 선택 | `500 mg`, `800/160`, `0.1%` |
| `Dosage Form` | **약의 형태** | **고정 목록**, 선택 | `Tablet`, `Syrup`, `Injection`, `Ointment` |
| `Unit` | **낱개 하나를 세는 이름** | 자유 입력, **필수** | `Tablet`, `Bottle`, `Tube`, `Piece` |

**고형 경구약에서만 뒤 둘이 우연히 같습니다.**

| 상품 | Strength | Dosage Form | Unit |
|---|---|---|---|
| Amoxicillin 정제 | `500 mg` | `Tablet` | `Tablet` ← 같음 |
| Amoxicillin 시럽 60mL | `125 mg/5 mL` | `Syrup` | `Bottle` ← 다름 |
| Gentamicin 연고 | `0.1%` | `Ointment` | `Tube` ← 다름 |
| 3M N95 마스크 | — | (없음) | `Piece` ← 제형 자체가 없음 |

`Unit`이 필수인 이유: 화면이 `Tablets Per Box`처럼 **복수형으로** 쓰므로 셀 수 있는 이름이어야 하고, 제형이 없는 비의약품에도 값이 필요합니다. 그래서 `Unit`에 `Syrup`을 적으면 `Syrups Per Box`가 되어 어색해집니다 — 그 칸에는 `Bottle`을 적습니다.

---

## 1. Activate PharmaPOS — [LicenseActivationView.xaml](../PharmaPOS.Wpf/Views/LicenseActivationView.xaml)

첫 실행 시 1회. 제목 아래 설명: `Enter the license code that came with your copy. This is a one-time step and works without an internet connection.`

### 입력 필드

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 비고 |
|---|---|---|---|---|---|
| 1 | `License Code` | TextBox (여러 줄, 높이 88, 자동 줄바꿈) | ✔ | 빈 칸 | 화면이 열리면 **자동으로 커서가 들어간다**. 코드가 124자라 줄바꿈·공백이 섞여도 무시한다 |

### 목록/표
없음.

### 저장 흐름

1. `Load from file…` — (선택) 열기 대화상자. 필터 `License file (*.txt;*.lic)` / `All files (*.*)`. 고른 파일 내용을 통째로 읽어 위 칸에 넣는다. 읽기 실패 시 `The selected file could not be read.`
2. `Activate` (또는 입력칸에서 **Enter**) — `LicenseService.Activate(코드)` 호출.
   - 성공 → 즉시 다음 화면(초기 설정 전이면 Initial Setup, 아니면 Login).
   - 실패 → 버튼 위에 사유 표시. **입력칸을 한 글자라도 고치면 오류 문구가 자동으로 사라진다.**

---

## 2. Initial Facility Setup / Login Setup — [InitialSetupView.xaml](../PharmaPOS.Wpf/Views/InitialSetupView.xaml)

2단계 마법사. 최초 1회만 나온다.

### 2-1. 1단계 `Initial Facility Setup`

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|
| 1 | `Facility Name` | TextBox | ✔ | 빈 칸 | |
| 2 | `Country` | TextBox | ✔ | 빈 칸 | |
| 3 | `Province / District` | TextBox | ✔ | 빈 칸 | |
| 4 | `Facility Type` | ComboBox | ✔ | **`Pharmacy`** (미리 선택돼 있음) | `Pharmacy` / `DrugShop` / `HealthPost` / `HealthCenter` |

> 네 칸 모두 라벨에 `*`가 없지만 1~3번은 실제로 필수다.

**`Next`** → 1·2·3번을 순서대로 검사(빈 칸이면 그 자리에서 멈추고 메시지) → 통과하면 2단계로. 값은 아직 저장되지 않는다.

### 2-2. 2단계 `Login Setup`

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|
| 1 | `ID*` | TextBox | ✔ | 빈 칸 | |
| 2 | `Password*` | PasswordBox | ✔ | 빈 칸 | 아래에 비밀번호 규칙 안내문 |
| 3 | `Confirm Password*` | PasswordBox | ✔ | 빈 칸 | |
| 4 | `Security Question` | ComboBox | ✔ (라벨엔 `*` 없음) | **선택 없음(빈 상태)** | `What was the name of your first pet?`<br>`What is your mother's maiden name?`<br>`What was the name of your first school?`<br>`What city were you born in?` |
| 5 | `Answer` | TextBox | ✔ (라벨엔 `*` 없음) | 빈 칸 | 저장 시 `Trim().ToLowerInvariant()`로 정규화 후 해시 |

### 저장 흐름

1. `Back` — 1단계로 되돌아간다(입력값은 유지).
2. `Next` — 이 버튼이 실제 **설정 완료** 버튼이다. code-behind가 PasswordBox 2개와 Answer 값을 모아 `CompleteSetupAsync`를 부른다.
3. 서비스 검증 순서: 시설명 → 국가 → 지역 → 아이디 → 비밀번호 → 확인 비밀번호 → 두 비밀번호 일치 → 비밀번호 규칙 → 보안질문 → 답변 → 아이디 중복.
4. 통과하면 **Facility와 첫 Administrator 계정이 한 트랜잭션으로 저장**되고, 초록색 `Initial setup completed successfully.` 후 로그인 화면으로 넘어간다.

여기서 만들어지는 계정이 시스템의 첫 관리자다.

---

## 3. Login — [LoginView.xaml](../PharmaPOS.Wpf/Views/LoginView.xaml)

제목 `Login` / 부제 `Sign in to continue`. 좌측 하단에 앱 버전.

### 입력 필드

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `ID` | TextBox | ✔ | **직전에 `Remember me`로 저장한 아이디**가 있으면 자동으로 채워진다 (`%APPDATA%\PharmaPOS\remember_me.txt`) |
| 2 | `Password` | PasswordBox | ✔ | 빈 칸 |
| 3 | `Remember me` | CheckBox | — | 저장된 아이디가 있으면 **켜진 상태**, 없으면 꺼짐 |

### 저장 흐름

1. `Login` (또는 아이디/비밀번호 칸에서 **Enter** — 버튼이 `IsDefault`) →
2. 아이디 빈 칸 검사 → 비밀번호 빈 칸 검사 → `LoginAsync`.
3. 성공하면 **먼저 아이디 저장 여부를 처리한 뒤**(`Remember me` 켜짐 = 파일에 기록, 꺼짐 = 파일 삭제) 메인 셸로 이동.
4. 실패 시 하단에 사유. 아이디가 없든 비밀번호가 틀리든 **같은 문구**(`Invalid username or password.`)를 낸다.

그 밖의 버튼: `Forgot Username?` → Find ID, `Forgot Password?` → Find Password.

---

## 4. Find ID — [FindUsernameView.xaml](../PharmaPOS.Wpf/Views/FindUsernameView.xaml)

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `E-mail` | TextBox | ✔ | 빈 칸 |

**흐름**: `Confirm` → 빈 칸 검사 → 메일 발송 시도 → **등록 여부와 무관하게 항상 같은 안내**를 파란 글씨로 표시한다 (`If this email is registered, we've sent your username to it. Please check your inbox.`). 의도된 보안 설계다. `Cancel` → 로그인 화면.

---

## 5. Find Password — [PasswordRecoveryView.xaml](../PharmaPOS.Wpf/Views/PasswordRecoveryView.xaml)

3단계. 화면 제목이 단계마다 바뀐다.

### 5-1. 1단계 `Find Password`

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `ID` | TextBox | ✔ | 빈 칸 |

`Confirm` → 계정의 복구 수단 조회 → 수단이 하나도 없으면 진행 불가(`No recovery method is available for this account…`). 있으면 2단계로. **보안질문이 있으면 보안질문 방식이 기본**, 없고 이메일만 되면 이메일 방식으로 열린다.

### 5-2. 2단계 `Verify Identity` — 보안질문 방식

제목 아래에 아이디가 표시되고, 그 아래 등록된 질문 문장이 나온다.

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | (질문 문장 — 읽기 전용 텍스트) | TextBlock | — | 계정에 등록된 질문 |
| 2 | `Answer` | TextBox | ✔ | 빈 칸 |

버튼: `Use Email OTP instead` (이메일 복구가 가능한 계정에만 보임) / `Back` (→ 1단계) / `Confirm`.

### 5-3. 2단계 `Verify Identity` — 이메일 OTP 방식

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `E-mail` | (읽기 전용 텍스트) | — | 가려진 이메일 주소 |
| 2 | `Code` | TextBox | ✔ | 빈 칸 |

버튼 순서: `Send Code` (코드 칸 **위**에 있다) → 코드 입력 → `Back` (→ 1단계) / `Confirm`. `Use Security Question instead`는 보안질문이 등록된 계정에만 보인다.
`Send Code`를 누르면 `A recovery code has been sent to your email.` 코드 유효시간은 10분.

### 5-4. 3단계 `New Password`

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `New Password` | PasswordBox | ✔ | 빈 칸 |
| 2 | `Confirm Password` | PasswordBox | ✔ | 빈 칸 |

버튼: `Back` (→ 2단계) / `Reset Password`.

**흐름**: `Reset Password` → 앞 단계에서 받은 검증 토큰이 있는지 확인(없으면 `Please verify your identity first.`) → `ResetPasswordAsync` → 성공하면 **곧바로 로그인 화면**으로 나간다(성공 메시지는 따로 없다).

**`Back`으로 되돌릴 때 지우는 범위가 단계마다 다르다.**

- **3 → 2**: 적었던 답/코드만 비운다. 검증 토큰은 남는다 — 이메일 OTP는 한 번 맞히면 그 코드가 소비되므로, 토큰까지 버리면 그냥 뒤를 확인하려던 사람도 코드를 다시 받아야 한다. 3단계로 다시 가려면 어차피 2단계의 `Confirm`을 통과해야 한다.
- **2 → 1**: 아이디를 바꿀 수 있는 자리로 돌아가므로 **검증 토큰까지 버린다.**

> 복구 상태(OTP·검증 토큰)는 메모리에만 있다. 도중에 앱을 끄면 처음부터 다시 해야 한다.

---

## 6. My Page — [MyPageView.xaml](../PharmaPOS.Wpf/Views/MyPageView.xaml)

입력 칸 없음. 계정 카드(이니셜 원 · 아이디 · 역할)와 메뉴 두 개.

- `Change Password` — 부제 `Set a new password for this account.`
- `Recovery Settings` — 부제 `Security question and recovery email, used when you forget your password.`
- `← Back` → 메인 셸

---

## 7. Change Password — [ChangePasswordView.xaml](../PharmaPOS.Wpf/Views/ChangePasswordView.xaml)

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `Current Password` | PasswordBox | ✔ | 빈 칸 |
| 2 | `New Password` | PasswordBox | ✔ | 빈 칸 (아래에 비밀번호 규칙 안내문) |
| 3 | `Confirm New Password` | PasswordBox | ✔ | 빈 칸 |

**흐름**: `Change Password` → 세 값을 모아 `ChangePasswordAsync` → 성공하면 초록색 `Password changed successfully. Please log in again.`을 띄우고 **즉시 로그인 화면으로 강제 이동**(세션 유지 불가). `Cancel`은 My Page로.

---

## 8. Recovery Settings — [RecoverySettingsView.xaml](../PharmaPOS.Wpf/Views/RecoverySettingsView.xaml)

제목 아래 설명: `Set up at least one recovery method in case you forget your password.`
`SECURITY QUESTION` / `RECOVERY EMAIL` 두 구역으로 나뉜다.

### 입력 필드 (배치 순서대로)

| # | 구역 | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|---|
| 1 | SECURITY QUESTION | `Question` | ComboBox | 조건부 | 선택 없음 | 초기 설정과 같은 4개:<br>`What was the name of your first pet?`<br>`What is your mother's maiden name?`<br>`What was the name of your first school?`<br>`What city were you born in?` |
| 2 | SECURITY QUESTION | `Answer` | TextBox | 조건부 | 빈 칸 | |
| 3 | RECOVERY EMAIL | `Email Address` | TextBox | 조건부 | 빈 칸 | |
| 4 | RECOVERY EMAIL | `Email Provider` | ComboBox | 조건부 | 선택 없음 | `Gmail` / `Outlook` / `Other` |
| 5 | RECOVERY EMAIL | `App Password` | PasswordBox | 조건부 | 빈 칸 | DPAPI로 암호화 저장 |
| 6 | RECOVERY EMAIL | `SMTP Host` | TextBox | `Other`일 때 ✔ | 빈 칸 | **Provider가 `Other`일 때만 화면에 나타난다** |
| 7 | RECOVERY EMAIL | `SMTP Port` | TextBox | 조건부 | 빈 칸 | 같은 조건으로 나타난다. 숫자여야 한다 |

> "조건부"란: 개별 칸은 비워 둘 수 있지만, **보안질문과 이메일 중 최소 하나는 완성해야** 저장된다 (`Please set up at least one recovery method.`). Gmail/Outlook은 SMTP 주소·포트가 고정값으로 자동 설정된다.

### 저장 흐름

1. `Save` → code-behind가 `App Password` 값을 ViewModel에 옮겨 담는다.
2. `Other` 제공자이고 포트를 적었으면 숫자 변환 검사 (`Please enter a valid SMTP port number.`).
3. `SaveRecoverySettingsAsync` → 성공 시 `Recovery settings saved successfully.` (화면은 그대로 유지)
4. `← Back` → My Page.

---

## 9. Products — [ProductListView.xaml](../PharmaPOS.Wpf/Views/ProductListView.xaml)

상품 목록 + **하단 입고(Stock-IN) 패널**이 한 화면에 있다. 별도의 입고 전용 화면은 없다.

### 9-1. 검색·필터 (표 바로 위, 왼쪽부터)

| 컨트롤 | 라벨 | 기준 | 선택지 / 기본값 |
|---|---|---|---|
| TextBox | **없음** | 상품 검색 (repository의 `SearchAsync`로 넘어간다) | 빈 칸. **한 글자 칠 때마다 즉시 재조회** |
| ComboBox | **없음** | 상태 | `All` / `Active` / `Inactive` — 기본 **`All`**. 바꾸면 즉시 재조회 |

결과가 없으면 하단에 `No products found.`

### 9-2. 표 컬럼 (왼쪽부터)

| # | 머리글 | 내용 |
|---|---|---|
| 1 | `Product Name` | 이름 **앞**에 WHO AWaRe 분류 색점. 초록 ACCESS · 노랑 WATCH · 빨강 RESERVE · 검정 NOT RECOMMENDED. 항생제가 아니거나 참조 목록에 없으면 점이 없다. 점 위에 마우스를 올리면 분류명이 뜬다 |
| 2 | `Generic Name` | |
| 3 | `Strength` | `500 mg` 등 |
| 4 | `Dosage Form` | 제형. `Tablet` / `Syrup` / `Injection` … 비어 있을 수 있다 |
| 5 | `Unit` | **낱개를 세는 단위.** 제형이 아니다 — 0-1번 항목 참고 |
| 6 | `Barcode` | 제조사 바코드 |
| 7 | `Internal Barcode` | |
| 8 | `Cost Price` | |
| 9 | `Selling Price` | |
| 10 | `Safety Stock` | |
| 11 | `Status` | `Active` / `Inactive` |

표는 읽기 전용, 한 줄만 선택 가능.

### 9-2-1. 우클릭 메뉴

| 항목 | 동작 |
|---|---|
| `View Details` | 사진과 모든 값이 있는 **Product 화면**으로 간다. 하단 `Edit` 버튼과 같은 화면이다 |

빈 곳을 우클릭하면 메뉴가 뜨지 않는다. 우클릭한 줄이 곧바로 선택된다.

### 9-3. 입고(Stock-IN) 패널 — 표에서 상품을 고르면 펼쳐진다

패널 머리에 `Stock-IN` + 고른 상품명. **다른 상품으로 옮기면 입력값이 전부 초기화된다.**

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 비고 |
|---|---|---|---|---|---|
| 1 | `Batch Number` | TextBox | ✔ | 빈 칸 | |
| 2 | `Expiry Date` | DatePicker | ✔ | **오늘 + 1년** | 입고일보다 뒤여야 한다 |
| 3 | `Stock-IN Date` | DatePicker | ✔ | **오늘** | |
| 4 | `Quantity` 또는 `Boxes` | TextBox | ✔ | 빈 칸 | **박스/낱개를 나눠 파는 상품이면 라벨이 `Boxes`로 바뀌고, 이 값은 박스 개수다.** 아래에 `10 box(es) × 30 = 300 units.` 형태의 환산 미리보기가 붙는다 |

### 9-4. 저장 흐름

**입고**
1. 표에서 상품을 고른다 → 패널이 열린다.
2. 네 칸을 채우고 패널의 `Save`.
3. 화면 단 검사: 수량이 정수인가 (`Quantity must be a whole number.` / 박스 상품이면 `Box quantity must be a whole number.`).
4. `StockInService` 검사 순서: 상품 선택 → 상품 존재 → 상품이 Active인가 → 배치번호 빈 칸 → **유효기한이 입고일보다 뒤인가** → 수량 > 0.
5. 성공하면 **패널이 닫히고** 화면 하단에 `Stock-in saved for {상품명}: {n} box(es), {m} units.` (낱개 상품은 `Stock-in saved for {상품명}.`)
   - 원장에는 낱개 기준 총량으로 기록된다. 10박스 × 30개 입고는 300으로 남는다.
6. 패널의 `Cancel`은 입력값을 비우고 패널을 닫는다.

**그 밖의 버튼** (하단 한 줄)
- `← Back` — **들어온 곳으로 돌아간다.** 재고 화면의 "View Product Details"로 왔으면 Inventory Status, 알림 화면의 "Go to Product"로 왔으면 Alerts, 그 외에는 메인 셸.
- `Add Product` → 빈 Product 화면
- `Edit` → 고른 상품으로 채워진 Product 화면 (선택 없으면 `Please select a product.`)
- `Deactivate` — **확인 대화상자 없이 즉시** 비활성화하고 목록을 다시 읽는다
- `Print Barcode` → Internal Barcode 화면

---

## 10. Product (등록/수정) — [ProductEditView.xaml](../PharmaPOS.Wpf/Views/ProductEditView.xaml)

좌우 2단. 왼쪽 = "이 약이 무엇인지", 오른쪽 = "얼마에 얼마나 파는지". 신규 등록과 수정이 같은 화면이다.

이 화면이 **상세 보기이자 수정 화면**이다. 목록의 `Edit` 버튼과 우클릭 `View Details`가 모두 여기로 온다.

### 10-0. 사진 칸 (맨 왼쪽)

| 라벨 | 컨트롤 | 비고 |
|---|---|---|
| (사진 자리) | 240×240 이미지 | 사진이 없으면 **빈 회색 자리**. 아이콘·문구 없음 |
| `Photo updated {yyyy-MM-dd}` | 읽기 전용 | 사진이 없거나 넣은 시각을 모르면 빈 줄 |
| `Set Photo` | 파일 선택 (jpg/jpeg/png/bmp) | **누르는 즉시 저장된다** |
| `Remove` | | 즉시 지워진다 |

**사진은 `Save`와 무관하게 즉시 저장·삭제됩니다.** 나머지 칸은 `Save`를 눌러야 반영됩니다. 그래서 사진을 바꾸고 `Cancel`을 눌러도 사진은 남고, 정보만 고쳐 저장해도 사진은 지워지지 않습니다.

**신규 등록 중에는 두 버튼이 꺼져 있고** `Save the product first, then reopen it to add a photo.`가 뜬다 — 사진은 상품 ID에 매달아 저장하는데 그 ID가 저장하는 순간 생긴다.

저장 시 긴 변 800px로 줄이고 JPEG로 다시 압축한다. 원본 20MB 초과는 거부한다.

### 10-1. 가운데 단

| # | 구역 | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|---|
| 1 | IDENTIFICATION | `Product Name *` | TextBox | ✔ | 빈 칸 | |
| 2 | IDENTIFICATION | `Generic Name` | TextBox | — | 빈 칸 | 항생제 복약안내 매칭에 쓰인다 |
| 3 | IDENTIFICATION | `Strength` | TextBox | — | 빈 칸 | `500 mg`, `800/160` 등 |
| 4 | IDENTIFICATION | `Dosage Form (optional)` | ComboBox | — | **빈 항목** | **(빈 항목 = 아직 정하지 않음)** / `Tablet` / `Capsule` / `Syrup` / `Suspension` / `Powder` / `Injection` / `Infusion` / `Ointment` / `Cream` / `Drops` / `Suppository` / `Inhaler` / `Other` |
| 5 | IDENTIFICATION | `Unit *` | TextBox | ✔ | 빈 칸 | **제형이 아니라 낱개를 세는 단위다.** 여기 적은 말이 아래 라벨에 그대로 들어간다 (`Sachet` → `Sachets Per Box *`). 안내문: `How one piece is counted and sold — Tablet, Bottle, Tube. Not the dosage form above.` |
| 6 | IDENTIFICATION | `Category (optional)` | ComboBox | — | **빈 항목** | **(빈 항목 = 아직 정하지 않음)** / `Medicine` / `NonMedicine` |
| 7 | SOURCE | `Manufacturer` | TextBox | — | 빈 칸 | |
| 8 | SOURCE | `Country of Origin` | TextBox | — | 빈 칸 | |
| 9 | ANTIBIOTIC | `ATC Code (antibiotics only)` | TextBox | — | 빈 칸 | 안내문: `Used to look up the WHO AWaRe group for counselling sheets. Leave empty for non-antibiotics.` 저장 시 대문자로 변환 |
| 10 | ANTIBIOTIC | `Fixed-dose combination product` | CheckBox | — | **꺼짐** | |

### 10-2. 오른쪽 단

| # | 구역 | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|---|
| 11 | BARCODE | `Manufacturer Barcode` | TextBox | — | 빈 칸 | 중복이면 `This barcode is already registered.` |
| 12 | BARCODE | `Internal Barcode (auto-generated if empty)` | TextBox **읽기 전용** | — | 기존 값 또는 빈 칸 | 비어 있으면 저장할 때 자동 생성 |
| 13 | PRICE AND STOCK | `Cost Price *` | TextBox | ✔ | 빈 칸 | 0보다 커야 한다 |
| 14 | PRICE AND STOCK | `Selling Price *` | TextBox | ✔ | 빈 칸 | 0보다 커야 한다. 아래 안내문이 **소분 판매 여부에 따라 바뀐다**: 켜짐 → `Cost price and selling price above are for one box.` / 꺼짐 → `Cost price and selling price above are for one {단위}.` |
| 15 | PRICE AND STOCK | `Safety Stock Level *` | TextBox | ✔ | 빈 칸 | 음수 불가 |
| 16 | PRICE AND STOCK | `Status *` | ComboBox | ✔ | **`Active`** | `Active` / `Inactive` |
| 17 | PRICE AND STOCK | `Sell loose units` | CheckBox | — | **꺼짐** (기존 상품은 박스당 개수가 2 이상이면 켜진 상태로 열린다) | 켜면 아래 3칸이 나타난다 |

### 10-3. 소분 판매를 켰을 때만 나오는 칸 (회색 상자 안)

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 비고 |
|---|---|---|---|---|---|
| 18 | `{단위}s Per Box *` (예: `Tablets Per Box *`) | TextBox | ✔ | **`1`**. 단, 체크박스를 켜는 순간 값이 `1`이면 **빈 칸으로 지워진다** | **2 이상**이어야 한다. 아니면 `Enter how many {단위}s are in one box (2 or more).` |
| 19 | `Loose Unit Price` | TextBox | — | 빈 칸 | 비워 두면 박스가 ÷ 박스당 개수로 자동 계산. 안내문이 실제 계산값을 보여준다: `Leave empty to sell one Tablet at 100 (3000 ÷ 30).` 값을 적으면 `One Tablet is sold at this price.` |
| 20 | `Unit Barcode` | TextBox **읽기 전용** | — | `내부바코드 + -EA` / 신규 상품은 `Generated on save.` | 안내문: `Scan this to sell one loose unit. The manufacturer barcode still sells a whole box.` |

### 10-4. 저장 흐름

1. `Save` →
2. **화면 단 숫자 변환 검사** (원가 → 판매가 → 기준 재고 순). 숫자가 아니면 그 항목의 메시지로 걸린다.
3. 소분 판매가 켜져 있으면: 박스당 개수 정수·2 이상 검사 → 낱개가를 적었다면 숫자 검사 (`Loose unit price must be a number.`).
4. `ProductService` 검증 순서: 상품명 → 단위 → 원가>0 → 판매가>0 → 기준 재고≥0 → 박스당 개수≥1 → 낱개가>0.
5. **판매가 < 원가이면** 확인 대화상자 `Confirm` / `Selling price is lower than cost price. Continue?` (Yes/No). Yes를 누르면 그대로 다시 저장을 시도한다.
6. 바코드 중복 검사 → 필요하면 내부 바코드 자동 생성 → INSERT 또는 UPDATE.
7. 성공하면 **Products 목록 화면으로 자동 복귀**한다 (성공 메시지는 없다).
8. `Cancel`도 목록으로 돌아간다.

> 소분 판매를 끄면 박스당 개수는 1로, 낱개가는 비워진 채로 저장된다 — 나중에 다시 켜도 옛 낱개가가 되살아나지 않는다.

---

## 11. Internal Barcode — [InternalBarcodeView.xaml](../PharmaPOS.Wpf/Views/InternalBarcodeView.xaml)

제목 아래에 대상 상품명이 표시된다.

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|
| 1 | `Internal Barcode` | TextBox **읽기 전용** | — | 상품의 현재 내부 바코드 (없으면 빈 칸) | |
| 2 | `Label Quantity` | TextBox | ✔ | **`1`** | |
| 3 | `Printer` | ComboBox | — | 선택 없음 | `Label Printer 1 (Placeholder)` / `Label Printer 2 (Placeholder)` — **실제 프린터 목록이 아니라 화면 확인용 더미다** |

### 흐름

- `Generate` — 내부 바코드가 **이미 있으면** `Internal barcode already exists.`로 거절. 없을 때만 새로 발번해서 1번 칸에 채운다.
- `Print Label` — 라벨 수량 정수 검사 후 `PrintLabelAsync` 호출. **실제 하드웨어 출력은 아직 구현되지 않았고**, 입력값만 검증하고 결과 메시지를 돌려준다.
- `← Back` → Products.

---

## 12. Inventory Status — [InventoryStatusView.xaml](../PharmaPOS.Wpf/Views/InventoryStatusView.xaml)

제품 행을 펼치면 배치 행이 나오는 2단 트리 표. 아래에 **조정 패널**과 **입고 패널**이 같은 자리를 나눠 쓴다(둘 중 하나만 열린다).

### 12-1. 검색·필터 (표 위, 왼쪽부터)

| # | 컨트롤 | 라벨 | 기준 | 선택지 / 기본값 |
|---|---|---|---|---|
| 1 | TextBox | **없음** | 상품 검색 | 빈 칸. 한 글자마다 즉시 재조회 |
| 2 | ComboBox | **없음** | 유효기한 | `All` / `Expired` / `Within7Days` / `Within30Days` / `Within90Days` — 기본 **`All`** |
| 3 | CheckBox | `Low Stock Only` | 기준 재고 미만만 보기 | 기본 **꺼짐** |
| 4 | ComboBox | **없음** | 정렬 | `ProductName` / `Quantity` / `ExpiryDate` — 기본 **`ProductName`** |

넷 다 바꾸는 즉시 목록을 다시 읽는다. 결과가 없으면 `No inventory records found.`

### 12-2. 표 컬럼 (왼쪽부터)

머리글은 대문자다.

| # | 머리글 | 제품 행 | 배치 행 |
|---|---|---|---|
| 1 | `PRODUCT` | 상품명 (저재고면 빨강) | 배치번호 (들여쓰기) |
| 2 | `STOCK` | `현재고 / 기준재고`. 박스 상품이면 아래에 `📦 박스수  💊 낱개수` | 그 배치의 현재고 (+ 박스/낱개 내역) |
| 3 | `PRICE` | 낱개 기준 단가 (`$0.00` 형식) | **비어 있음** (판매가는 제품 단위 값이라) |
| 4 | `EXPIRY` | 배치 중 **가장 이른** 만료일 | 그 배치의 만료일 |
| 5 | `STATUS` | 배지 — `Expired`(빨강) / `Low`(연빨강) / `Expiring`(노랑). 셋이 동시에 뜰 수 있다 | `Expired` / `Expiring`만. **`Low`는 제품 행에만** 나온다 |

### 12-3. 우클릭 메뉴 (표 위에서)

빈 곳을 우클릭하면 메뉴가 뜨지 않는다. 우클릭한 줄이 곧바로 선택된다.

| 항목 | 활성 조건 | 동작 |
|---|---|---|
| `Sell in POS` | 제품 행 또는 배치 행 선택 | 그 상품이 미리 담긴 POS 판매 화면으로 |
| `View Product Details` | 〃 | 그 상품이 선택된 Products 화면으로 (`← Back`으로 여기 돌아온다) |
| `Stock-in` | 〃 | 아래 입고 패널을 편다 |
| `Adjustment` | 〃 | 아래 조정 패널을 편다 |
| `Delete Batch` | **배치 행**을 골랐을 때만 | 빈 배치 삭제 |

하단 버튼(`Stock-in` / `Adjustment` / `Delete Batch`)은 이 메뉴와 **완전히 같은 동작**이다.

### 12-4. 조정(Adjustment) 패널 — 배치 행을 고르고 `Adjustment`를 눌렀을 때

패널 머리: `Adjustment` + `{상품명} · Batch {배치번호}` (배치번호가 없으면 `no batch number`).
**제품 행만 고른 상태로 누르면** 열리지 않고 `Please expand the product and select the batch you want to adjust.`가 뜬다.

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 비고 |
|---|---|---|---|---|---|
| 1 | `Batch Number` | TextBox | — | **고른 배치의 번호** | **고칠 수 있다.** 초기 재고를 번호 없이 넣은 뒤 나중에 번호를 붙이는 자리가 여기뿐이다. 비워 두면 번호 없는 상태로 저장된다. 같은 상품의 다른 배치와 겹치면 거절 |
| 2 | `System Quantity` | TextBox **읽기 전용** | — | 전산 재고 | 박스 상품이면 아래에 `System: 3 box(es) of 30 + 12 loose unit(s).` |
| 3 | `Physical Count — Unopened Boxes` | TextBox | — | **`0`** | **박스/낱개 상품일 때만 나타난다** |
| 4 | `Physical Count` 또는 `Physical Count — Loose Units` | TextBox | ✔ | 빈 칸 | 박스 칸이 함께 보일 때만 라벨이 `— Loose Units`로 갈라진다. **이 칸이 비어 있으면 실사를 안 적은 것으로 본다** |
| 5 | `Adjustment Delta` | TextBox **읽기 전용** | — | 빈 값 | 실사 − 전산. 위 두 칸을 칠 때마다 즉시 다시 계산된다 |
| 6 | `Reason` | TextBox | **Delta ≠ 0일 때만** ✔ | 빈 칸 | 차이가 없으면 비워도 된다 |

### 12-5. 입고(Stock-IN) 패널 — `Stock-in`을 눌렀을 때

패널 머리: `Stock-IN` + 상품명.

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `Batch Number` | TextBox | ✔ | **배치 행에서 열었으면 그 배치번호**, 제품 행에서 열었으면 빈 칸 |
| 2 | `Expiry Date` | DatePicker | ✔ | **배치 행에서 열었고 그 배치에 만료일이 있으면 그 날짜**, 아니면 오늘 + 1년 |
| 3 | `Stock-IN Date` | DatePicker | ✔ | **오늘** |
| 4 | `Quantity` 또는 `Boxes` | TextBox | ✔ | 빈 칸 (박스 상품이면 환산 미리보기가 붙는다) |

> 이 목록에는 **재고가 있는 배치만** 나오므로, 아직 한 번도 입고한 적 없는 신상품은 여기서 입고할 수 없다. 그건 Products 화면의 입고 패널에서 해야 한다.

### 12-6. 저장 흐름

**조정**
1. 배치 행 선택 → `Adjustment` → 패널이 열린다. (**다른 줄로 옮기면 실사 값이 초기화되고 패널이 닫힌다.**)
2. 실사 값을 적고 `Save`.
3. 실사 두 칸이 숫자로 합쳐지는지 검사 → `AdjustmentService`: 상품 → 배치 → **배치번호 중복** → 실사 음수 → Delta≠0인데 사유 비었는지.
4. **Delta가 0이고 배치번호도 안 고쳤으면** 확인 대화상자 `Confirm` / `No quantity difference was found.` (Yes/No). Yes면 그대로 저장.
5. 성공 → 패널이 닫히고, **목록을 다시 읽은 뒤** 하단에 `Adjustment saved successfully.`
6. 저장 도중 재고가 바뀌었으면(동시 수정) 실패 메시지와 함께 목록을 다시 읽는다.

**입고**
1. 줄 선택 → `Stock-in` → 패널이 열린다(조정 패널은 자동으로 닫힌다).
2. `Save` → 수량 정수 검사 → `StockInService` 검증(Products 화면과 동일) → 성공하면 패널을 닫고 목록을 다시 읽은 뒤 `Stock-in saved for …`
3. **이미 있는 배치번호를 그대로 두면 그 배치가 늘어나고, 새 번호를 적으면 새 배치가 생긴다.**

**배치 삭제**
1. 배치 행 선택. 버튼 왼쪽에 이유가 미리 표시된다 — `Select a batch row to delete it.` / `Batch A1 is empty and can be removed.` / `Batch A1 still has 12 left. Use Adjustment instead.`
2. 재고가 0인 배치에서만 버튼이 켜진다.
3. 확인 대화상자 `Delete Batch` / `Remove batch {번호} of {상품명} from the inventory list?` + `It is empty, and past sales records are kept.` (버튼 `Delete` / `Cancel`).
4. 삭제 후 목록 재조회 → `Batch {번호} was removed.` 확인과 삭제 사이에 입고가 들어왔으면 `The batch was not empty any more and was kept.`

---

## 13. Inventory Adjustment (독립 화면) — [AdjustmentView.xaml](../PharmaPOS.Wpf/Views/AdjustmentView.xaml)

> ⚠️ **현재 이 화면으로 가는 길이 없다.** 조정 기능이 Inventory Status의 패널로 옮겨지면서 메인 셸 하단의 `✏️ Adjustment` 버튼이 사라졌고, 코드 어디에서도 이 화면을 생성하지 않는다. 파일과 ViewModel은 그대로 남아 있다. **매뉴얼에는 넣지 않는 편이 맞다.** 참고용으로만 적어 둔다.

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `Product Search (barcode or name, press Enter)` | TextBox | ✔ | 빈 칸. **Enter로 검색** (USB 스캐너도 그대로 동작) |
| 2 | (검색 결과) | ListBox | ✔ | 상품명 목록에서 하나 선택 |
| 3 | `Batch Number` | ComboBox | ✔ | 선택 없음. 고른 상품의 배치 목록 |
| 4 | `System Quantity` | TextBox 읽기 전용 | — | 고른 배치의 전산 재고 |
| 5 | `Physical Count — Unopened Boxes` | TextBox | — | `0` (박스 상품에만 표시, 아래에 `{n} units per box.`) |
| 6 | `Physical Count` / `Physical Count — Loose Units` | TextBox | ✔ | 빈 칸 |
| 7 | `Adjustment Delta` | TextBox 읽기 전용 | — | 자동 계산 |
| 8 | `Reason` | TextBox | Delta≠0일 때 ✔ | 빈 칸 |

여기서는 **배치번호를 고칠 수 없다**(목록에서 고르기만 한다). 저장 성공 시 폼 전체가 초기화되고 `Adjustment saved successfully.`가 뜬다(화면은 그대로).

---

## 14. POS Sale — [PosSaleView.xaml](../PharmaPOS.Wpf/Views/PosSaleView.xaml)

왼쪽 장바구니, 오른쪽 주문 패널.

### 14-1. 상단 (전체 폭)

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 비고 |
|---|---|---|---|---|---|
| 1 | **없음** | TextBox (높이 48, 큰 글씨) | ✔ | 빈 칸 | 바코드 스캔 또는 상품명. **Enter로 검색.** 낱개 바코드(`…-EA`)를 찍으면 접미사를 떼고 찾되 **판매 단위를 자동으로 낱개로 맞춘다** |
| 2 | (검색 결과) | ListBox | — | | **결과가 딱 하나면 자동 선택**된다. 여럿이면 직접 골라야 한다 |

### 14-2. 장바구니 표 컬럼 (왼쪽부터)

| # | 머리글 | 내용 |
|---|---|---|
| 1 | `Product` | 상품명 |
| 2 | `Batch` | 배치번호 |
| 3 | `Qty` | 수량 |
| 4 | `Unit` | **이 수량이 박스인지 낱개인지는 이 컬럼으로만 구분된다** |
| 5 | `Price` | 단가 |
| 6 | `Total` | 줄 합계 |
| 7 | (머리글 없음) | `Remove` 버튼 |

### 14-3. 오른쪽 주문 패널 (배치 순서대로)

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|
| 1 | `Selected Product` | (읽기 전용 텍스트) | — | 고른 상품명 | |
| 2 | `Batch Number` | ComboBox | ✔ | **자동 선택**: 재고>0, 만료 안 됨, 유효기한이 가장 이른 배치. 유효기한을 모르는 배치(0)는 맨 뒤로 밀린다 | 그 상품의 배치 목록 |
| 3 | `Sell As` | ComboBox | ✔ | 바코드로 정해짐 (낱개 바코드=`Each`, 그 외=`Box`) | `Box` / `Each`. **박스/낱개를 나눠 파는 상품에만 나타난다** |
| 4 | `Quantity` 또는 `Quantity (boxes)` | TextBox | ✔ | **`1`** | 박스로 팔 때만 라벨에 `(boxes)`가 붙는다 |
| 5 | `Selling Price` | TextBox | ✔ | **상품 마스터 값이 자동으로 채워진다** (판매 단위가 바뀌면 그 단위 가격으로 다시 잡힌다) | **Administrator만 수정 가능. FacilityStaff에게는 읽기 전용** |
| 6 | `Payment Method` | ComboBox | ✔ | **선택 없음** | `Cash` / `MobilePayment` / `Insurance` / `Credit` / `Other` |
| 7 | `Cash Tendered` | TextBox | Cash일 때 ✔ | 빈 칸 | **`Cash`를 골랐을 때만 나타난다.** 값을 칠 때마다 아래 `Change`가 갱신된다 |
| 8 | `Notes` | TextBox | — | 빈 칸 | ⚠️ **저장되지 않는다.** `Stock_Transaction`에 컬럼이 없다 |
| 9 | `Total` | (읽기 전용) | | 장바구니 합계 | |
| 10 | `Change` | (읽기 전용) | | 받은 돈 − 합계. Cash일 때만 표시 | |

### 14-4. 저장 흐름

**장바구니에 담기 — `＋ Add to Cart`**
1. 상품 선택 → 배치 선택 → **만료 배치 차단**(`This batch is expired and cannot be sold.`) → 수량 빈 칸/정수/0 초과 → 판매가 숫자 검사.
2. 재고 판정은 **배치 재고에서 이미 장바구니에 담은 만큼을 뺀 나머지** 기준이다.
3. 낱개로 파는데 헐어 놓은 낱개가 모자라면 **여기서** 확인 대화상자가 뜬다 — `Open a Box` / `Only 4 loose unit(s) left in this batch.` + `Open 1 box(es) of 30 to sell 10?` (버튼 `Open` / `Cancel`). 거절하면 `Sale cancelled — no box was opened.`
4. 같은 상품 + 같은 배치 + 같은 판매 단위면 **기존 줄에 수량이 합산**된다. 박스 줄과 낱개 줄은 합치지 않는다.
5. 담고 나면 검색어·선택 상품·배치·수량(`1`)·판매가가 초기화되어 **바로 다음 상품을 스캔할 수 있다.**

**판매 확정 — `✓ Confirm Sale`**
1. Cash면 받은 돈 빈 칸/숫자 검사 → `ConfirmSaleAsync`.
2. 서비스 검증: 장바구니 비었는지 → 결제수단 선택 → (Cash) 받은 돈 ≥ 합계 → 각 줄 판매가 > 0.
3. **원가보다 싸게 파는 줄이 있으면** 확인 대화상자 `Confirm` / `Selling price is lower than cost price. Continue?` (Yes/No).
4. DB 트랜잭션 안에서 재고를 다시 확인하고 차감한다. 모자라면 `Some products do not have enough stock.`
5. 성공하면 **화면을 먼저 초기화**하고 `Sale completed successfully.`
6. 이어서 **영수증** 팝업 (`Receipt (Simulated Print)` — 실제 프린터로 나가지 않는다).
7. 이어서 **항생제 복약안내**. 설정이 `Ask`면 상품마다 `Antibiotic Counselling` / `This product contains an antibiotic. Print the counselling sheet?` + 상품명 (버튼 `Print` / `Skip`). 6·7단계가 실패해도 **판매는 이미 확정된 상태**다.
8. `✕ Cancel Sale` — 확인 없이 장바구니를 비우고 메인 셸로 나간다.
9. `← Back` — 장바구니를 비우지 않고 메인 셸로 (담아 둔 내용은 사라진다).

---

## 15. History — [HistoryView.xaml](../PharmaPOS.Wpf/Views/HistoryView.xaml)

입력 칸 없음. 부제 `Select a history type to view records.`

- 카드 `Sale History` (부제 `View all sales transactions`, 🛒) → Sales History
- 카드 `Adjustment History` (부제 `View all inventory adjustments`, ✏️) → Adjustment History
- `← Back` → 메인 셸

---

## 16. Sales History — [SalesHistoryView.xaml](../PharmaPOS.Wpf/Views/SalesHistoryView.xaml)

화면을 열면 **필터 없이 전체를 한 번 조회**한 상태로 시작한다.

### 16-1. 검색·필터 (왼쪽부터)

| # | 컨트롤 | 라벨 | 기준 | 선택지 / 기본값 |
|---|---|---|---|---|
| 1 | DatePicker | **없음** | 시작일 | **비어 있음**(제한 없음) |
| 2 | DatePicker | **없음** | 종료일 | **비어 있음** |
| 3 | TextBox | **없음** | 상품명 등 검색어 | 빈 칸 |
| 4 | ComboBox | **없음** | 결제수단 | **빈 항목(=전체)** / `Cash` / `MobilePayment` / `Insurance` / `Credit` / `Other`. 기본 빈 항목 |

- `Search` — 위 네 조건으로 재조회. 결과 없으면 `No sales records found.`
- `Reset` — 네 조건을 모두 비우고 다시 조회한다.

> 날짜·검색어를 바꿔도 **자동 조회되지 않는다.** 반드시 `Search`를 눌러야 한다 (Products·Inventory 화면과 다른 점).

### 16-2. 표 컬럼 (왼쪽부터)

| # | 머리글 | 내용 |
|---|---|---|
| 1 | `Product` | 상품명 |
| 2 | `Qty` | 수량 (환불 행은 **음수**) |
| 3 | `Unit Price` | 낱개 환산 단가 |
| 4 | `Line Total` | 줄 금액 (환불 행은 음수) |
| 5 | `Payment` | 결제수단 |
| 6 | `Sold By` | 판매자 아이디 |
| 7 | `Status` | 환불 행인지, 이미 일부/전부 환불된 판매 줄인지를 여기서 구분한다 |

### 16-3. 버튼과 흐름

| 버튼 | 동작 |
|---|---|
| `← Back` | **메인 셸로 간다.** History 화면이 아니다 (Admin Dashboard에서 들어왔을 때도 마찬가지) |
| `View Detail` | 한 거래 전체를 팝업으로. 환불이 있었으면 `Refunded:` / `Net:` 줄이 붙는다. **환불 행을 고르면** `Please select a sale, not a refund.` |
| `Reprint Receipt` | 같은 거래의 영수증 팝업을 다시 띄운다. 환불 행 선택 시 거절 |
| `Refund` | 아래 환불 창을 연다. 환불 행 선택 시 거절 |
| `Export` | 저장 대화상자 → `CSV files (*.csv)` / 기본 파일명 `sales_history_{yyyyMMdd}.csv`. 헤더에 `Type` 컬럼이 있어 판매(`Sale`)와 환불(`Refund`)을 구분한다 |

---

## 17. Refund (창) — [RefundWindow.xaml](../PharmaPOS.Wpf/Views/RefundWindow.xaml)

Sales History에서 판매 줄을 고르고 `Refund`를 누르면 뜨는 모달 창(창 제목 `Refund`).
제목 아래 요약: `Sold on {yyyy-MM-dd HH:mm} by {아이디}  ·  {결제수단}`

### 17-1. 표 컬럼 (왼쪽부터)

| # | 머리글 | 편집 | 내용 |
|---|---|---|---|
| 1 | `Product` | 읽기 전용 | 상품명 |
| 2 | `Batch` | 읽기 전용 | 배치번호 |
| 3 | `Sold` | 읽기 전용 | 판매 수량 |
| 4 | `Refundable` | 읽기 전용 | **이미 환불한 몫을 뺀 지금 되돌릴 수 있는 최대** |
| 5 | `Price` | 읽기 전용 | 단가 |
| 6 | `Refund Qty` | **입력 가능** (TextBox, 한 번 눌러 바로 편집) | 기본 `0`. **`Refundable`을 넘겨 치면 그 자리에서 잘려 되돌아간다** |
| 7 | `Amount` | 읽기 전용 | 단가 × 환불 수량 |

**한 줄짜리 판매는 열자마자 전량이 채워져 있다.**

### 17-2. 그 밖의 입력

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `Return refunded items to stock` | CheckBox | — | **켜짐** |
| 2 | `Memo (optional)` | TextBox | — | 빈 칸 |

### 17-3. 흐름

1. `Refund whole sale` — 모든 줄의 `Refund Qty`를 `Refundable`로 채운다.
2. 우측 상단에 `Total refund: {금액}`이 실시간으로 갱신된다.
3. `Refund` → 수량이 0보다 큰 줄이 하나도 없으면 `Please enter the quantity to refund.`
4. 확인 대화상자 `Confirm Refund` / `Refund {금액}?` + `The items will be returned to stock.` 또는 `The items will NOT be returned to stock.` (버튼 `Refund` / `Cancel`).
5. 저장은 append 방식이다 — 원 판매 줄을 고치지 않고 **수량·금액이 음수인 Refund 행을 새로 쓴다.**
6. 성공하면 창이 닫히고, Sales History가 다시 조회되며 `Refunded {금액}.`
7. 열 때 이미 전량 환불된 판매면 `This sale has already been fully refunded.`와 함께 `Refund` 버튼이 꺼진다.

> 재고 반환 체크를 끄면 **돈만 돌려주고 재고는 그대로**이며, 그 흔적은 사유에 붙는 `(not returned to stock)` 표시뿐이다. 되돌아온 재고는 언제나 **낱개**로 들어간다(박스로 복원되지 않는다).

---

## 18. Adjustment History — [AdjustmentHistoryView.xaml](../PharmaPOS.Wpf/Views/AdjustmentHistoryView.xaml)

읽기 전용 화면. 열자마자 전체를 한 번 조회한다.

### 검색·필터 (왼쪽부터)

| # | 컨트롤 | 라벨 | 기준 | 기본값 |
|---|---|---|---|---|
| 1 | DatePicker | 없음 | 시작일 | 비어 있음 |
| 2 | DatePicker | 없음 | 종료일 | 비어 있음 |
| 3 | TextBox | 없음 | 상품명 **또는** 배치번호 | 빈 칸 |

`Search`를 눌러야 반영된다. `Reset`은 셋을 비우고 재조회. 결과 없으면 `No adjustment records found.`

### 표 컬럼 (왼쪽부터)

`Date` / `Product` / `Batch` / `Qty Change` / `Reason` / `Adjusted By`

`Qty Change`는 실사 − 전산이라 음수일 수 있다. 배치번호를 고친 조정은 사유에 `Batch number: (none) → A1` 형태의 문구가 함께 남는다.

`← Back` → History 화면.

---

## 19. Inventory Alerts — [AlertsView.xaml](../PharmaPOS.Wpf/Views/AlertsView.xaml)

메인 셸의 🔔 → `View All Alerts →`로 들어온다.

### 검색·필터 (왼쪽부터)

| # | 컨트롤 | 라벨 | 기준 | 선택지 / 기본값 |
|---|---|---|---|---|
| 1 | ComboBox | **없음** | 알림 종류 | `All` / `LowStock` / `Expiry` — 기본 **`All`** |
| 2 | ComboBox | **없음** | 우선순위 | `All` / `Critical` / `Warning` / `Normal` — 기본 **`All`** |

둘 다 바꾸는 즉시 재조회. 결과 없으면 `No alerts found.`

### 표 컬럼 (왼쪽부터)

`Type` / `Priority` / `Product Name` / `Quantity` / `Batch Number`

### 우클릭 메뉴 · 버튼

| 항목 | 동작 |
|---|---|
| 우클릭 `Go to Inventory` | Inventory Status를 열고 **검색어에 그 상품명을 미리 넣어 준다** |
| 우클릭 `Go to Product` | 그 상품이 선택된 Products 화면으로 (`← Back`으로 여기 돌아온다) |
| `← Back` | 메인 셸 |
| `View Inventory` | 우클릭 `Go to Inventory`와 같다 |
| `Export` | 저장 대화상자 → `CSV files (*.csv)` / 기본명 `inventory_alerts_{yyyyMMdd}.csv` |

---

## 20. Administrator Dashboard — [AdminDashboardView.xaml](../PharmaPOS.Wpf/Views/AdminDashboardView.xaml)

입력 칸 없음. 지표 카드 6개(`Daily Sales` / `Transactions` / `Inventory Value` / `Active Products` / `Low Stock` / `Expiry Alert`)와 이동 버튼 6개.

`📦  Product Management` / `👥  User Management` / `📊  Inventory Overview` / `🛒  Sales History` / `📈  Reports` / `📁  Import / Export` / `← Back`

---

## 21. User Management — [UserManagementView.xaml](../PharmaPOS.Wpf/Views/UserManagementView.xaml)

### 검색·필터

| # | 컨트롤 | 라벨 | 기준 | 선택지 / 기본값 |
|---|---|---|---|---|
| 1 | TextBox | **없음** | 아이디 검색 | 빈 칸. **한 글자마다 즉시 재조회** |
| 2 | ComboBox | **없음** | 상태 | **빈 항목(=전체)** / `Active` / `Inactive`. 기본 빈 항목. 즉시 재조회 |

결과 없으면 `No users found.`

### 표 컬럼 (왼쪽부터)

`Username` / `Role` / `Status`

### 우클릭 메뉴

빈 곳을 우클릭하면 메뉴가 뜨지 않는다. 우클릭한 줄이 곧바로 선택된다. 아래 버튼과 **같은 동작**이다.

| 항목 | 표시 조건 |
|---|---|
| `Edit Role` | 항상 (선택 없으면 비활성) |
| `Activate` | **비활성 계정을 골랐을 때만** |
| `Deactivate` | 그 외 (선택 없으면 비활성) |
| `Reset Password` | 항상 (선택 없으면 비활성) |

### 버튼과 흐름

| 버튼 | 동작 |
|---|---|
| `← Back` | **Admin Dashboard**로 (메인 셸이 아니다) |
| `Add User` | 아래 Add User 창 |
| `Edit Role` | **Administrator ↔ FacilityStaff 단순 토글.** 확인 대화상자 `Confirm` / `Change role of '{아이디}' to {새 역할}?` (Yes/No) → 성공 시 목록 재조회 |
| `Deactivate` (빨강) | **확인 대화상자 없이 즉시** 비활성화. 자기 계정은 불가 (`You cannot deactivate your own account.`) |
| `Activate` (파랑) | **비활성 계정을 고르면 `Deactivate` 자리에 대신 나타난다.** 확인 없이 즉시 되살린다. 본인 계정 검사는 없다 — 자기 계정은 애초에 비활성화할 수 없다. 상태 필터가 `Inactive`면 그 줄이 목록에서 사라지므로 그때만 `'{아이디}' is active again.`을 남긴다 |
| `Reset Password` | 아래 Reset Password 창. 성공하면 `Password for '{아이디}' has been reset.` |

선택 없이 누르면 전부 `Please select a user.`

> `Deactivate`와 `Activate`는 **한 칸을 나눠 쓴다.** 고른 계정의 상태에 따라 하나만 보이고, 아무것도 고르지 않았을 때는 `Deactivate`가 남는다.

---

## 22. Add User (창) — [AddUserWindow.xaml](../PharmaPOS.Wpf/Views/AddUserWindow.xaml)

창 제목 `Add User` / 본문 제목 `Add User`.

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|
| 1 | `Username` | TextBox | ✔ | 빈 칸 | |
| 2 | `Password` | PasswordBox | ✔ | 빈 칸 | 아래에 비밀번호 규칙 안내문 |
| 3 | `Confirm Password` | PasswordBox | ✔ | 빈 칸 | |
| 4 | `Role` | ComboBox | ✔ | **선택 없음** | `FacilityStaff` / `Administrator` |

**흐름**: `Create` → `CreateUserAsync` (아이디 빈 칸 → 비밀번호 → 확인 일치 → 비밀번호 규칙 → 역할 선택 → 아이디 중복) → 성공하면 창이 닫히고 **목록이 자동 재조회**된다. 실패 시 창 하단에 사유. `Cancel`은 그냥 닫는다.

---

## 23. Reset Password (창) — [ResetPasswordWindow.xaml](../PharmaPOS.Wpf/Views/ResetPasswordWindow.xaml)

창 제목 `Reset Password`. 본문 맨 위에 `Reset password for: {대상 아이디}`.

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `New Password` | PasswordBox | ✔ | 빈 칸 (아래에 비밀번호 규칙 안내문) |
| 2 | `Confirm New Password` | PasswordBox | ✔ | 빈 칸 |

**흐름**: `Reset` → `ResetPasswordAsync` → 성공하면 창이 닫히고 User Management 화면에 `Password for '{아이디}' has been reset.`이 남는다(목록은 비밀번호를 보여주지 않으므로 재조회는 없다). 실패 시 창 하단에 사유.

> 창 높이는 내용에 맞춰 늘어난다. 한때 `320`으로 못박혀 있어 비밀번호 규칙 안내문이 두 줄 들어가면 `Cancel`/`Reset`이 창 밖으로 밀려 보이지 않았다.

---

## 24. Reports — [ReportsView.xaml](../PharmaPOS.Wpf/Views/ReportsView.xaml)

관리자 전용. 화면을 열면 **이번 달 1일 ~ 오늘**로 자동 조회된다.

### 24-1. 기간 선택 (왼쪽부터)

| # | 컨트롤 | 라벨 | 기본값 |
|---|---|---|---|
| 1 | DatePicker | **없음** | **이번 달 1일** |
| 2 | DatePicker | **없음** | **오늘** |
| 3 | Button `Apply` | | 위 두 날짜로 재조회 |
| 4 | Button `This Month` | | 이번 달 1일 ~ **오늘**로 맞추고 즉시 조회 |
| 5 | Button `Last Month` | | 지난달 1일 ~ **말일**로 맞추고 즉시 조회 |

기간 아래에 `{기간 라벨}`과 `compared with the previous month: {직전 기간 라벨}`이 표시된다. 달 전체를 고르면 비교 대상이 자동으로 전월이 된다.

### 24-2. 요약 카드 4개

`Sales Amount` / `Transactions` / `Units Sold` / `ACCESS Share` — 각각 아래에 직전 기간 대비 증감이 붙는다. `ACCESS Share`의 부제는 `{n} printed / {m} antibiotic sales`.

### 24-3. 정렬 컨트롤

| 컨트롤 | 라벨 | 선택지 / 기본값 |
|---|---|---|
| ComboBox | `Sort by` (상품 순위 표 오른쪽 위) | `Amount` / `Quantity` — 기본 **`Amount`**. **바꿔도 DB를 다시 치지 않고** 이미 받아 온 값을 다시 늘어놓고 순위를 다시 매긴다 |

### 24-4. 표 컬럼

**Product Ranking** (왼쪽 표) — `#` / `Product` / `Qty` / `Amount` / `Amount vs prev`

**Antibiotics by Ingredient** (오른쪽 표) — `Ingredient` / `Strength` / `Group` / `Qty` / `Qty vs prev` / `Counselled` / `Rate`
(`Counselled`의 `6 / 8`은 "판매 8건 중 6건에 복약안내가 나갔다"는 뜻)

> **이 표에는 AWaRe 그룹으로 판정된 항생제만 나온다.** 복약안내 기능은 판매된 모든 줄을 참조 목록과 대조하고 실패한 것도 로그에 남기지만(시드에서 빠진 항생제를 찾는 기록이다), 그 `UNMATCHED` 행은 이 표와 위쪽 `ACCESS Share` · `{n} printed / {m} antibiotic sales` 집계에서 모두 제외된다. 마스크나 혈압약이 성분 한 줄로 오르거나 ACCESS 비중이 항생제와 무관한 수량에 희석되지 않는다. 누락 확인은 Counselling 설정 화면의 `Unmatched products` 항목이 맡는다.

항생제 표가 비면 아래에 안내문이 나온다: `No antibiotic sales recorded in this period. This table is built from counselling records, so sales made before the counselling feature was in use do not appear.`

### 24-5. 흐름

- 조회 결과 거래가 0건이면 `No sales in {기간}.`
- `Export CSV` → 저장 대화상자 → `CSV files (*.csv)` / 기본명 `report_{시작일}_{종료일}.csv`. **요약 · 상품 순위 · 항생제 표가 한 파일에 구역을 나눠 들어간다.** 데이터가 없으면 `No data to export.`
- `← Back` → 메인 셸.

---

## 25. Import / Export — [BackupExportView.xaml](../PharmaPOS.Wpf/Views/BackupExportView.xaml)

화면 제목은 `Import / Export`. 좌우로 나뉘고, 복원만 아래 빨간 위험 구역에 따로 있다.

### 25-1. 왼쪽 `IMPORT` / `File to app`

설명: `One CSV or Excel file registers products and stock, in two steps. Use the same file for ① and then ②.`

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | `File` | TextBox **읽기 전용** + `Browse` 버튼 | ✔ | 빈 칸 |

버튼 두 개(각각 아래에 설명문이 붙는다):
- `①  Import Products` — `Adds new products. For products that already exist, only the columns filled in the file are updated — empty columns are left as they are.`
- `②  Import Inventory` — `Adds stock batch by batch, recorded as stock-in. Quantity is counted in single units. Put N in expiry_date when the expiry date is unknown.`

**`③  Import Photos`** — 파일이 아니라 **폴더**를 고른다. 시트에 컬럼을 늘리지 않는다.

- 사진 파일명을 **바코드**로 둔다 (`8801234567890.jpg`). 매칭 순서는 유통사 바코드 → 내부 바코드 → `-EA` 뗀 값 → 상품명.
- 확장자는 **`.jpg` 권장**. `.jpeg` `.png` `.bmp`도 읽는다.
- **HEIC는 못 읽는다** — 아이폰 기본 저장 형식이다. 카메라 설정을 "높은 호환성"으로 바꾸거나 JPG로 변환해야 한다. 읽지 못한 파일은 이유와 함께 미리보기에 나온다.
- 미리보기에 **덮어쓸 장수**가 따로 나온다. 되돌릴 수 없다.
- ①②와 달리 **같은 폴더를 다시 넣어도 막지 않는다.** 사진은 덮어쓰기라 쌓이지 않는다.

맨 아래 `File columns` 안내 상자:
- 필수/기본: `product_name, unit, barcode, cost_price, selling_price, safety_stock, units_per_box, loose_unit_price, batch_number, expiry_date, quantity`
- 선택: `generic_name, strength, dosage_form, atc_code, is_combination, manufacturer, country_of_origin, status. An exported products file can be edited and imported straight back.`
- `dosage_form is the form of the medicine (Tablet, Syrup, Injection, Ointment…), while unit is how one piece is counted (Tablet, Bottle, Tube). A file with only product_name and dosage_form fills the form in for products that already exist.`

**`dosage_form` 값** — 고정 목록이지만 파일은 손으로 채우므로 관대하게 읽는다. 대소문자·구분자·복수형·흔한 줄임말을 받아 준다 (`tablets`, `tab`, `caps`, `inj`, `vial`, `sachet`, `Eye drops`, `OINTMENT` …). 그래도 못 읽으면 **그 행이 오류로 빠지고** 허용 값 목록이 메시지에 함께 나온다. 실제 저장 값은 `Tablet` / `Capsule` / `Syrup` / `Suspension` / `Powder` / `Injection` / `Infusion` / `Ointment` / `Cream` / `Drops` / `Suppository` / `Inhaler` / `Other`.

> **필수 컬럼은 `product_name` 하나뿐**이므로, `product_name` + `dosage_form` **두 열짜리 파일**로 이미 등록된 상품 수백 개의 제형만 일괄로 채울 수 있다. 나머지 값은 건드리지 않는다. 재임포트 차단은 파일 내용의 해시 기준이라 열을 추가해 고친 파일은 막히지 않는다.

### 25-2. 오른쪽 `EXPORT / BACKUP` / `App to file`

설명: `Writes the data currently stored in the app to files. Nothing in the app changes.`

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|
| 1 | `Folder` | TextBox **읽기 전용** + `Browse` | ✔ | 빈 칸 | 내보내기와 백업이 같은 폴더를 쓴다 |
| 2 | `Products — product list` | CheckBox | — | **켜짐** | |
| 3 | `Inventory — stock by batch` | CheckBox | — | **켜짐** | |
| 4 | `Sales history — sales and refunds` | CheckBox | — | **켜짐** | |
| 5 | `Format` | RadioButton 2개 | ✔ | **`CSV`** | `CSV` / `Excel` |

- `📤  Export Selected Data` — 설명: `One file per selected item. These files are for reading and record keeping — they cannot be restored.`
- 아래 `Full backup (.db)` 상자 — 설명 `Copies the whole database into a single file. This is the only file the restore below can read.` / 버튼 `💾  Create Backup File`

### 25-3. 아래 `RESTORE` / `Roll back to a backup file`

설명: `Replaces all current data with the contents of a backup (.db) file. Nothing is merged — the data in the app now is gone. The current state is backed up automatically first, and the app restarts when the restore finishes.`

| # | 라벨 | 컨트롤 | 필수 | 기본값 |
|---|---|---|---|---|
| 1 | (라벨 없음) | TextBox **읽기 전용** + `Browse` | ✔ | 빈 칸. 필터 `SQLite Database (*.db)` / `All files (*.*)` |

버튼 `🔄  Restore`.

### 25-4. 저장 흐름

**가져오기 (① ②가 같은 순서를 탄다)**
1. `Browse`로 파일 선택. 필터 `CSV/Excel (*.csv;*.xlsx)` / `CSV (*.csv)` / `Excel (*.xlsx)`.
2. `①` 또는 `②` 클릭 → 파일 존재 확인 → **SHA-256 해시로 같은 파일을 이미 넣었는지 확인**. 넣었으면 `Import Blocked` / `This file has already been imported.`로 **여기서 끊는다**(진행 선택지 없음). 단계(상품/재고)가 다르면 같은 파일이라도 통과한다.
3. 파일을 읽어 **무엇이 들어갈지 계획만 세우고 미리보기 대화상자**를 띄운다 (고정폭 글씨).
   - 1단계: `Rows in file` / `New products` / `Products to update` / `Unchanged` / `Duplicate rows skipped` / `Rows with errors` + 오류 목록(최대 15줄) + `Existing products keep any value the file leaves empty.` + `Rows listed above are skipped. Continue?`
   - 2단계: `Rows in file` / `Batches to add` / `Product not found` / `Without expiry date` / `Rows with errors` + 목록 + `Quantity is counted in single units, not boxes.` + `Rows listed above are skipped. Continue?`
   - 버튼 `Import` / `Cancel`. 취소하면 `Import cancelled.`
4. `Import`를 누르면 **방금 보여준 계획을 그대로** 반영한다(다시 계산하지 않는다).
5. 결과 대화상자: `Imported : {n} products|batches` / `Failed : {m}` + 실패 목록. 하단 메시지는 `Import complete — {n} products added.` 또는 `Import complete — Success: n, Failed: m.`

**내보내기** — 체크한 데이터셋마다 파일 하나. 폴더를 안 골랐으면 `Please select a backup location.`

**DB 백업** — `pharmapos_backup_{yyyyMMdd_HHmmss}.db`

**복원**
1. 파일을 안 골랐으면 `Please select a backup file.`
2. 확인 대화상자 `Confirm Restore` / `Current data will be replaced. Continue?` (Yes/No). 거절하면 `Please confirm database restore.`
3. **복원 직전 현재 DB를 자동 백업**한다 (`pharmapos_pre_restore_backup_…db`). 내보내기 폴더를 지정하지 않았으면 `%APPDATA%\PharmaPOS\`에 떨어진다.
4. 성공하면 `Restart Required` / `Database restored successfully. The application will now restart.` → **앱이 자동으로 재시작된다.**

`← Back` → 메인 셸.

---

## 26. Antibiotic Counselling — [CounsellingSettingsView.xaml](../PharmaPOS.Wpf/Views/CounsellingSettingsView.xaml)

관리자 전용. 제목 아래: `Counselling sheets support pharmacist advice on antibiotic use. They do not provide dosing or diagnosis.`
화면을 열면 저장된 설정을 읽어 각 칸을 채운다.

### 26-1. 읽기 전용 정보 (입력 칸 위)

- `WHO AWaRe REFERENCE DATA` — `{n} antibiotics loaded ({출처}).` 또는 `Not installed. Counselling sheets cannot be printed until the AWaRe reference file is added.`
- (입력 칸 아래) `STEWARDSHIP FIGURES` — 최근 30일 지표 한 문단. 데이터가 없으면 `No antibiotic sales recorded in the last 30 days.`

### 26-2. 입력 필드 (`PRINTING` 구역, 배치 순서대로)

| # | 라벨 | 컨트롤 | 필수 | 기본값 | 선택 항목 |
|---|---|---|---|---|---|
| 1 | `When an antibiotic is sold` | ComboBox | ✔ | 저장값 (초기 **`Always`**) | `Always` / `Ask`<br>안내문: `A counselling notice is shown on screen for every antibiotic sale — this cannot be turned off. Always prints the sheet as well; Ask lets you decide each time.` |
| 2 | `Send the sheet to` | ComboBox | ✔ | 저장값 (초기 **`Printer`**) | `Printer` / `File`<br>안내문: `Printer sends the sheet to the default printer. File saves it as a text file instead - useful for checking the sheet when no printer is attached.` |
| 3 | `Folder (leave empty for the default location)` | TextBox + `Browse` | — | 저장값 | **`File`을 골랐을 때만 나타난다.** 비우면 `%APPDATA%\PharmaPOS\counselling-sheets\` |
| 4 | `Sheet length` | ComboBox | ✔ | 저장값 (초기 **`Full`**) | `Full` / `Compact`<br>안내문: `Full is about 20 cm on 58 mm paper. Compact is about 10 cm and drops the signature and QR block.` |
| 5 | `Local language` | ComboBox | ✔ | 저장값, 없으면 **`English only`** | 첫 항목은 항상 `English only`. 그 뒤로 설치된 로케일이 `km-kh (Khmer) - approved` 또는 `… - not reviewed - English only` 형태로 나온다<br>안내문: `English is always printed. A local language is added only after its translation has been reviewed and approved.` |
| 6 | `More information address (optional)` | TextBox | — | 저장값 | 안내문: `Leave empty to omit the information block from the sheet.` |

### 26-3. 저장 흐름

`Save` → 검증 없이 그대로 저장(폴더·주소는 `Trim()`) → `Settings saved.` 실패 시 `Settings could not be saved. Please try again.` `← Back` → 메인 셸.

> 미검수 로케일도 목록에 나오지만, 고르더라도 현지어는 인쇄되지 않는다(라벨에 그 사실이 적혀 있다).

---

## 27. 메인 셸 — [MainShellView.xaml](../PharmaPOS.Wpf/Shell/MainShellView.xaml)

입력 칸 없음. 매뉴얼용으로 구성만 적어 둔다.

- **상단 왼쪽** — `Welcome, {아이디}` + 앱 버전
- **상단 버튼** — `My Page` (파란색) / `Logout` (빨간 글씨)
- **상단 오른쪽** — `🔔` 버튼 + 건수 배지(0이면 배지가 사라진다). 누르면 팝업이 열리고, 팝업 안에 우선순위 색 막대가 붙은 최근 알림 목록과 `View All Alerts →` 버튼. 알림이 없으면 `No alerts 🎉`
- **가운데 카드 3개** — `Products` (💊) / `Inventory` (📦) / `POS Sale` (🛒). **카드 전체가 버튼이다**
- **하단 (관리자, 4개)** — `⚙️  Admin Dashboard` / `📈  Reports` / `📊  History` / `🧫  Counselling`
- **하단 (직원, 2개)** — `🔒  Reports` (**비활성. 마우스를 올리면 `Administrators only.`**) / `📊  History`

---

## 부록 A. ComboBox 선택 항목 한눈에 보기

| 화면 · 칸 | 항목 | 기본 선택 |
|---|---|---|
| Initial Setup · `Facility Type` | `Pharmacy` / `DrugShop` / `HealthPost` / `HealthCenter` | `Pharmacy` |
| Initial Setup · `Security Question`<br>Recovery Settings · `Question` | `What was the name of your first pet?`<br>`What is your mother's maiden name?`<br>`What was the name of your first school?`<br>`What city were you born in?` | 없음 |
| Recovery Settings · `Email Provider` | `Gmail` / `Outlook` / `Other` | 없음 |
| Products · 상태 필터 | `All` / `Active` / `Inactive` | `All` |
| Product · `Dosage Form (optional)` | (빈 항목) / `Tablet` / `Capsule` / `Syrup` / `Suspension` / `Powder` / `Injection` / `Infusion` / `Ointment` / `Cream` / `Drops` / `Suppository` / `Inhaler` / `Other` | 빈 항목 |
| Product · `Category (optional)` | (빈 항목) / `Medicine` / `NonMedicine` | 빈 항목 |
| Product · `Status *` | `Active` / `Inactive` | `Active` |
| Inventory Status · 유효기한 필터 | `All` / `Expired` / `Within7Days` / `Within30Days` / `Within90Days` | `All` |
| Inventory Status · 정렬 | `ProductName` / `Quantity` / `ExpiryDate` | `ProductName` |
| POS Sale · `Sell As` | `Box` / `Each` | 바코드에 따라 자동 |
| POS Sale · `Payment Method` | `Cash` / `MobilePayment` / `Insurance` / `Credit` / `Other` | 없음 |
| Sales History · 결제수단 필터 | (빈 항목=전체) / 위 5개 | 빈 항목 |
| Alerts · 종류 필터 | `All` / `LowStock` / `Expiry` | `All` |
| Alerts · 우선순위 필터 | `All` / `Critical` / `Warning` / `Normal` | `All` |
| User Management · 상태 필터 | (빈 항목=전체) / `Active` / `Inactive` | 빈 항목 |
| Add User · `Role` | `FacilityStaff` / `Administrator` | 없음 |
| Reports · `Sort by` | `Amount` / `Quantity` | `Amount` |
| Counselling · `When an antibiotic is sold` | `Always` / `Ask` | 저장값 (초기 `Always`) |
| Counselling · `Send the sheet to` | `Printer` / `File` | 저장값 (초기 `Printer`) |
| Counselling · `Sheet length` | `Full` / `Compact` | 저장값 (초기 `Full`) |
| Counselling · `Local language` | `English only` + 설치된 로케일 | 저장값 |
| Internal Barcode · `Printer` | `Label Printer 1 (Placeholder)` / `Label Printer 2 (Placeholder)` | 없음 |

---

## 부록 B. 수량 칸이 뜻하는 단위 (혼동 지점)

같은 "수량"이라도 화면마다 뜻이 다르다. 매뉴얼에서 가장 오해가 나기 쉬운 부분이다.

| 화면 · 칸 | 단위 |
|---|---|
| Products / Inventory Status 입고 패널 · `Boxes` | **박스 개수** (박스/낱개 상품일 때). 아래 미리보기가 낱개 환산량을 보여준다 |
| Products / Inventory Status 입고 패널 · `Quantity` | 낱개 (박스 구분이 없는 상품) |
| 조정 패널 · `Physical Count — Unopened Boxes` | 안 뜯은 박스 개수 |
| 조정 패널 · `Physical Count — Loose Units` | 헐어 놓은 낱개 개수 |
| 조정 패널 · `Physical Count` | 낱개 전량 (박스 구분이 없는 상품) |
| POS Sale · `Quantity (boxes)` | 박스 개수 |
| POS Sale · `Quantity` | 낱개 개수 |
| Import/Export · 파일의 `quantity` 열 | **낱개** (박스가 아니다 — 입고 화면과 반대) |
| Inventory Status 표의 `STOCK`, 판매 이력의 `Qty` | 언제나 **낱개** |

---

## 부록 B-1. 재고가 맞지 않을 때 (`StockBefore` / `StockAfter`)

판매 이력 내보내기와 Import/Export의 `sales_history` 파일에 두 컬럼이 있다. 그 거래 **직전·직후 그 배치의 재고**다.

| Product | Batch | Qty | StockBefore | StockAfter |
|---|---|---|---|---|
| Amoxicillin 500mg | B2401 | 10 | 300 | 290 |
| Amoxicillin 500mg | B2401 | 5 | 290 | 285 |
| Amoxicillin 500mg | B2401 | 2 | **280** ← 어긋남 | 278 |

**배치별로 시간순 정렬해 훑는다.** 앞 줄의 `StockAfter`와 다음 줄의 `StockBefore`가 어긋나는 지점이 원인 자리다 — 원장에 남지 않은 재고 변동이 거기서 일어났다는 뜻이다.

- 입고·판매·조정·환불 **모두** 기록된다. 하나라도 빠지면 정상 동작에서도 체인이 끊긴다.
- 계산값이 아니라 **실제 재고에서 읽은 값**이다.
- 재고 반환을 끈 환불은 두 값이 **같다** — 돈만 돌려주고 재고는 그대로라는 뜻이다.
- 배치가 처음 생기는 입고는 `StockBefore`가 **빈칸**이다.
- **이 기능이 생기기 전의 거래는 두 칸이 모두 빈칸이다.** 역추적은 그 이후 거래에만 된다.

## 부록 C. 저장되지 않거나 동작하지 않는 입력 칸

매뉴얼에서 설명을 빼거나, 한계를 밝혀야 하는 칸들이다.

| 화면 · 칸 | 실제 |
|---|---|
| POS Sale · `Notes` | **입력해도 저장되지 않는다.** `Stock_Transaction`에 컬럼이 없다. 가장 위험한 항목 |
| Internal Barcode · `Printer` | 실제 프린터 목록이 아니라 `(Placeholder)` 더미 2개 |
| Internal Barcode · `Print Label` | 입력값 검증만 하고 성공을 돌려준다. 하드웨어 연동 없음 |
| POS Sale · 영수증 | 실제 프린터로 나가지 않고 `Receipt (Simulated Print)` 팝업으로 보여준다 |
| Product · `ATC Code` | 비워 둬도 성분명(`Generic Name`)만으로 복약안내 매칭이 된다. 필수가 아니다 |
| 각 검색 상자 | 라벨도 자리표시자도 없다 (0번 항목 참고) |
| Inventory Adjustment 독립 화면 | 현재 진입 경로가 없다 (13번 항목 참고) |
