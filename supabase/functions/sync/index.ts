// CamPOS v2 동기화 — 서버로 들어가는 유일한 길
//
// 표에는 RLS가 켜져 있고 정책이 하나도 없다. anon 키로는 아무것도 읽거나 쓸 수
// 없고, service_role 키는 이 함수 안에만 있다. 그래서 쓰기 규칙을 지킬 자리가
// 여기 하나뿐이고, 반대로 말하면 여기서 빠뜨린 규칙은 어디에도 없다.
//
// 지키는 규칙 세 가지:
//
//  1. site_id는 토큰이 정한다. 묶음에 적힌 값을 쓰지 않는다 — 그러면 한 약국의
//     PC가 다른 약국의 행을 덮어쓸 수 있다.
//
//  2. 열 이름은 허용 목록에 있는 것만 받고, 모르는 이름이 오면 거부한다.
//     조용히 버리면 그 열만 비어서 들어가고, 아무도 그것을 보고하지 않는다.
//     그래서 클라이언트가 열을 늘릴 때는 서버가 먼저 바뀌어야 한다 — 순서가
//     거꾸로면 전송이 멈추지만, 멈춘 전송은 보이고 반쯤 들어간 데이터는 안 보인다.
//
//  3. 같은 묶음을 다시 받아도 행이 늘지 않는다. 키가 (site_id, 원본 ID)이고
//     upsert이므로 제자리에서 갱신된다.

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

// ── 허용 목록 ───────────────────────────────────────────────────────────────
// 이 목록이 곧 서버 컬럼 목록이다. 클라이언트가 snake_case로 보내는 이유가
// 이것 — 이름을 바꿔 주는 표가 없으므로 한 줄 틀려서 조용히 비는 일이 없다.
const ALLOWED: Record<string, { table: string; key: string; columns: string[] }> = {
  products: {
    table: "product",
    key: "site_id,product_id",
    columns: [
      "product_id", "barcode", "internal_barcode", "unit_barcode",
      "product_name", "generic_name", "strength", "dosage_form", "unit",
      "units_per_box", "sells_loose", "manufacturer", "country_of_origin",
      "atc_code", "is_combination", "category", "safety_stock_level", "status",
      "cost_price", "selling_price", "unit_selling_price",
      "selling_price_khr", "cost_price_khr", "unit_selling_price_khr",
      "created_at", "updated_at",
    ],
  },
  inventory: {
    table: "inventory",
    key: "site_id,inventory_id",
    columns: [
      "inventory_id", "product_id", "batch_number", "expiry_date",
      "current_quantity", "box_quantity", "unit_quantity", "updated_at",
    ],
  },
  users: {
    table: "app_user",
    key: "site_id,user_id",
    // 네 개뿐이다. password_hash 같은 이름이 오면 허용 목록에 없으므로 거부된다.
    columns: ["user_id", "username", "role", "status"],
  },
  transactions: {
    table: "stock_transaction",
    key: "site_id,transaction_id",
    columns: [
      "transaction_id", "product_id", "user_id", "transaction_type",
      "batch_number", "expiry_date", "quantity",
      "selling_price_at_transaction", "total_amount", "payment_method",
      "reason", "related_transaction_id", "stock_before", "stock_after",
      "transaction_time",
    ],
  },
  counselling_logs: {
    table: "counselling_log",
    key: "site_id,log_id",
    columns: [
      "log_id", "transaction_id", "product_id", "atc_code", "aware_group",
      "printed", "skip_reason", "locale", "source_version", "created_at",
    ],
  },
};

const TOP_LEVEL = new Set([
  "app_version", "client_time", "positions", ...Object.keys(ALLOWED),
]);

// 한 번에 넣는 행 수. 요청 하나가 커지면 끊긴 연결에서 통째로 다시 보내야 한다.
const CHUNK = 500;

// 사용자에게 그대로 보이는 문구이므로 영어로 적는다.
function fail(status: number, message: string) {
  return new Response(JSON.stringify({ ok: false, error: message }), {
    status,
    headers: { "content-type": "application/json" },
  });
}

