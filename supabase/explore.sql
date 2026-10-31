-- CamPOS — 모아 둔 데이터를 들여다보는 쿼리 모음.
--
-- SQL Editor에 붙여 놓고 필요한 블록만 골라 실행한다. Supabase는 여러 문장을
-- 붙여 넣으면 <b>마지막 것의 결과만</b> 보여주므로, 한 번에 하나씩 돌린다.
-- 블록을 드래그해서 선택하고 Run 하면 그것만 실행된다.
--
-- ── 읽기 전에 알아야 할 네 가지 ─────────────────────────────────────────────
--
--  1. 시각은 bigint(epoch 밀리초)다.  to_timestamp(x / 1000)
--     날짜 경계는 프놈펜 자정이다 — 앱의 모든 날짜가 그렇다. UTC로 자르면
--     저녁 늦은 판매가 다음 날로 넘어간다.
--
--  2. 환불은 음수다.  순매출은 transaction_type in ('StockOut','Refund')로 세야
--     한다. 'StockOut'만 세면 총매출(환불 전)이 나오는데, 화면에는 아무 표시가
--     없어서 틀린 줄을 모른다. 반대로 <b>판매 건수</b>는 StockOut만 세야 한다 —
--     환불은 또 하나의 판매가 아니다.
--
--  3. expiry_date = 0 은 "유통기한 모름"이다. 1970-01-01이 아니다.
--     날짜와 비교하는 모든 쿼리에서 따로 빼야 한다.
--
--  4. 약국이 여럿이 되면 site_id로 거른다. 지금은 하나뿐이라 전부 나온다.
--     where site_id = 'KH-0001'


-- ════════════════════════════════════════════════════════════════════════════
-- 0. 지금 뭐가 들어와 있나
-- ════════════════════════════════════════════════════════════════════════════
select 'product'            as "표", count(*) as "행" from public.product
union all select 'inventory',         count(*) from public.inventory
union all select 'app_user',          count(*) from public.app_user
union all select 'stock_transaction', count(*) from public.stock_transaction
union all select 'counselling_log',   count(*) from public.counselling_log
union all select 'usage_events',      count(*) from public.usage_events
union all select 'product_photo',     count(*) from public.product_photo;


-- ════════════════════════════════════════════════════════════════════════════
-- 1. 약국 목록
-- ════════════════════════════════════════════════════════════════════════════
-- label은 발급 대장에서 온다 — 약국 PC가 자기 이름을 보낸 적은 없다.
select
    s.site_id                                   as "약국코드",
    l.label                                     as "약국",
    to_char(s.created_at at time zone 'Asia/Seoul', 'YYYY-MM-DD') as "등록",
    (select count(*) from public.product p where p.site_id = s.site_id)  as "품목",
    (select count(*) from public.stock_transaction t where t.site_id = s.site_id) as "원장"
from public.site s
left join lateral (
    select label from public.license_issuance
    where site_id = s.site_id order by serial desc limit 1
) l on true
order by s.site_id;


-- ════════════════════════════════════════════════════════════════════════════
-- 2. 의약품 목록
-- ════════════════════════════════════════════════════════════════════════════
select
    product_name                        as "상품",
    manufacturer                        as "제조사",
    generic_name                        as "성분",
    strength                            as "함량",
    unit                                as "단위",
    units_per_box                       as "박스당",
    case when sells_loose then '예' else '-' end as "낱개판매",
    round(selling_price, 4)             as "판매가($)",
    round(cost_price, 4)                as "원가($)",
    selling_price_khr                   as "판매가(적은그대로,៛)",
    safety_stock_level                  as "안전재고",
    status                              as "상태"
from public.product
order by product_name;


-- ════════════════════════════════════════════════════════════════════════════
-- 3. 판매 — 일별 순매출
-- ════════════════════════════════════════════════════════════════════════════
select
    to_char(to_timestamp(transaction_time / 1000) at time zone 'Asia/Phnom_Penh',
            'YYYY-MM-DD')                                   as "날짜",
    count(*) filter (where transaction_type = 'StockOut')   as "판매줄",
    count(*) filter (where transaction_type = 'Refund')     as "환불줄",
    round(sum(total_amount), 2)                             as "순매출($)"
from public.stock_transaction
where transaction_type in ('StockOut', 'Refund')
group by 1
order by 1 desc;


