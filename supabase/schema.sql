-- CamPOS v2 동기화 — 서버 스키마
--
-- 이 파일을 Supabase SQL Editor에 그대로 붙여 실행한다. 여러 번 실행해도 같은
-- 결과가 되도록 전부 IF NOT EXISTS로 적었다 — 한 번에 성공한다는 보장이 없는
-- 작업이고, 실패한 지점부터 다시 돌릴 수 있어야 한다.
--
-- 바뀌지 않는 세 가지 결정:
--
--  1. 키는 (site_id, 원본 ID)다. 약국의 SQLite가 쓰는 ID를 그대로 두 번째 열로
--     쓴다. 같은 행을 다시 보내면 제자리에서 갱신되므로, 끊긴 전송을 다시 보내도
--     행이 늘지 않는다 — 중복 처리의 절반이 키 선택으로 끝난다.
--
--  2. 금액은 numeric이고 자릿수를 제한하지 않는다. numeric(12,2)로 적으면 서버가
--     값을 반올림한다. 서버는 받은 숫자를 바꾸는 쪽이 되어서는 안 된다 —
--     $0.9756 같은 네 자리 가격이 이 앱에는 정상이고, 그 값에서 리엘이 정확히
--     되돌아 나온다.
--
--  3. 시각은 bigint(epoch ms) 그대로 둔다. timestamptz로 바꿔 주는 생성 열을
--     두면 편하지만, expiry_date의 0은 "유효기간 미상"이라서 1970-01-01로
--     번역된다. 가공은 Python 쪽에서 0을 알고 하는 것이 맞다.

-- ── 약국과 토큰 ────────────────────────────────────────────────────────────
-- site_id는 약국을 가리키는 가명 코드다. 어느 약국인지는 이 표 밖(발급 대장)에
-- 있고, 라이선스 시리얼과 같은 방식이다.
create table if not exists public.site (
    site_id     text primary key,
    label       text,
    created_at  timestamptz not null default now()
);

-- PC가 보내는 토큰. 토큰 자체가 아니라 SHA-256만 저장한다 — 이 표가 새어도
-- 그것으로 전송할 수는 없다.
--
-- site_id를 토큰에서 끌어내는 것이 핵심이다. 묶음 안에 site_id를 적게 하면
-- 한 약국의 PC가 다른 약국의 행을 덮어쓸 수 있고, 쓰기 경로가 함수 하나뿐이니
-- 그 규칙이 들어갈 자리도 여기밖에 없다.
create table if not exists public.site_token (
    token_sha256  text primary key,
    site_id       text not null references public.site(site_id),
    label         text,
    created_at    timestamptz not null default now(),
    revoked_at    timestamptz,
    last_used_at  timestamptz
);

create index if not exists site_token_site_idx on public.site_token(site_id);

-- ── 전송 기록 ──────────────────────────────────────────────────────────────
-- 받은 묶음 하나가 한 행. 약국이 오류를 말할 때 테이블을 뒤지지 않고 여기만
-- 보면 언제 무엇이 몇 건 들어왔는지, 어디서 멈췄는지 알 수 있다.
create table if not exists public.sync_batch (
    batch_id      uuid primary key default gen_random_uuid(),
    site_id       text not null references public.site(site_id),
    app_version   text,
    client_time   bigint,
    received_at   timestamptz not null default now(),
    status        text not null default 'started',
    error         text,
    row_counts    jsonb,
    positions     jsonb
);

create index if not exists sync_batch_site_time_idx
    on public.sync_batch(site_id, received_at desc);

