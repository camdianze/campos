-- CamPOS — 성과 지표 뷰.
--
-- schema.sql을 먼저 실행한 뒤 이 파일을 실행한다. 여기 있는 것은 전부 뷰와 권한뿐이고
-- 표를 만들거나 고치지 않으므로, 몇 번을 다시 실행해도 같다.
--
-- ── 왜 뷰인가 ────────────────────────────────────────────────────────────────
-- 지표를 읽는 쪽(파이썬·Streamlit·나중의 웹 화면)에 표를 직접 열어 주면, 그쪽이
-- 자기 나름의 집계를 쓰기 시작한다. 그러면 같은 질문에 두 개의 숫자가 생기고 어느
-- 쪽이 맞는지 아무도 모른다. 집계는 여기 한 벌만 둔다.
--
-- ── 읽는 쪽에 주는 권한 ──────────────────────────────────────────────────────
-- 표에는 RLS가 켜져 있고 정책이 하나도 없어서 아무도 직접 읽지 못한다. 뷰는
-- 소유자(postgres) 권한으로 도는 보통 뷰라 표를 읽을 수 있고, 아래에서 만드는
-- campos_metrics 역할에는 <b>뷰만</b> SELECT를 준다 — 지표를 보는 사람이
-- 약국 원장을 통째로 긁어 갈 길은 열지 않는다.
--
-- ── 여기 없는 것 ─────────────────────────────────────────────────────────────
-- <b>개인별 사용 기록 뷰는 없다.</b> usage_events에 user_id를 담지 않기로 했기
-- 때문이고, 그래서 만들 수가 없다. 역할(role) 단위까지가 전부다.


-- ════════════════════════════════════════════════════════════════════════════
-- 1. 약국별 월간 활성 사용일
-- ════════════════════════════════════════════════════════════════════════════
-- "판매가 한 건이라도 있었던 날"을 센다. 앱을 켠 날이 아니라 <b>장사에 쓴 날</b>이
-- 지속 사용의 척도다 — 켜 두기만 한 PC는 매일 app_started를 남긴다.
--
-- 날짜 경계는 프놈펜 자정이다. 앱의 모든 날짜 경계가 그렇고(PhnomPenhClock),
-- UTC로 자르면 저녁 늦은 판매가 다음 날로 넘어가 활성일이 부풀거나 쪼개진다.
create or replace view public.metric_active_days as
select
    t.site_id,
    to_char(
        date_trunc('month',
            to_timestamp(t.transaction_time / 1000) at time zone 'Asia/Phnom_Penh'),
        'YYYY-MM')                                          as month,
    count(distinct
        (to_timestamp(t.transaction_time / 1000) at time zone 'Asia/Phnom_Penh')::date
    )                                                       as active_days,
    count(*) filter (where t.transaction_type = 'StockOut')  as sale_lines
from public.stock_transaction t
where t.transaction_type in ('StockOut', 'Refund')
group by 1, 2;


-- ════════════════════════════════════════════════════════════════════════════
-- 2. 기능별 사용 횟수와 비율
-- ════════════════════════════════════════════════════════════════════════════
-- 비율의 분모는 그 약국 그 달의 전체 이벤트다. 약국마다 규모가 다르므로 횟수만
-- 보면 큰 약국이 모든 줄에서 이긴다 — 알고 싶은 것은 "이 약국이 무엇에 시간을
-- 쓰는가"이고, 그것은 비율로만 보인다.
create or replace view public.metric_feature_use as
with per_month as (
    select
        site_id,
        to_char(
            date_trunc('month',
                to_timestamp(occurred_at / 1000) at time zone 'Asia/Phnom_Penh'),
            'YYYY-MM')          as month,
        event_name,
        count(*)                as uses
    from public.usage_events
    group by 1, 2, 3
)
select
    site_id,
    month,
    event_name,
    uses,
    round(100.0 * uses / sum(uses) over (partition by site_id, month), 1) as pct_of_month
from per_month;