-- ════════════════════════════════════════════════════════════════════════════
-- 4. 판매 — 상품 순위
-- ════════════════════════════════════════════════════════════════════════════
select
    p.product_name                  as "상품",
    p.manufacturer                  as "제조사",
    sum(t.quantity)                 as "수량",
    round(sum(t.total_amount), 2)   as "금액($)"
from public.stock_transaction t
join public.product p
  on p.site_id = t.site_id and p.product_id = t.product_id
where t.transaction_type in ('StockOut', 'Refund')
group by 1, 2
order by 4 desc nulls last
limit 30;


-- ════════════════════════════════════════════════════════════════════════════
-- 5. 판매 — 마진
-- ════════════════════════════════════════════════════════════════════════════
-- <b>원가가 있는 줄만</b> 센다. cost_price_at_transaction이 NULL인 줄은
-- "그 약국이 매입가를 몰랐다"이거나 이 열이 생기기 전의 거래다. 0으로 치면
-- 마진이 100%로 나오는데, 그럴듯해 보여서 틀린 줄을 모른다.
--
-- 그래서 "원가 모름" 줄 수를 같이 보여준다 — 그 수가 크면 아래 마진은
-- 전체가 아니라 일부의 이야기다.
select
    to_char(to_timestamp(t.transaction_time / 1000) at time zone 'Asia/Phnom_Penh',
            'YYYY-MM')                                          as "월",
    count(*) filter (where t.cost_price_at_transaction is null)  as "원가모름(줄)",
    count(*) filter (where t.cost_price_at_transaction is not null) as "계산됨(줄)",
    round(sum(t.total_amount)
          filter (where t.cost_price_at_transaction is not null), 2)   as "매출($)",
    round(sum(t.quantity * t.cost_price_at_transaction)
          filter (where t.cost_price_at_transaction is not null), 2)   as "원가($)",
    round(sum(t.total_amount - t.quantity * t.cost_price_at_transaction)
          filter (where t.cost_price_at_transaction is not null), 2)   as "마진($)"
from public.stock_transaction t
where t.transaction_type in ('StockOut', 'Refund')
group by 1
order by 1 desc;


-- ════════════════════════════════════════════════════════════════════════════
-- 6. 판매 — 영수증 단위로 묶어 보기
-- ════════════════════════════════════════════════════════════════════════════
-- 판매 헤더 표가 없다. 한 판매는 (transaction_time, user_id)가 같은 줄들이고,
-- 앱도 같은 방식으로 묶는다. 영수증 번호는 서버로 보내지 않으므로, 종이 번호로
-- 서버를 뒤지는 것은 지금은 안 된다 — 그건 약국 PC의 Sales History에서 한다.
select
    to_char(to_timestamp(transaction_time / 1000) at time zone 'Asia/Phnom_Penh',
            'MM-DD HH24:MI')            as "시각",
    count(*)                            as "품목",
    round(sum(total_amount), 2)         as "금액($)",
    max(payment_method)                 as "결제"
from public.stock_transaction
where transaction_type in ('StockOut', 'Refund')
group by transaction_time, user_id
order by transaction_time desc
limit 30;


-- ════════════════════════════════════════════════════════════════════════════
-- 7. 재고 — 현재 수량과 유통기한
-- ════════════════════════════════════════════════════════════════════════════
select
    p.product_name          as "상품",
    i.batch_number          as "배치",
    case when i.expiry_date = 0 then '모름'
         else to_char(to_timestamp(i.expiry_date / 1000), 'YYYY-MM-DD') end as "유통기한",
    i.box_quantity          as "박스",
    i.unit_quantity         as "낱개",
    i.current_quantity      as "총낱개"
from public.inventory i
join public.product p
  on p.site_id = i.site_id and p.product_id = i.product_id
where i.current_quantity > 0
order by (i.expiry_date = 0), i.expiry_date;


-- ════════════════════════════════════════════════════════════════════════════
-- 8. 재고 — 곧 만료되는 것
-- ════════════════════════════════════════════════════════════════════════════
-- expiry_date = 0을 반드시 빼야 한다. 안 빼면 "유통기한 모름" 배치가 전부
-- 1970년에 만료된 것으로 올라온다.
select
    p.product_name                                              as "상품",
    i.batch_number                                              as "배치",
    to_char(to_timestamp(i.expiry_date / 1000), 'YYYY-MM-DD')   as "유통기한",
    (to_timestamp(i.expiry_date / 1000)::date - current_date)   as "남은일수",
    i.current_quantity                                          as "수량"
from public.inventory i
join public.product p
  on p.site_id = i.site_id and p.product_id = i.product_id