-- ── 제품 ───────────────────────────────────────────────────────────────────
create table if not exists public.product (
    site_id                 text not null references public.site(site_id),
    product_id              text not null,

    barcode                 text,
    internal_barcode        text,
    unit_barcode            text,
    product_name            text not null,
    generic_name            text,
    strength                text,
    dosage_form             text,
    unit                    text not null,
    units_per_box           integer not null,
    sells_loose             boolean not null,
    manufacturer            text,
    country_of_origin       text,
    atc_code                text,
    is_combination          boolean not null,
    category                text,
    safety_stock_level      integer not null,
    status                  text not null,

    cost_price              numeric not null,
    selling_price           numeric not null,
    unit_selling_price      numeric,

    -- 약국이 리엘로 정한 가격을 적은 그대로. 달러 값을 환율로 되돌리면 어긋나므로
    -- 원래 숫자가 따로 필요하다. 달러로 정한 가격은 NULL이다.
    selling_price_khr       numeric,
    cost_price_khr          numeric,
    unit_selling_price_khr  numeric,

    created_at              bigint not null,
    updated_at              bigint,

    server_received_at      timestamptz not null default now(),

    primary key (site_id, product_id)
);

-- ── 재고(배치별) ───────────────────────────────────────────────────────────
create table if not exists public.inventory (
    site_id             text not null references public.site(site_id),
    inventory_id        text not null,

    product_id          text not null,
    batch_number        text not null,
    expiry_date         bigint not null,
    current_quantity    integer not null,
    box_quantity        integer not null,
    unit_quantity       integer not null,
    updated_at          bigint not null,

    server_received_at  timestamptz not null default now(),

    primary key (site_id, inventory_id)
);

create index if not exists inventory_product_idx on public.inventory(site_id, product_id);

-- ── 직원 ───────────────────────────────────────────────────────────────────
-- 네 개뿐이다. 비밀번호 해시·보안질문·복구 이메일을 담을 열이 아예 없으므로,
-- 클라이언트가 실수로 보내더라도 들어갈 자리가 없다.
create table if not exists public.app_user (
    site_id             text not null references public.site(site_id),
    user_id             text not null,

    username            text not null,
    role                text not null,
    status              text not null,

    server_received_at  timestamptz not null default now(),

    primary key (site_id, user_id)
);

-- ── 재고 원장 ──────────────────────────────────────────────────────────────
create table if not exists public.stock_transaction (
    site_id                       text not null references public.site(site_id),
    transaction_id                text not null,

    product_id                    text not null,
    user_id                       text not null,
    transaction_type              text not null,
    batch_number                  text not null,
    expiry_date                   bigint not null,
    quantity                      integer not null,
    selling_price_at_transaction  numeric,
    total_amount                  numeric,
    payment_method                text,
    reason                        text,
    related_transaction_id        text,

    -- NULL은 두 가지다: 그 배치가 아직 없었거나(입고로 처음 생긴 행), 이 열이
    -- 생기기 전에 쓰인 행. 0으로 채우면 "재고가 0이었다"는 다른 뜻이 된다.
    stock_before                  integer,
    stock_after                   integer,

    transaction_time              bigint not null,

    server_received_at            timestamptz not null default now(),

    primary key (site_id, transaction_id)
);

create index if not exists stock_transaction_time_idx
    on public.stock_transaction(site_id, transaction_time);

create index if not exists stock_transaction_batch_idx
    on public.stock_transaction(site_id, product_id, batch_number, transaction_time);

-- ── 항생제 상담 기록 ───────────────────────────────────────────────────────
create table if not exists public.counselling_log (
    site_id             text not null references public.site(site_id),
    log_id              text not null,

    transaction_id      text not null,
    product_id          text not null,
    atc_code            text,
    aware_group         text not null,
    printed             boolean not null,
    skip_reason         text,
    locale              text not null,
    source_version      text,
    created_at          bigint not null,

    server_received_at  timestamptz not null default now(),

    primary key (site_id, log_id)
);

create index if not exists counselling_log_time_idx
    on public.counselling_log(site_id, created_at);

