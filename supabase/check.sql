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

select
    'product'            as "표", count(*)::text as "행 수" from public.product
union all select 'inventory',         count(*)::text from public.inventory
union all select 'app_user',          count(*)::text from public.app_user
union all select 'stock_transaction', count(*)::text from public.stock_transaction
union all select 'counselling_log',   count(*)::text from public.counselling_log
union all select '───────────────',   '───────'
union all select '마지막 전송',
       to_char(max(received_at) at time zone 'Asia/Phnom_Penh', 'MM-DD HH24:MI')
  from public.sync_batch
union all select '마지막 전송 상태',
       (select status from public.sync_batch order by received_at desc limit 1)
union all select '전송 횟수',          count(*)::text from public.sync_batch
union all select '실패한 전송',
       count(*)::text from public.sync_batch where status <> 'ok'
union all select '가장 최근 원장 시각',
       to_char(to_timestamp(max(transaction_time) / 1000) at time zone 'Asia/Phnom_Penh',
               'MM-DD HH24:MI')
  from public.stock_transaction;