async function sha256Hex(text: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text));
  return Array.from(new Uint8Array(digest))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return fail(405, "Use POST.");
  }

  const token = request.headers.get("x-campos-token");

  if (!token) {
    return fail(401, "This device is not registered for sync.");
  }

  const db = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
    { auth: { persistSession: false } },
  );

  // ── 어느 약국인가: 토큰이 답한다 ─────────────────────────────────────────
  const tokenHash = await sha256Hex(token);

  const { data: registration, error: tokenError } = await db
    .from("site_token")
    .select("site_id")
    .eq("token_sha256", tokenHash)
    .is("revoked_at", null)
    .maybeSingle();

  if (tokenError) {
    return fail(500, "The server could not check this device: " + tokenError.message);
  }

  if (!registration) {
    // 토큰이 틀렸는지 해지되었는지 구분해서 알려주지 않는다 — 로그인과 같은 이유다.
    return fail(401, "This device is not registered for sync.");
  }

  const siteId = registration.site_id as string;

  // ── 묶음 읽기 ────────────────────────────────────────────────────────────
  let payload: Record<string, unknown>;

  try {
    // BOM을 떼고 파싱한다. 미리보기 파일은 사람이 열어 볼 것이라 BOM과 함께
    // 쓰이고, 그 파일을 그대로 curl로 보내 서버를 확인하는 것이 2단계의 검증
    // 방법이다. JSON.parse는 앞에 붙은 BOM 하나에 실패한다.
    payload = JSON.parse((await request.text()).replace(/^﻿/, ""));
  } catch {
    return fail(400, "The request body is not valid JSON.");
  }

  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return fail(400, "The request body must be a JSON object.");
  }

  for (const name of Object.keys(payload)) {
    if (!TOP_LEVEL.has(name)) {
      return fail(400, `The server does not know the field "${name}". Update the server first.`);
    }
  }

  // site_id를 보내왔다면 거부한다. 무시하면 보낸 쪽은 그것이 쓰였다고 믿는다.
  if ("site_id" in payload) {
    return fail(400, "The payload must not carry site_id; the device token decides it.");
  }

  // ── 기록을 먼저 남긴다 ───────────────────────────────────────────────────
  // 성공한 뒤에 적으면 실패한 전송은 흔적이 없다. 약국이 오류를 말할 때 찾을
  // 것이 아무것도 없는 상태가 제일 곤란하다.
  const { data: batch, error: batchError } = await db
    .from("sync_batch")
    .insert({
      site_id: siteId,
      app_version: payload.app_version ?? null,
      client_time: payload.client_time ?? null,
      status: "started",
    })
    .select("batch_id")
    .single();

  if (batchError) {
    return fail(500, "The server could not start a sync batch: " + batchError.message);
  }

  const batchId = batch.batch_id as string;
  const accepted: Record<string, number> = {};

  try {
    for (const [field, spec] of Object.entries(ALLOWED)) {
      const rows = payload[field];

      if (rows === undefined || rows === null) {
        accepted[spec.table] = 0;
        continue;
      }

      if (!Array.isArray(rows)) {
        throw new Error(`Field "${field}" must be a list.`);
      }

      const allowed = new Set(spec.columns);
      const prepared = rows.map((row, index) => {
        if (row === null || typeof row !== "object") {
          throw new Error(`Row ${index + 1} of "${field}" is not an object.`);
        }

        for (const column of Object.keys(row)) {
          if (!allowed.has(column)) {
            throw new Error(
              `The server does not know the column "${column}" in "${field}". Update the server first.`,
            );
          }
        }

        return { ...row, site_id: siteId };
      });

      for (let at = 0; at < prepared.length; at += CHUNK) {
        const slice = prepared.slice(at, at + CHUNK);

        const { error } = await db
          .from(spec.table)
          .upsert(slice, { onConflict: spec.key });

        if (error) {
          throw new Error(`${spec.table}: ${error.message}`);
        }
      }

      accepted[spec.table] = prepared.length;
    }

    await db.from("sync_batch").update({
      status: "ok",
      row_counts: accepted,
      positions: payload.positions ?? null,
    }).eq("batch_id", batchId);

    await db.from("site_token")
      .update({ last_used_at: new Date().toISOString() })
      .eq("token_sha256", tokenHash);

    // positions를 그대로 돌려준다. 클라이언트는 이 응답을 받은 뒤에만 자기
    // 워터마크를 올려야 한다 — 보내고 바로 올리면, 실패한 묶음의 행들은 다시
    // 선택되지 않고 영구히 빠진다.
    return new Response(
      JSON.stringify({
        ok: true,
        batch_id: batchId,
        site_id: siteId,
        accepted,
        positions: payload.positions ?? {},
      }),
      { status: 200, headers: { "content-type": "application/json" } },
    );
  } catch (thrown) {
    const message = thrown instanceof Error ? thrown.message : String(thrown);

    await db.from("sync_batch").update({
      status: "failed",
      error: message,
      row_counts: accepted,
    }).eq("batch_id", batchId);

    return fail(400, message);
  }
});