-- ── 제품 사진 (5단계) ──────────────────────────────────────────────────────
-- 사진은 지금 올리지 않지만 자리를 미리 만든다. 사진은 Storage에 두고 여기에는
-- 그 경로만 적는다 — bytea로 넣으면 제품 한 행을 읽을 때마다 이미지가 따라온다.
--
-- product_id로 키를 잡는 이유: 파일 이름을 바코드로 맞춘 것은 PC 쪽에서 제품을
-- 찾기 위한 수단이었고, 서버에 도착할 때는 이미 어느 제품인지 정해져 있다.
-- V4의 "사진을 찍으면 제품 페이지가 뜬다"도 이 열을 거꾸로 읽는 것이다.
create table if not exists public.product_photo (
    site_id             text not null references public.site(site_id),
    product_id          text not null,

    storage_path        text not null,
    content_type        text,
    byte_size           integer,
    sha256              text,
    photo_updated_at    bigint,

    server_received_at  timestamptz not null default now(),

    primary key (site_id, product_id)
);

-- ── 데이터 표끼리 외래키를 걸지 않는 이유 ──────────────────────────────────
-- inventory.product_id → product 로 걸면 제품이 아직 안 올라간 재고 행이 거부
-- 된다. 한 묶음 안에서는 제품이 먼저 가므로 문제가 없지만, 3단계에서 묶음을
-- 쪼개 보내면 순서 차이만으로 약국의 전송이 실패한다. PC 쪽 SQLite가 이미
-- foreign_keys = ON으로 참조를 지키고 있으니, 여기서 한 번 더 막아 얻는 것보다
-- 잃는 것이 크다. site 참조만 남긴다 — 그쪽은 토큰이 정하므로 깨질 수 없다.

-- ── RLS: 켜고, 정책은 하나도 두지 않는다 ───────────────────────────────────
-- 정책이 없는 RLS는 anon·authenticated 키로 아무것도 읽거나 쓸 수 없다는 뜻이다.
-- Edge Function만 service_role로 이 표들에 닿고, 그 키는 서버를 떠나지 않는다.
-- 정책을 하나라도 추가하면 그 순간 함수 밖에 길이 생긴다 — 이 설계에서 가장
-- 조용히 깨지는 부분이 그것이다.
-- ── 권한을 프로젝트 설정이 아니라 여기서 정한다 ──────────────────────────
-- Supabase의 "Automatically expose new tables"는 새 표의 권한을 anon·authenticated
-- <b>그리고 service_role</b>에 한꺼번에 준다. 꺼 두는 것이 맞지만(Supabase 자신이
-- 그렇게 권하고, 2026-05-30부터 새 프로젝트의 기본값이다), 끄면 service_role도
-- 권한을 못 받아 Edge Function이 "permission denied"로 죽는다.
--
-- 그래서 설정에 맡기지 않고 필요한 것만 여기서 준다. 이 파일을 실행한 프로젝트는
-- 그 체크박스가 켜져 있든 꺼져 있든 같게 동작한다 — 설정 화면의 상태를 기억해야
-- 하는 코드는 몇 달 뒤에 아무도 기억하지 못한다.
grant usage on schema public to service_role;

grant select, insert, update, delete
    on all tables in schema public to service_role;

-- 앞으로 추가될 표까지. 빠뜨리면 그 표만 조용히 안 들어간다.
alter default privileges in schema public
    grant select, insert, update, delete on tables to service_role;

-- anon·authenticated는 이 표들에 닿을 일이 없다. RLS가 이미 막지만, 권한까지
-- 거두면 정책을 실수로 하나 추가해도 길이 열리지 않는다 — 자물쇠 두 개가
-- 서로 다른 실수를 막는다.
revoke all on all tables in schema public from anon, authenticated;

alter default privileges in schema public
    revoke all on tables from anon, authenticated;

alter table public.site              enable row level security;
alter table public.site_token        enable row level security;
alter table public.sync_batch        enable row level security;
alter table public.product           enable row level security;
alter table public.inventory         enable row level security;
alter table public.app_user          enable row level security;
alter table public.stock_transaction enable row level security;
alter table public.counselling_log   enable row level security;
alter table public.product_photo     enable row level security;