-- ════════════════════════════════════════════════════════════════════════════
-- 3. 유통기한 알림을 본 뒤 7일 안의 재고 처리
-- ════════════════════════════════════════════════════════════════════════════
-- <b>이 뷰가 재는 것을 정확히 알고 읽어야 한다.</b>
--
-- 사용 기록에는 상품 ID가 없다(일부러 없다). 그래서 "그 알림을 보고 그 상품을
-- 처리했는가"는 알 수 없고, 이 뷰가 세는 것은 다음 한 가지다:
--
--     알림을 본 날로부터 7일 안에, 그 약국에서 조정이나 반품이 한 건이라도 있었나
--
-- 인과가 아니라 동반이다. 매일 조정을 하는 약국은 늘 100%로 나온다. 파일럿에서
-- "알림을 보긴 보는가, 보고 나면 움직이는가"를 가늠하는 용도이지, 알림의 효과를
-- 증명하는 숫자가 아니다. 증명하려면 알림에 상품 ID를 실어야 하고, 그것은 약국에게
-- 한 약속을 바꾸는 일이다.
create or replace view public.metric_expiry_response as
with opened as (
    select distinct
        site_id,
        (to_timestamp(occurred_at / 1000) at time zone 'Asia/Phnom_Penh')::date as opened_on
    from public.usage_events
    where event_name = 'expiry_alert_opened'
)
select
    o.site_id,
    to_char(date_trunc('month', o.opened_on), 'YYYY-MM')  as month,
    count(*)                                             as days_alert_opened,
    count(*) filter (where exists (
        select 1
        from public.stock_transaction t
        where t.site_id = o.site_id
          and t.transaction_type in ('Adjustment', 'Refund')
          and (to_timestamp(t.transaction_time / 1000)
                   at time zone 'Asia/Phnom_Penh')::date
              between o.opened_on and o.opened_on + 7
    ))                                                   as days_followed_by_action,
    round(100.0 * count(*) filter (where exists (
        select 1
        from public.stock_transaction t
        where t.site_id = o.site_id
          and t.transaction_type in ('Adjustment', 'Refund')
          and (to_timestamp(t.transaction_time / 1000)
                   at time zone 'Asia/Phnom_Penh')::date
              between o.opened_on and o.opened_on + 7
    )) / nullif(count(*), 0), 1)                         as pct_followed_by_action
from opened o
group by 1, 2;


-- ════════════════════════════════════════════════════════════════════════════
-- 4. 역할별 사용 비율
-- ════════════════════════════════════════════════════════════════════════════
-- 관리자 한 사람이 다 하는 약국인지, 직원이 실제로 쓰는 약국인지. 뒤쪽이라야
-- 제품이 약국의 일과에 들어간 것이다.
--
-- role이 빈 줄(로그인 전 app_started)은 '(before login)'으로 모은다 — NULL로 두면
-- group by에서 한 칸을 차지하면서 이름이 없어 읽는 쪽이 매번 묻게 된다.
create or replace view public.metric_role_mix as
select
    site_id,
    to_char(
        date_trunc('month',
            to_timestamp(occurred_at / 1000) at time zone 'Asia/Phnom_Penh'),
        'YYYY-MM')                                      as month,
    coalesce(role, '(before login)')                    as role,
    count(*)                                            as events,
    round(100.0 * count(*) / sum(count(*)) over (
        partition by site_id,
            date_trunc('month',
                to_timestamp(occurred_at / 1000) at time zone 'Asia/Phnom_Penh')
    ), 1)                                               as pct_of_month
from public.usage_events
group by 1, 2, 3,
         date_trunc('month',
             to_timestamp(occurred_at / 1000) at time zone 'Asia/Phnom_Penh');


