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

-- ── 발급 도구의 비밀 ───────────────────────────────────────────────────────
-- 발급 도구가 issue 함수를 부를 때 내미는 값. 토큰과 같은 방식으로 SHA-256만
-- 저장하므로, 이 표가 새어도 그것으로 등록할 수는 없다.
--
-- 발급 도구에 Supabase 비밀 키를 주지 않는 이유가 이 표다. 그 키는 RLS를 통째로
-- 우회해 약국 데이터 전부를 읽을 수 있는데, 발급 도구에 필요한 것은 대장에 한 줄
-- 쓰는 권한뿐이다. 노트북이 털려도 라이선스 등록 말고는 할 수 없어야 한다.
create table if not exists public.issuer_secret (
    secret_sha256  text primary key,
    label          text,
    created_at     timestamptz not null default now(),
    revoked_at     timestamptz
);

-- ── 라이선스 발급 대장 ─────────────────────────────────────────────────────
-- 어느 일련번호가 어느 약국에 나갔는지. register 함수가 이것을 읽어 라이선스
-- 코드를 토큰으로 바꿔 준다.
--
-- 라이선스 코드 안에는 고객 이름이 없다(LicensePayload의 주석 참조). 서명이
-- 맞다고 해서 어느 약국인지 알 수 있는 것은 아니고, 그 대응을 들고 있는 것이
-- 이 표다.
--
-- <b>발급 대장 전체</b>를 담는다. serial → site_id만 있으면 register는 돌지만,
-- 그러면 발급 내역이 Firestore와 여기 둘로 나뉜 채 남는다. 발급 도구가 여기에
-- 쓰도록 바뀌는 날 Firestore를 접을 수 있게, 그때 필요한 칸을 미리 둔다.
--
-- site_id는 갱신해도 바뀌지 않는다. 일련번호는 발급 건마다 새로 나오므로,
-- 같은 약국의 2년치 라이선스는 서로 다른 serial로 같은 site_id를 가리킨다 —
-- 그것이 이 표가 있어야 하는 이유이기도 하다.
create table if not exists public.license_issuance (
    serial       bigint primary key,
    site_id      text not null references public.site(site_id),

    -- 대장이므로 약국 이름이 여기엔 들어간다. 항생제 CSV의 가명성은 "그 파일에
    -- 이름이 없다"는 것이지 대장이 없다는 뜻이 아니다. 이 표는 Edge Function만
    -- 읽는다(아래 RLS).
    label        text,

    issued_at    timestamptz not null default now(),
    expires_at   timestamptz,
    revoked_at   timestamptz,
    note         text
);

create index if not exists license_issuance_site_idx on public.license_issuance(site_id);

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

    -- 거래가 일어난 그 순간의 원가와 환율. 둘 다 그때만 알 수 있는 값이라
    -- 여기 적지 않으면 나중에 어떤 계산으로도 되살릴 수 없다.
    --
    -- 원가: 상품 마스터의 cost_price는 <b>현재</b> 값이고 매입가가 오르면
    -- 덮어써진다. 그것으로 작년 매출의 마진을 내면 틀린 숫자가 나오는데,
    -- 그럴듯해 보여서 틀린 줄을 모른다.
    -- NULL은 "모름"이다 — 종이 장부만 쓰던 약국의 상품은 원가가 0으로
    -- 저장되어 있고, 그것을 그대로 적으면 마진이 100%로 계산된다.
    --
    -- 환율: 판매 금액은 달러로만 남는다. 환율을 적지 않으면 지난달 매출을
    -- 오늘 환율로 환산하게 되고, 환율이 오르면 판매가 한 건도 안 늘었는데
    -- 과거가 커 보인다. 리엘이 실제로 오간 돈이므로 이 열이 그 기록이다.
    cost_price_at_transaction     numeric,
    exchange_rate_at_transaction  numeric,

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

-- ── 기능 사용 기록 ────────────────────────────────────────────────────────
-- 약국이 어떤 기능을 얼마나 쓰는지. 캄디언즈가 성과 지표로 읽는다.
--
-- <b>열은 여섯 개뿐이고, 늘리려면 동의서를 먼저 고쳐야 한다.</b> 약국은 계약할 때
-- "무엇이 기록되는가"를 읽고 서명하는데, 열이 하나 늘면 그 문장이 조용히 거짓이
-- 된다 — 기록은 쌓이고 나면 되돌릴 수 없으므로, 늘리는 쪽이 항상 설명보다 빠르다.
-- PC 쪽 UsageEventTests가 열 개수를 고정하고, docs/usage-events-consent.md가
-- 약국에게 하는 약속이다.
--
-- <b>누가 눌렀는지는 적지 않는다.</b> user_id를 적으면 이미 올라가 있는 app_user와
-- 이어져 직원 실명 단위 기록이 되고, 그것은 성과 지표에 필요하지 않다. role만
-- 남기므로 "관리자와 직원이 각각 얼마나 쓰는가"는 나오고 "누가 무엇을 했는가"는
-- 나오지 않는다. 그 선은 되돌릴 수 없는 쪽에 그어 두는 편이 낫다.
--
-- role은 그 시점의 역할이다. app_user의 역할은 직원이 승진하면 덮어써지므로,
-- 과거 기록을 지금의 역할로 읽으면 틀린다 — 원가 스냅샷과 같은 이유다.
create table if not exists public.usage_events (
    site_id             text not null references public.site(site_id),
    event_id            text not null,

    occurred_at         bigint not null,
    role                text,
    event_name          text not null,
    outcome             text not null,
    app_version         text,

    server_received_at  timestamptz not null default now(),

    primary key (site_id, event_id)
);

create index if not exists usage_events_time_idx
    on public.usage_events(site_id, occurred_at);

create index if not exists usage_events_name_idx
    on public.usage_events(site_id, event_name);


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

-- ── 이미 만들어진 프로젝트에 열 붙이기 ────────────────────────────────────────
-- create table if not exists는 이미 있는 표에 아무것도 하지 않으므로, 열이 늘면
-- 그것만 따로 붙여야 한다. 앱의 AddColumnIfMissing과 같은 방식이고, 같은 이유로
-- 이 파일을 통째로 다시 Run 하는 것이 유일한 적용 방법이 된다 —
-- "이번엔 어느 줄만 실행하면 되나"를 사람이 판단하게 두면 언젠가 틀린다.
--
-- 서버가 앱보다 <b>먼저</b>다. 서버는 모르는 이름이 오면 묶음 전체를 거부하므로,
-- 순서를 뒤집으면 그 약국의 동기화가 멈춘다.
alter table public.stock_transaction
    add column if not exists cost_price_at_transaction    numeric;
alter table public.stock_transaction
    add column if not exists exchange_rate_at_transaction numeric;


alter table public.issuer_secret     enable row level security;
alter table public.license_issuance  enable row level security;
alter table public.site              enable row level security;
alter table public.site_token        enable row level security;
alter table public.sync_batch        enable row level security;
alter table public.product           enable row level security;
alter table public.inventory         enable row level security;
alter table public.app_user          enable row level security;
alter table public.stock_transaction enable row level security;
alter table public.counselling_log   enable row level security;
alter table public.usage_events      enable row level security;
alter table public.product_photo     enable row level security;
