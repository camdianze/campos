# 업데이트 보관소 만들기 (한 번만)

새 버전을 올려 두는 자리다. 동기화에 쓰는 **같은 Supabase 프로젝트**에 버킷 하나를 더 만들 뿐이고, 새 서비스도 새 계정도 필요 없다.

## 왜 공개 읽기인가

| | |
|---|---|
| **읽기** | 공개. 약국 PC가 열쇠 없이 받아야 한다 |
| **쓰기** | 금지. 업로드는 대시보드에서 사람이 한다 |

앱에 업로드 권한을 주지 않는 이유는 그 열쇠가 **약국 PC 수백 대에 뿌려지기** 때문이다. 그중 한 대만 뜯겨도 모든 약국에 아무 버전이나 보낼 수 있게 된다. 받는 쪽만 열어 두면 최악의 경우가 "남이 우리 설치 파일을 내려받는 것"으로 끝나고, 그 파일은 어차피 약국에 나가는 것이다.

동기화 쪽이 RLS로 전부 잠근 것과 반대로 보이지만 같은 원칙이다 — **쓰기 경로를 하나로 줄인다.** 동기화는 Edge Function 하나, 업데이트는 사람 하나.

## ① 버킷 만들기

Supabase → **Storage** → **New bucket**

- 이름: **`releases`**
- **Public bucket: 켠다** ← 동기화 때와 반대다. 설치 파일은 비밀이 아니다

## ② 폴더 두 개

버킷 안에 폴더를 만든다 (파일을 올리면 자동으로 생기기도 한다):

```
releases/
  test/      ← 본인 PC와 약국 한 곳
  stable/    ← 전체 약국
```

**먼저 test, 확인한 뒤 stable.** 잘못 올린 버전은 되돌릴 수 없다 — 이미 받아 간 PC가 있기 때문이고, 수습은 더 높은 번호의 수정판을 올리는 것뿐이다.

## ③ 익명 쓰기가 막혔는지 확인

Public 버킷은 읽기만 열린다. 그래도 한 번 확인한다 — SQL Editor에서:

```sql
-- 이 버킷에 정책이 몇 개 걸려 있나. INSERT/UPDATE/DELETE가 있으면 안 된다.
select name, action, definition
from storage.policies
where bucket_id = 'releases';
```

아무것도 안 나오거나 SELECT만 있으면 정상이다. `INSERT`나 `UPDATE`가 anon에게 열려 있으면 지운다.

> `storage.policies` 뷰가 없는 버전이면 대시보드의 **Storage → releases → Policies**에서 같은 것을 본다.

## ④ 주소 확인

앱에 박혀 있는 주소는 이것이다 ([UpdateSource.cs](../CamPOS.Wpf/Services/UpdateSource.cs)):

```
https://wigewqyblckkguaslupp.supabase.co/storage/v1/object/public/releases
```

버킷을 만든 뒤 브라우저로 이 주소 + `/stable/RELEASES-stable`을 열어 본다. 아직 아무것도 안 올렸으면 404가 정상이고, **400이나 403이면 버킷 이름이나 공개 설정이 틀린 것**이다.

## 용량

| | |
|---|---|
| 파일 하나 한도 | 50 MB |
| 전체 패키지 | **약 7 MB** |
| 변경분(delta) | 약 2 MB |

한도의 14%다. 약국이 열 곳으로 늘어도 전송량이 문제가 될 자리가 아니다.
