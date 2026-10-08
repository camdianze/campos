# CamPOS v2 — 서버 쪽 (2단계)

이 폴더는 **서버가 받을 자리**다. 앱은 아직 아무것도 보내지 않는다 — 전송은 3단계다.

| 파일 | 무엇 |
|---|---|
| `schema.sql` | 표 전부. SQL Editor에 붙여 실행한다. |
| `functions/sync/index.ts` | Edge Function. 서버로 들어가는 유일한 길. |

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

## 할 일

### ① 프로젝트 만들기

[supabase.com](https://supabase.com) → New project

- **Region: Southeast Asia (Singapore)** — 캄보디아에서 가장 가깝다
- 데이터베이스 비밀번호는 적어 두기 (분실하면 재설정만 가능)

만든 뒤 **Settings → API**에서 두 개를 적어 둔다:

- `Project URL` — `https://xxxx.supabase.co`
- `anon public` 키 — 3단계에서 앱에 들어간다. **이건 비밀이 아니다** (앱에 실려 나간다). 자물쇠가 아니라 체이고, 실제 자물쇠는 아래 ③의 토큰이다.

`service_role` 키는 **적어 두지도 말고 어디에도 붙이지 마라.** Edge Function이 환경에서 자동으로 읽는다.

### ② 표 만들기

SQL Editor → New query → [`schema.sql`](schema.sql) 전체를 붙이고 **Run**.

확인: Table Editor에 `site`, `site_token`, `sync_batch`, `product`, `inventory`, `app_user`, `stock_transaction`, `counselling_log`, `product_photo` 9개가 보이고, 각 표 이름 옆에 **RLS enabled** 표시가 있어야 한다.

### ③ 약국 하나와 토큰 등록

먼저 토큰을 만든다 (PowerShell):

```powershell
# 32바이트 난수 → hex 64자
-join ((1..32) | ForEach-Object { '{0:x2}' -f (Get-Random -Max 256) })
```

나온 문자열을 **안전한 곳에 적어 둔다.** 서버는 해시만 가지므로 잃어버리면 다시 발급해야 한다.

SQL Editor에서 (`site_id`와 토큰을 자기 값으로 바꿔서):

```sql
insert into public.site (site_id, label)
values ('KH-0001', '첫 약국')
on conflict (site_id) do nothing;

insert into public.site_token (token_sha256, site_id, label)
values (
    encode(sha256('여기에_위에서_만든_토큰'::bytea), 'hex'),
    'KH-0001',
    '사무실 PC'
);
```

`site_id`는 **가명 코드**다. 약국 이름을 넣지 않는다 — 어느 약국인지는 발급 대장(이 DB 밖)이 들고 있고, 라이선스 시리얼과 같은 방식이다. 항생제 CSV의 `research.site_code`와 같은 값을 쓰면 나중에 맞춰 보기 편하다.

### ④ 함수 올리기

Edge Functions → **Deploy a new function** → 이름 `sync` → [`functions/sync/index.ts`](functions/sync/index.ts) 내용을 붙이고 배포.

**Verify JWT는 켠 채로 둔다.** 그러면 anon 키 없는 요청은 함수 코드에 닿기도 전에 걸러진다. 실제 인증은 `x-campos-token`이 하지만, 체가 하나 더 있어서 나쁠 것은 없다.

CLI를 쓴다면:

```bash
supabase functions deploy sync --project-ref <프로젝트_ref>
```

### ⑤ 확인 — 1단계 미리보기 파일을 그대로 보내 본다

이게 2단계의 검증이다. 바탕화면에 있는 `sync-preview-*.json`을 실제로 POST한다.

```powershell
$url   = "https://xxxx.supabase.co/functions/v1/sync"
$anon  = "<anon public 키>"
$token = "<③에서 만든 토큰>"
$file  = "$env:USERPROFILE\Desktop\sync-preview-20261008-185904.json"

curl.exe -X POST $url `
  -H "Authorization: Bearer $anon" `
  -H "x-campos-token: $token" `
  -H "content-type: application/json" `
  --data-binary "@$file"
```

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

## 3단계 미리보기

앱에 **Synchronize** 버튼이 붙고, `positions`를 **응답을 받은 뒤에만** 올린다. 보내고 바로 올리면 실패한 묶음의 행들은 다시 선택되지 않고 영구히 빠진다 — `Sync_State`에 쓰는 시점이 그 기능의 전부다.

토큰과 URL은 DPAPI로 PC에 저장한다 (SMTP 앱 비밀번호와 같은 방식). 복사된 DB가 다른 PC에서 전송할 수 없게 되는 것도 같은 이유다.