where i.current_quantity > 0
  and i.expiry_date <> 0
  and to_timestamp(i.expiry_date / 1000)::date < current_date + 90
order by i.expiry_date;


-- ════════════════════════════════════════════════════════════════════════════
-- 9. 재고 추적 — 한 배치의 장부
-- ════════════════════════════════════════════════════════════════════════════
-- stock_after와 다음 줄의 stock_before가 어긋나는 자리가, 장부 없이 재고가
-- 움직인 지점이다. 상품 이름을 넣어 쓴다.
select
    to_char(to_timestamp(t.transaction_time / 1000) at time zone 'Asia/Phnom_Penh',
            'MM-DD HH24:MI')    as "시각",
    t.transaction_type          as "종류",
    t.batch_number              as "배치",
    t.quantity                  as "수량",
    t.stock_before              as "직전",
    t.stock_after               as "직후",
    t.reason                    as "사유"
from public.stock_transaction t
join public.product p
  on p.site_id = t.site_id and p.product_id = t.product_id
where p.product_name ilike '%여기에 상품 이름%'
order by t.transaction_time;


-- ════════════════════════════════════════════════════════════════════════════
-- 10. 항생제 (AMR)
-- ════════════════════════════════════════════════════════════════════════════
-- 총계로 센다(환불을 빼지 않는다). 상담은 일어난 사건이고, 환불이 그것을
-- 되돌리지는 않는다 — 앱의 항생제 리포트도 같은 규칙이다.
select
    aware_group                                 as "등급",
    count(*)                                    as "상담",
    count(*) filter (where printed)             as "인쇄함",
    round(100.0 * count(*) filter (where printed) / nullif(count(*), 0), 1) as "인쇄율(%)"
from public.counselling_log
group by 1
order by 1;


-- ════════════════════════════════════════════════════════════════════════════
-- 11. 사용 흔적 — 날것
-- ════════════════════════════════════════════════════════════════════════════
-- 집계는 metric_* 뷰가 해 준다(아래 12번). 이건 "무슨 줄이 들어와 있나"를
-- 눈으로 볼 때 쓴다.
select
    to_char(to_timestamp(occurred_at / 1000) at time zone 'Asia/Phnom_Penh',
            'MM-DD HH24:MI')                as "시각",
    event_name                              as "이벤트",
    outcome                                 as "결과",
    coalesce(role, '(로그인 전)')            as "역할",
    app_version                             as "버전"
from public.usage_events
order by occurred_at desc
limit 50;


-- ════════════════════════════════════════════════════════════════════════════
-- 11b. 사용 흔적 — 22개 전부, 안 쓴 것까지
-- ════════════════════════════════════════════════════════════════════════════
-- usage_events를 그냥 group by 하면 <b>일어난 것만</b> 나온다. 정작 알고 싶은 것은
-- "아무도 리포트를 열지 않았다" 쪽이므로, 이름 22개를 먼저 깔고 왼쪽 조인한다.
--
-- 0으로 남는 줄이 이 쿼리의 요점이다. 기능이 안 쓰이는 이유는 셋 중 하나이고
-- 셋이 전부 다른 일이다 — 필요가 없거나, 찾지 못하거나, 눌렀는데 안 되거나.
--
-- 이름은 UsageEventNames의 고정 목록이다. 거기 없는 이름은 기록되지 않으므로
-- 아래 목록과 어긋날 수 없다 — 늘리려면 동의서부터 고쳐야 한다.
with catalogue(ord, grp, event_name) as (
    values
        (1, '화면', 'sales'),
        (1, '화면', 'products'),
        (1, '화면', 'inventory'),
        (1, '화면', 'stock_in'),
        (1, '화면', 'alerts'),
        (1, '화면', 'reports'),
        (1, '화면', 'sales_history'),
        (1, '화면', 'stock_history'),
        (1, '화면', 'data_center'),

        (2, '행동', 'sale_completed'),
        (2, '행동', 'receipt_printed'),
        (2, '행동', 'stock_in_saved'),
        (2, '행동', 'adjustment_saved'),
        (2, '행동', 'refund_saved'),
        (2, '행동', 'label_printed'),
        (2, '행동', 'import_completed'),
        (2, '행동', 'backup_created'),
        (2, '행동', 'sync_manual'),

        (3, '알림', 'expiry_alert_opened'),
        (3, '알림', 'low_stock_alert_opened'),

        (4, '기본', 'app_started'),
        (4, '기본', 'login')
)
select
    c.grp                                                        as "묶음",
    c.event_name                                                 as "기능",
    count(u.event_id)                                            as "전체",
    count(u.event_id) filter (where u.outcome = 'shown')         as "열림",
    count(u.event_id) filter (where u.outcome = 'success')       as "성공",
    count(u.event_id) filter (where u.outcome = 'failure')       as "실패",
    count(u.event_id) filter (where u.outcome = 'skipped')       as "건너뜀",
    coalesce(
        to_char(to_timestamp(max(u.occurred_at) / 1000) at time zone 'Asia/Phnom_Penh',
                'MM-DD HH24:MI'),
        '-')                                                     as "마지막"
