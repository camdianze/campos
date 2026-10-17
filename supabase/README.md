# CamPOS v2 — 서버 쪽 (2단계)

이 폴더는 **서버가 받을 자리**다. 앱은 아직 아무것도 보내지 않는다 — 전송은 3단계다.

| 파일 | 무엇 |
|---|---|
| `schema.sql` | 표 전부. SQL Editor에 붙여 실행한다. |
| `functions/sync/index.ts` | Edge Function. 서버로 들어가는 유일한 길. |
| `functions/sync-photo/index.ts` | 사진 한 장을 받는다. 싣는 것이 달라 함수가 따로 있다. |
| `functions/register/index.ts` | 라이선스 코드를 이 PC의 토큰으로 바꿔 준다. |
| `new-token.ps1` | PC용 토큰 하나를 만들고, 붙여넣을 SQL을 출력한다. |
| `test-upload.ps1` | 미리보기 파일을 실제로 보내 서버를 확인한다. |
| `check.sql` | 서버에 무엇이 들어와 있는지 한눈에. 전송할 때마다 다시 Run. |
| `releases-bucket.md` | 업데이트 보관소 버킷 만들기 (한 번만). |

CLI는 필요 없다 — 전부 웹 콘솔과 위 두 스크립트로 끝난다.

---

## 왜 이렇게 생겼나

**표에 RLS를 켜고 정책을 하나도 두지 않는다.** 정책 없는 RLS는 "anon 키로는 아무것도 못 한다"는 뜻이다. `service_role` 키는 Edge Function 안에만 있고 PC에는 내려가지 않는다. 그래서 쓰기 경로가 함수 하나뿐이고, 규칙을 지킬 자리도 거기 하나뿐이다. **정책을 하나라도 추가하면 그 순간 함수 밖에 길이 생긴다** — 이 설계에서 가장 조용히 깨지는 부분이 그것이다.

**site_id는 토큰이 정한다.** 묶음에 적힌 site_id는 아예 거부한다. PC가 자기 site_id를 고를 수 있으면 한 약국이 다른 약국의 행을 덮어쓸 수 있고, 쓰기가 함수 하나이므로 그 규칙이 들어갈 자리도 여기밖에 없다.

**토큰은 SHA-256만 저장한다.** 이 표가 새어도 그것으로는 전송할 수 없다. 라이선스 시리얼과 같은 방식 — 서버는 대조만 하고 원본은 들고 있지 않다.

**키는 `(site_id, 원본 ID)`다.** 약국 SQLite의 ID를 그대로 둘째 열로 쓴다. 같은 묶음을 다시 받아도 제자리에서 갱신되니 **행이 늘지 않는다** — 중복 처리의 절반이 키 선택으로 끝난다.

**모르는 열 이름이 오면 거부한다.** 조용히 버리면 그 열만 비어서 들어가고 아무도 보고하지 않는다. 그래서 앱에 열을 늘릴 때는 **서버가 먼저** 바뀌어야 한다. 순서가 거꾸로면 전송이 멈추지만, 멈춘 전송은 보이고 반쯤 들어간 데이터는 안 보인다.

**금액은 자릿수를 제한하지 않은 `numeric`이다.** `numeric(12,2)`로 적으면 서버가 값을 반올림한다. $0.9756 같은 네 자리 가격이 이 앱에는 정상이고, 그 값에서만 리엘이 정확히 되돌아 나온다 — 서버는 받은 숫자를 바꾸는 쪽이 되어서는 안 된다.

**시각은 `bigint`(epoch ms) 그대로다.** `timestamptz` 생성 열을 두면 읽기 편하지만 `expiry_date = 0`("유효기간 미상")이 1970-01-01로 번역된다. 0을 아는 쪽, 즉 Python 가공 단계에서 바꾸는 것이 맞다.

**데이터 표끼리 외래키는 걸지 않았다.** `inventory.product_id → product`로 걸면, 묶음을 쪼개 보내는 3단계에서 **순서 차이만으로** 약국의 전송이 실패한다. PC 쪽 SQLite가 이미 `foreign_keys = ON`으로 참조를 지키고 있다. `site` 참조만 남겼다 — 그쪽은 토큰이 정하므로 깨질 수 없다.

---

## 상태

**2단계는 2026-10-10에 끝났다.** 개발 프로젝트는 `wigewqyblckkguaslupp` (Singapore), 개발 PC는 `site_id = DEV-001`. 아래 ①~⑤를 그대로 거쳤고 확인된 것은:

