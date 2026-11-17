-- 서버에 무엇이 들어와 있는지 한눈에.
--
-- Supabase SQL Editor에 붙여 놓고 Synchronize를 누를 때마다 다시 Run 한다.
-- 저장된 쿼리 이름은 check.sql로 두면 저장소의 이 파일이 원본임을 알 수 있다.
--
-- 보는 법:
--   · 전송한 직후인데 last_batch의 received_at이 안 바뀌었다 → 서버에 닿지 않았다
--   · status가 failed다 → error 열에 사유가 그대로 적혀 있다
--   · 같은 묶음을 두 번 보냈는데 product 수가 늘었다 → 키가 잘못 걸린 것이다
--     (그런 일은 없어야 한다. 키가 (site_id, 원본 ID)라 제자리에서 갱신된다.)
--
-- 시각에 시간대 이름을 붙여 적는 이유: 서버는 UTC로 저장하고 보여줄 때만 시간대를
-- 고르는데, 이름이 없으면 읽는 사람이 자기 시계로 읽는다. 프놈펜은 한국보다 두
-- 시간 느리다 — 두 시간 전에 보낸 것처럼 보이는 전송이 사실 방금 것이다.
-- 약국 데이터의 날짜 경계는 프놈펜 자정이고(PhnomPenhClock), 이 쿼리를 읽는
-- 사람은 한국에 있으므로 둘 다 적는다.
-- 원장 시각은 프놈펜만 적는다 — 그것은 약국에서 일어난 일의 시각이다.

select
    'product'            as "표", count(*)::text as "행 수" from public.product
union all select 'inventory',         count(*)::text from public.inventory
union all select 'app_user',          count(*)::text from public.app_user
union all select 'stock_transaction', count(*)::text from public.stock_transaction
union all select 'counselling_log',   count(*)::text from public.counselling_log
union all select '───────────────',   '───────'
union all select '마지막 전송 (프놈펜)',
       to_char(max(received_at) at time zone 'Asia/Phnom_Penh', 'MM-DD HH24:MI')
  from public.sync_batch
union all select '마지막 전송 (한국)',
       to_char(max(received_at) at time zone 'Asia/Seoul', 'MM-DD HH24:MI')
  from public.sync_batch
union all select '마지막 전송 상태',
       (select status from public.sync_batch order by received_at desc limit 1)
union all select '전송 횟수',          count(*)::text from public.sync_batch
union all select '실패한 전송',
       count(*)::text from public.sync_batch where status <> 'ok'
union all select '가장 최근 원장 시각 (프놈펜)',
       to_char(to_timestamp(max(transaction_time) / 1000) at time zone 'Asia/Phnom_Penh',
               'MM-DD HH24:MI')
  from public.stock_transaction;


-- ── 약국별 최근 7일 기능 사용 ──────────────────────────────────────────────
-- 배포 직후와 파일럿 중에 제일 자주 보게 되는 쿼리다. 어느 약국이 무엇을 쓰고
-- 있는지, 그리고 <b>무엇이 0인지</b>가 한 화면에 나온다.
--
-- 0으로 남은 기능은 셋 중 하나다 — 필요가 없거나, 찾지 못하거나, 눌렀는데 안
-- 되거나. 마지막 것만 실패 칸이 말해 주므로, 실패가 0인데 사용도 0이면 앞의 둘이다.
--
-- 날짜 경계는 프놈펜 자정이다. 앱의 모든 날짜가 그렇고, UTC로 자르면 저녁 늦게
-- 쓴 것이 다음 날로 넘어간다.
select
    u.site_id                                                   as "약국",
    u.event_name                                                as "기능",
    count(*)                                                    as "횟수",
    count(*) filter (where u.outcome = 'failure')               as "실패",
    count(distinct (to_timestamp(u.occurred_at / 1000)
                    at time zone 'Asia/Phnom_Penh')::date)      as "쓴 날",
    to_char(max(to_timestamp(u.occurred_at / 1000))
            at time zone 'Asia/Phnom_Penh', 'MM-DD HH24:MI')    as "마지막"
from public.usage_events u
where to_timestamp(u.occurred_at / 1000)
      > now() - interval '7 days'
group by u.site_id, u.event_name
order by u.site_id, count(*) desc;