-- ════════════════════════════════════════════════════════════════════════════
-- 5. 기능별 오류율
-- ════════════════════════════════════════════════════════════════════════════
-- 오류를 별도 이벤트 이름으로 두지 않고 outcome에 적는 이유가 이 뷰다 —
-- save_failed라는 줄은 무언가 실패했다는 것만 말하지 <b>무엇이</b> 실패했는지
-- 말하지 않아서, 기능별로 나눌 수가 없다.
--
-- skipped는 분모에서 뺀다. 영수증 인쇄를 사람이 거절한 것은 실패가 아니고,
-- 넣어 두면 Ask 모드를 쓰는 약국이 전부 오류율 높은 약국으로 보인다.
-- shown(화면 열기)도 뺀다 — 성공하거나 실패하지 않는다.
create or replace view public.metric_error_rate as
select
    site_id,
    to_char(
        date_trunc('month',
            to_timestamp(occurred_at / 1000) at time zone 'Asia/Phnom_Penh'),
        'YYYY-MM')                                              as month,
    event_name,
    count(*)                                                    as attempts,
    count(*) filter (where outcome = 'failure')                 as failures,
    round(100.0 * count(*) filter (where outcome = 'failure')
          / nullif(count(*), 0), 1)                             as pct_failed
from public.usage_events
where outcome in ('success', 'failure')
group by 1, 2, 3;


-- ════════════════════════════════════════════════════════════════════════════
-- 6. 약국별 마지막 동기화
-- ════════════════════════════════════════════════════════════════════════════
-- sync_batch에서 읽는다. usage_events의 sync_manual이 아니라 — 자동 전송은
-- 기록하지 않으므로(30분마다 한 줄씩 쌓는 것은 지표가 아니라 소음이다), 사람이
-- 누른 것만으로는 "이 약국이 서버에 닿고 있는가"에 답할 수 없다.
--
-- 멈춘 약국을 찾는 것이 이 뷰의 쓸모다. days_quiet가 커지는 곳에 전화한다.
create or replace view public.metric_last_sync as
select
    s.site_id,
    l.label,
    max(b.received_at)                                          as last_sync_at,
    round(extract(epoch from (now() - max(b.received_at))) / 86400, 1) as days_quiet,
    count(*)                                                    as batches,
    count(*) filter (where b.status <> 'ok')                    as failed_batches,
    max(b.app_version) filter (where b.status = 'ok')           as app_version
from public.site s
left join public.sync_batch b on b.site_id = s.site_id
left join lateral (
    select label from public.license_issuance
    where site_id = s.site_id order by serial desc limit 1
) l on true
group by s.site_id, l.label;


-- ════════════════════════════════════════════════════════════════════════════
-- 읽기 전용 역할
-- ════════════════════════════════════════════════════════════════════════════
-- 지표를 읽는 쪽에 주는 계정이다. 표가 아니라 <b>위 여섯 뷰만</b> 볼 수 있다.
--
-- 비밀번호는 여기 적지 않는다. 저장소에 들어가는 순간 비밀이 아니고, 이 파일은
-- 공개 저장소에 들어갈 수도 있다. 아래 줄의 자리표시자를 콘솔에서 직접 바꿔 실행하고,
-- 쓴 비밀번호는 선유님 비밀번호 관리자에 둔다.
--
--   do $$ begin
--       if not exists (select 1 from pg_roles where rolname = 'campos_metrics') then
--           create role campos_metrics login password 'PUT-A-LONG-RANDOM-PASSWORD-HERE';
--       end if;
--   end $$;
--
-- 위 줄을 실행한 뒤 아래를 실행한다. 아래는 비밀이 없으므로 몇 번을 돌려도 된다.

-- 표에는 아무 권한도 주지 않는다. 새 역할은 기본적으로 아무것도 못 읽고
-- (schema.sql의 default privileges는 service_role에만 준다), 뷰가 소유자 권한으로
-- 돌기 때문에 뷰 SELECT만으로 충분하다. 표까지 주면 지표를 보는 사람이 약국
-- 원장을 통째로 읽을 수 있다 — 주지 않는 편이 설명도 짧다.
do $$
begin
    if exists (select 1 from pg_roles where rolname = 'campos_metrics') then
        execute 'grant usage on schema public to campos_metrics';

        execute 'grant select on
            public.metric_active_days,
            public.metric_feature_use,
            public.metric_expiry_response,
            public.metric_role_mix,
            public.metric_error_rate,
            public.metric_last_sync
          to campos_metrics';
    else
        raise notice
            'campos_metrics 역할이 없습니다. 위 주석의 create role을 먼저 실행하세요.';
    end if;
end $$;