| 확인 | 결과 |
|---|---|
| 서버가 묶음을 받는가 | product 304 / inventory 324 / app_user 1 / stock_transaction 324 |
| **같은 묶음을 두 번 보내면** | **행이 늘지 않는다.** `sync_batch`만 2가 된다 |
| 금액이 반올림되는가 | 아니다 — `7.25`, `29000`, `3.75`가 그대로 |
| 비밀이 새는가 | 아니다 — `app_user`는 네 칸뿐 |

`DEV-001`의 304개 제품은 **가상 데이터**다. 진짜 약국은 자기 `site_id`와 토큰으로 따로 등록하고, 테스트 데이터는 `delete from public.product where site_id = 'DEV-001'`처럼 site_id로 잘라 지운다.

아래 ①~⑤는 **다음 약국을 등록할 때 다시 따라갈 절차**다.

## 할 일

### ① 프로젝트 만들기

[supabase.com](https://supabase.com) → New project

- **Region: Southeast Asia (Singapore)** — 캄보디아에서 가장 가깝다
- 데이터베이스 비밀번호는 적어 두기 (분실하면 재설정만 가능)

Security 체크박스 세 개:

| 항목 | | 왜 |
|---|---|---|
| Enable Data API | ✅ **켠다** | Edge Function이 supabase-js로 `/rest/v1`을 호출한다. 끄면 함수가 부를 곳이 없어진다. |
| Automatically expose new tables | ⬜ **끈다** | 이걸 켜면 새 표가 `anon`·`authenticated`에도 열린다. 다만 이 설정은 `service_role` 권한까지 같이 주므로, 끈 채로 두면 함수가 권한 오류로 죽는다 — 그래서 `schema.sql`이 `service_role`에 필요한 권한을 **직접** 준다. 설정이 어느 쪽이든 같게 동작한다. |
| Enable automatic RLS | ✅ **켠다** | `schema.sql`이 9개 표에 전부 RLS를 켜므로 지금은 할 일이 없다. 나중에 누가 표를 하나 더 만들고 RLS를 잊는 경우를 막는 안전망이고, 그게 이 설계가 가장 조용히 깨지는 방식이다. `service_role`은 RLS를 우회하므로 함수에는 영향이 없다. |

만든 뒤 **Settings → API**에서 두 개를 적어 둔다:

- `Project URL` — `https://xxxx.supabase.co`
- `anon public` 키 — 3단계에서 앱에 들어간다. **이건 비밀이 아니다** (앱에 실려 나간다). 자물쇠가 아니라 체이고, 실제 자물쇠는 아래 ③의 토큰이다.

`service_role` 키는 **적어 두지도 말고 어디에도 붙이지 마라.** Edge Function이 환경에서 자동으로 읽는다.

### ② 표 만들기

SQL Editor → New query → [`schema.sql`](schema.sql) 전체를 붙이고 **Run**.

확인: Table Editor에 `site`, `site_token`, `sync_batch`, `product`, `inventory`, `app_user`, `stock_transaction`, `counselling_log`, `product_photo` 9개가 보이고, 각 표 이름 옆에 **RLS enabled** 표시가 있어야 한다.

### ③ 약국 하나와 토큰 등록

```powershell
cd supabase
.\new-token.ps1 -SiteId KH-0001 -Label "첫 약국"
```

토큰이 한 번 출력되고, 그 아래에 **붙여넣을 SQL이 바로 나온다.** 토큰을 비밀번호 관리자에 저장한 뒤 SQL만 SQL Editor에 붙여 실행한다.

SQL에는 **해시만** 들어간다 — 토큰 원본을 SQL Editor에 붙이면 쿼리 기록에 남고, 그 기록은 지우기 번거롭고 지웠는지 확인할 방법도 없다. 서버가 해시만 가지는 것과 같은 이유다.

토큰을 잃으면 복구할 수 없으니 다시 발급해야 한다 (`revoked_at`을 채워 옛것을 막고 새로 발급).

`site_id`는 **가명 코드**다. 약국 이름을 넣지 않는다 — 어느 약국인지는 발급 대장(이 DB 밖)이 들고 있고, 라이선스 시리얼과 같은 방식이다. 항생제 CSV의 `research.site_code`와 같은 값을 쓰면 나중에 맞춰 보기 편하다.

### ④ 함수 올리기

Edge Functions → **Deploy a new function** → 이름 `sync` → [`functions/sync/index.ts`](functions/sync/index.ts) 내용을 붙이고 배포.

**Verify JWT는 켠 채로 둔다.** 그러면 anon 키 없는 요청은 함수 코드에 닿기도 전에 걸러진다. 실제 인증은 `x-campos-token`이 하지만, 체가 하나 더 있어서 나쁠 것은 없다.

CLI를 쓴다면:

```bash
supabase functions deploy sync --project-ref <프로젝트_ref>
```

### ⑤ 확인 — 1단계 미리보기 파일을 그대로 보내 본다

이게 2단계의 검증이다. 앱에서 **Preview Sync Data**로 파일을 하나 받아 두고 (관리자 대시보드 → DATA SYNC), 그 파일을 실제로 POST한다.

```powershell
cd supabase
.\test-upload.ps1 -Url https://xxxx.supabase.co `
                  -AnonKey "<anon public 키>" `
                  -Token "<③에서 만든 토큰>" `
                  -File "$env:USERPROFILE\Desktop\sync-preview-20261010-101500.json"
```

스크립트가 **보낸 건수와 받은 건수를 대조해서** 알려준다. 눈으로 숫자를 맞춰 보는 단계를 없애려는 것이다 — 한 표만 0으로 들어가도 나머지가 맞으면 정상처럼 보인다.

성공하면 이렇게 돌아온다:

```json
{"ok":true,"batch_id":"…","site_id":"KH-0001",
 "accepted":{"product":304,"inventory":324,"app_user":1,
             "stock_transaction":324,"counselling_log":0},
 "positions":{"stock_transaction":324}}
```

그다음 SQL Editor에서:

```sql
select status, row_counts, positions, error, received_at
from public.sync_batch order by received_at desc limit 5;

select count(*) from public.product;           -- 304
select count(*) from public.stock_transaction;  -- 324
select count(*) from public.app_user;           -- 1

-- 금액이 반올림되지 않고 들어왔는지
select product_name, selling_price, selling_price_khr
from public.product where selling_price_khr is not null limit 5;

-- 비밀이 들어갈 자리가 아예 없는지
select column_name from information_schema.columns
where table_name = 'app_user';   -- site_id, user_id, username, role, status, server_received_at
```

**한 번 더 보내 본다.** 같은 명령을 그대로 다시 실행하면 `accepted`는 같고 `count(*)`는 **늘지 않아야** 한다. 늘어나면 키가 잘못 걸린 것이고, 그건 3단계 전에 반드시 잡아야 한다 — 재전송은 끊긴 연결에서 늘 일어난다.

**토큰을 틀리게 보내 본다.** `x-campos-token`을 아무 값으로 바꾸면 `401`과 `This device is not registered for sync.`가 와야 한다.

---

## 사진 (5단계)

사진은 묶음(JSON)에 들어가지 않고 `sync-photo`로 따로 올라간다. 장당 수백 KB라 base64로 실으면 묶음 하나가 수십 MB가 되고, 사진 한 장이 실패하면 그 안의 원장 행까지 전부 다시 보내야 한다.

**파일은 Storage 버킷 `product-photos`에, 표에는 경로만 적는다.** 약국 PC가 사진을 DB 안(BLOB)에 넣는 것과 반대인데, PC 쪽 이유(백업이 `.db` 파일 하나)가 서버에는 없고 — 서버에서 표에 넣으면 제품 한 줄을 읽을 때마다 이미지가 따라온다.

버킷은 **Public을 끈 채로** 만든다. Edge Function만 service_role로 닿고, RLS가 없는 Storage에서 public 버킷은 주소를 아는 누구에게나 열린다.

**경로는 `{site_id}/{product_id}.jpg`로 고정이다.** 해시를 파일 이름에 넣으면 사진을 고칠 때마다 옛 파일이 남아 버킷이 끝없이 커지고, 지우는 일을 누군가 기억해야 한다. 같은 자리에 덮어쓰면 그 일이 없어진다.

**무엇을 올릴지는 해시가 정한다.** `Product_Master.photo_sha256`(지금 사진)과 `photo_synced_sha256`(마지막으로 올린 사진)이 다르면 올린다. 서버에 목록을 물어보지 않는 이유는, 동기화 함수에 **읽는 길이 아예 없기** 때문이다 — 토큰이 새어도 남의 데이터를 가져갈 수 없는 것이 그 덕분이고, 사진 목록을 받아오려고 읽기를 열면 그 성질이 사라진다.

서버도 받은 바이트의 해시를 **다시 센다.** 보내온 해시를 믿고 적으면, 틀린 해시가 저장된 사진은 바뀌어도 다시 올라오지 않거나 안 바뀌었는데 매번 올라온다 — 둘 다 조용하다.

**원장이 사진보다 먼저 올라간다.** 사진이 늦어도 재고와 매출은 맞지만 그 반대는 아니다. 느린 회선에서 사진을 먼저 올리다 시간이 다 가면 그날 판매가 서버에 없다.

---

## 3단계 미리보기

앱에 **Synchronize** 버튼이 붙고, `positions`를 **응답을 받은 뒤에만** 올린다. 보내고 바로 올리면 실패한 묶음의 행들은 다시 선택되지 않고 영구히 빠진다 — `Sync_State`에 쓰는 시점이 그 기능의 전부다.

토큰과 URL은 DPAPI로 PC에 저장한다 (SMTP 앱 비밀번호와 같은 방식). 복사된 DB가 다른 PC에서 전송할 수 없게 되는 것도 같은 이유다.

---

## 새 버전 배포하기

업데이트는 약국에게 **묻지도 알리지도 않고** 일어난다. 그 대가로 올리는 쪽이 신중해야 한다 — 잘못 올린 버전은 이미 받아 간 PC가 있어서 **되돌릴 수 없다.**

### 순서

```
1. deploy.ps1 -Version 1.27 -Channel test
2. test/ 에 올린다      → 본인 PC에서 확인
3. 약국 한 곳에서 확인   → 며칠 써 본다
4. deploy.ps1 -Version 1.27 -Channel stable
5. stable/ 에 올린다    → 전체 약국
```

`deploy.ps1`이 테스트를 먼저 돌리고, 실패하면 패키지를 만들지 않는다. 버전 번호는 [AppVersion.cs](../CamPOS.Application/AppVersion.cs)에서 올라가고 거기서 화면·내보낸 파일 이름·패키지가 전부 나오므로, 약국 화면의 버전과 실제로 설치된 것이 어긋날 수가 없다.

### 올릴 파일

스크립트가 마지막에 목록을 찍어 준다. **`RELEASES-*` 파일을 빼먹지 않는다** — 그것이 목록이고, 없으면 아무도 새 버전을 보지 못한다. 새 버전을 올렸는데 아무 PC도 안 받는다면 거의 항상 이것이다.

| 파일 | 무엇 |
|---|---|
| `RELEASES-{채널}` | **목록.** 이것이 기준이다 |
| `CamPOS-{버전}-{채널}-full.nupkg` | 전체 (~7 MB) |
| `CamPOS-{버전}-{채널}-delta.nupkg` | 변경분 (~2 MB). 있으면 이쪽이 먼저 쓰인다 |
| `CamPOS-{채널}-Setup.exe` | **첫 설치용.** 약국에 처음 깔 때만 |
| `releases.{채널}.json` / `assets.{채널}.json` | 메타 |

`Portable.zip`은 안 올려도 된다 — 설치 없이 쓰는 용도이고 업데이트가 안 된다.

### 옛 버전 정리

`full.nupkg`는 **두세 개 남기고** 지운다. 한동안 안 켠 PC가 중간 버전을 거쳐 따라오는 데 쓰이기 때문이다. `delta`는 바로 앞 버전 것만 있으면 된다.

`RELEASES-*`에 적힌 것을 지우면 그 줄도 같이 지워야 한다 — 목록에 있는데 파일이 없으면 받다가 실패한다.

### 문제 있는 버전이 나갔을 때

**내리는 것으로 해결되지 않는다.** 이미 받아 간 PC는 그 버전으로 돌아가 있다.

```
1. stable/ 에서 그 버전의 nupkg와 RELEASES의 그 줄을 지운다   (더 퍼지는 것만 막는다)
2. 고쳐서 더 높은 번호로 올린다                                 (1.27이 문제면 1.28)
3. 이미 받은 PC는 다음 실행에서 1.28로 올라간다
```

번호를 낮춰 되돌릴 수는 없다. Velopack은 더 높은 번호만 설치한다.

### 급할 때 한 대만

약국 PC의 Admin Dashboard → Updates → 채널을 `test`로 바꾸면 그 PC만 test를 본다. 고친 것을 한 대에서 먼저 보고 싶을 때 쓴다. 확인 뒤 `stable`로 되돌린다.

---

## 약국 하나 등록하기 (지금 방식)

설치할 때 사람이 넣는 것은 **라이선스 코드 하나**다. 동기화 토큰은 앱이 서버에서 받아 와 혼자 저장한다 — 사람이 보지도 만지지도 않는다.

### 라이선스를 발급할 때 서버에도 한 줄

```sql
-- 약국이 처음이면 먼저
insert into public.site (site_id, label)
values ('KH-0002', '두 번째 약국')
on conflict (site_id) do nothing;

-- 발급한 라이선스의 일련번호를 그 약국에 묶는다
insert into public.license_issuance (serial, site_id, label, expires_at)
values (42, 'KH-0002', '두 번째 약국', '2027-10-01');
```

**일련번호는 갱신할 때마다 새로 나온다.** 같은 약국의 내년 라이선스는 다른 `serial`로 같은 `site_id`를 가리키므로, 갱신할 때도 한 줄 추가한다. `site_id`는 그대로다.

### 약국 PC에서

라이선스 코드를 입력하고 활성화하면 끝이다. 앱이 `register` 함수를 불러 토큰을 받아 DPAPI로 저장한다.

인터넷이 없으면 활성화만 되고 동기화는 설정되지 않는다 — **앱은 그대로 쓸 수 있고**, 나중에 Data Center에서 다시 하거나 토큰을 손으로 넣으면 된다.

### 나중에: 발급 도구가 직접 쓰게

지금은 위 SQL을 손으로 넣는다. 발급 도구가 `license_issuance`에 쓰도록 바꾸면 그 한 줄도 사라지고, **Firestore도 접을 수 있다** — 발급 내역이 여기 다 있기 때문이다.

그 전에 `%APPDATA%\PharmaPOS.Issuer`의 대장이 온전한지 확인할 것. 그 폴더를 잃으면 새 라이선스 발급이 영영 불가능하다.

---

## 손으로 만드는 토큰은 언제 쓰나

지금은 설치할 때 **두 가지**를 넣어야 한다 — 라이선스 코드, 그리고 동기화 토큰. 하나여야 맞고, 목표는 **라이선스만 넣으면 토큰이 자동으로 발급되는 것**이다.

```
앱 ──라이선스 코드──> 서버 ┬ 공개키로 서명 확인
                           ├ serial → site_id 조회
                           └ 토큰 발급
앱 <──────토큰─────────────┘  DPAPI로 저장. 사람 눈에 안 보인다.
```

**막고 있는 것은 "어느 약국인가"다.** `LicensePayload.SerialNumber`는 발급 *대장의 행 번호*이지 약국 번호가 아니라서, 갱신하면 같은 약국이 새 번호를 받는다. 그대로 `site_id`로 쓰면 한 약국의 데이터가 해마다 갈라진다.

**해결은 라이선스 포맷을 바꾸는 것이 아니다.** 발급 도구는 발급하는 순간 누구 것인지 알고 있다(대장이 그 매핑을 들고 있다 — `LicensePayload` 주석 참조). 코드 안에 없을 뿐이고, 서버가 필요로 하는 것은 코드 안이 아니라 대장 쪽이다. 그러니 발급 도구가 발급할 때 Supabase에도 `serial → site_id` 한 줄을 쓰면 된다. 이미 Firestore에 쓰고 있으니 같은 모양의 일이다.

그러면 바이트까지 같아야 하는 세 공유 파일도, `license-vectors.json`도 건드리지 않는다. 갱신은 같은 `site_id`에 serial이 하나 더 붙는 것이고, PC를 바꿔도 라이선스만 넣으면 새 토큰이 나온다 — 옛 토큰은 `revoked_at`으로 막는다.

**오프라인 활성화는 깨지지 않는다.** 활성화는 그대로 두고 "서버 연결"을 그다음 단계로 둔다. 동기화는 인터넷이 있어야 돌아가는 기능이므로 거기서 인터넷을 요구하는 비용은 0이다.

지금 손으로 만드는 토큰은 버려지지 않는다 — **서버는 토큰이 어디서 왔는지 보지 않는다.** `site_token`에 줄이 있으면 통과다. 자동 발급은 그 줄을 사람 대신 코드가 넣는 것뿐이다.
