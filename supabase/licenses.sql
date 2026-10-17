-- 라이선스와 토큰이 서버에 어떻게 들어와 있는지.
--
-- Supabase SQL Editor에 붙여 놓고 이름을 licenses.sql로 바꿔 두면, 저장소의 이
-- 파일이 원본임을 다음 사람이 알 수 있다. 콘솔에서만 고치면 둘이 갈라지고,
-- 프로젝트를 새로 만들 때 그 수정이 없다.
--
-- 서버에 <b>없는</b> 것 두 가지를 기억해 둘 것:
--   · 라이선스 코드 자체 — 검증은 공개키로 하므로 코드를 둘 이유가 없고,
--     두면 서버가 털렸을 때 모든 약국의 라이선스가 함께 샌다.
--   · 토큰 원본 — 지문(SHA-256)만 있다. 대조에는 그것으로 충분하다.

-- ── 약국별 요약 ────────────────────────────────────────────────────────────
select
    l.serial                                   as "발급번호",
    l.site_id                                  as "약국코드",
    l.label                                    as "약국",
    to_char(l.issued_at at time zone 'Asia/Seoul', 'YYYY-MM-DD') as "등록일",
    coalesce(to_char(l.expires_at, 'YYYY-MM-DD'), '무기한')      as "만료",
    count(t.token_sha256)                      as "쓰는 토큰",
    coalesce(to_char(max(t.last_used_at) at time zone 'Asia/Seoul', 'MM-DD HH24:MI'),
             '사용 안 함')                      as "마지막 사용"
from public.license_issuance l
left join public.site_token t
       on t.site_id = l.site_id and t.revoked_at is null
group by l.serial, l.site_id, l.label, l.issued_at, l.expires_at
order by l.serial;


-- ── 토큰 하나하나 ──────────────────────────────────────────────────────────
-- label로 출처가 구분된다:
--   'licence 10'  → 활성화할 때 register 함수가 발급한 것 (정상 경로)
--   그 밖         → new-token.ps1로 손수 만든 것 (인터넷 없는 설치의 대비책)
--
-- 한 약국에 여러 줄이 있는 것은 이상하지 않다 — PC를 바꾸거나 한 대 더 놓으면
-- 는다. 떠난 PC의 토큰은 revoked_at으로 막는다. 지우지 않는 것은, 나중에
-- "이 토큰이 언제 왜 막혔나"를 볼 수 있어야 하기 때문이다.

-- select
--     site_id                                    as "약국코드",
--     label                                      as "어디서 왔나",
--     left(token_sha256, 12) || '…'              as "지문",
--     to_char(created_at at time zone 'Asia/Seoul', 'MM-DD HH24:MI')   as "발급",
--     coalesce(to_char(last_used_at at time zone 'Asia/Seoul', 'MM-DD HH24:MI'), '-') as "마지막 사용",
--     case when revoked_at is null then '사용중' else '해지됨' end      as "상태"
-- from public.site_token
-- order by created_at;


-- ── 떠난 PC의 토큰 막기 ────────────────────────────────────────────────────
-- update public.site_token set revoked_at = now()
-- where site_id = 'KH-0002' and label = '옛 PC';


-- ── 아직 서버에 등록되지 않은 라이선스 찾기 ────────────────────────────────
-- 발급은 했는데 license_issuance에 넣는 것을 잊으면, 그 약국은 활성화할 때
-- "Licence N is not registered on the server yet."를 받는다. 발급 대장
-- (%APPDATA%\PharmaPOS.Issuer\licenses.csv)의 번호와 아래 목록을 맞춰 본다.

-- select serial from public.license_issuance order by serial;