from catalogue c
left join public.usage_events u on u.event_name = c.event_name
-- 약국이 여럿이면 여기에 조건을 더한다:  and u.site_id = 'KH-0001'
group by c.ord, c.grp, c.event_name
order by c.ord, count(u.event_id) desc, c.event_name;


-- ════════════════════════════════════════════════════════════════════════════
-- 11c. 한 번도 안 쓴 기능만
-- ════════════════════════════════════════════════════════════════════════════
-- 위 쿼리에서 0인 줄만 뽑는다. 파일럿 보고서에 "이 기능은 쓰이지 않았다"를
-- 적을 때 쓴다.
select c.event_name as "한 번도 안 쓴 기능"
from (values
        ('sales'),('products'),('inventory'),('stock_in'),('alerts'),('reports'),
        ('sales_history'),('stock_history'),('data_center'),
        ('sale_completed'),('receipt_printed'),('stock_in_saved'),('adjustment_saved'),
        ('refund_saved'),('label_printed'),('import_completed'),('backup_created'),
        ('sync_manual'),
        ('expiry_alert_opened'),('low_stock_alert_opened'),
        ('app_started'),('login')
     ) as c(event_name)
where not exists (
    select 1 from public.usage_events u where u.event_name = c.event_name
)
order by 1;


-- ════════════════════════════════════════════════════════════════════════════
-- 12. 사용 흔적 — 지표 뷰
-- ════════════════════════════════════════════════════════════════════════════
-- metrics.sql이 만든 여섯 개. 집계는 여기 한 벌만 두고, 읽는 쪽이 각자
-- 집계하지 않는다 — 그러면 같은 질문에 숫자가 둘 생긴다.

select * from public.metric_active_days     order by month desc, site_id;
-- select * from public.metric_feature_use     order by month desc, uses desc;
-- select * from public.metric_role_mix        order by month desc, events desc;
-- select * from public.metric_error_rate      where failures > 0 order by pct_failed desc;
-- select * from public.metric_expiry_response order by month desc;
-- select * from public.metric_last_sync       order by days_quiet desc nulls last;


-- ════════════════════════════════════════════════════════════════════════════
-- 13. 전송이 멎은 약국 찾기
-- ════════════════════════════════════════════════════════════════════════════
-- 자동 전송은 실패해도 조용하다(계산대에 오류창을 띄울 수 없으므로). 그래서
-- 멎은 것을 알아차릴 자리가 여기다. days_quiet가 커지는 곳에 전화한다.
select * from public.metric_last_sync order by days_quiet desc nulls last;


-- ════════════════════════════════════════════════════════════════════════════
-- 14. 사진
-- ════════════════════════════════════════════════════════════════════════════
-- 파일은 Storage 버킷 product-photos에 있고 여기엔 경로만 있다. 버킷은
-- Public이 꺼져 있으므로 주소만으로는 안 열린다 — 콘솔 Storage에서 받는다.
select
    p.product_name                              as "상품",
    ph.storage_path                             as "경로",
    round(ph.byte_size / 1024.0)                as "KB",
    to_char(ph.server_received_at at time zone 'Asia/Seoul', 'MM-DD HH24:MI') as "받은시각"
from public.product_photo ph
join public.product p
  on p.site_id = ph.site_id and p.product_id = ph.product_id
order by ph.server_received_at desc;


-- ════════════════════════════════════════════════════════════════════════════
-- 15. CSV로 빼기
-- ════════════════════════════════════════════════════════════════════════════
-- 아무 쿼리나 Run 한 뒤 결과 오른쪽 위 <b>Export → Download CSV</b>를 누르면 된다.
-- 따로 쓸 SQL은 없다.
--
-- 엑셀에서 한글이 깨지면, 엑셀 대신 구글 시트로 열거나 엑셀의
-- 데이터 → 텍스트/CSV 가져오기에서 인코딩을 UTF-8로 고른다.
