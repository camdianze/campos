// CamPOS — 발급 도구가 라이선스 발급 내역을 적는 곳.
//
// 발급 도구가 Supabase 비밀 키를 들고 다니지 않게 하려고 둔 함수다. 그 키는
// RLS를 통째로 우회해 약국 데이터 전부를 읽을 수 있는데, 발급 도구에 필요한
// 것은 "대장에 한 줄 쓰기"뿐이다. 노트북이 털려도 라이선스 등록 말고는 할 수
// 없어야 한다.
//
// 쓰는 표는 둘뿐이다: site(없으면 만들고)와 license_issuance. 동기화 표에는
// 손대지 않는다.
//
// 비밀은 헤더로 온다. 토큰과 같은 방식으로 <b>SHA-256만</b> 서버에 저장하므로,
// 설정 표가 새어도 그것으로 등록할 수는 없다.

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

function fail(status: number, message: string) {
  return new Response(JSON.stringify({ ok: false, error: message }), {
    status,
    headers: { "content-type": "application/json" },
  });
}

function readSecretKey(): string {
  const dictionary = Deno.env.get("SUPABASE_SECRET_KEYS");

  if (dictionary) {
    try {
      const parsed = JSON.parse(dictionary);
      const key = parsed["default"] ?? Object.values(parsed)[0];
      if (typeof key === "string" && key.length > 0) return key;
    } catch { /* 아래 평문으로 */ }
  }

  const legacy = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (legacy) return legacy;

  throw new Error("The function cannot find its database key.");
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

  const secret = request.headers.get("x-campos-issuer-secret");

  if (!secret) {
    return fail(401, "This tool is not authorised to register licences.");
  }

  let body: Record<string, unknown>;

  try {
    body = await request.json();
  } catch {
    return fail(400, "The request body is not valid JSON.");
  }

  const serial = Number(body.serial);
  const siteId = String(body.site_id ?? "").trim();
  const label = String(body.label ?? "").trim();

  if (!Number.isInteger(serial) || serial <= 0) {
    return fail(400, "serial must be a positive whole number.");
  }

  if (siteId.length === 0) {
    return fail(400, "site_id is required.");
  }

  let db;

  try {
    db = createClient(Deno.env.get("SUPABASE_URL")!, readSecretKey(), {
      auth: { persistSession: false },
    });
  } catch (thrown) {
    return fail(500, thrown instanceof Error ? thrown.message : String(thrown));
  }

  // ── 부르는 쪽이 발급 도구가 맞는가 ───────────────────────────────────
  const { data: allowed, error: secretError } = await db
    .from("issuer_secret")
    .select("label")
    .eq("secret_sha256", await sha256Hex(secret))
    .is("revoked_at", null)
    .maybeSingle();

  if (secretError) {
    return fail(500, "The server could not check this tool: " + secretError.message);
  }

  if (!allowed) {
    return fail(401, "This tool is not authorised to register licences.");
  }

  // ── 약국이 아직 없으면 만든다 ────────────────────────────────────────
  // 발급 도구가 새 약국의 첫 라이선스를 끊는 순간이 그 약국이 생기는 순간이다.
  // 사람이 site를 먼저 만들게 하면 그 단계를 잊고, 그러면 발급은 됐는데 등록이
  // 안 된 상태가 된다 — 약국에서 활성화할 때까지 아무도 모른다.
  const { error: siteError } = await db
    .from("site")
    .upsert({ site_id: siteId, label: label || siteId }, { onConflict: "site_id" });

  if (siteError) {
    return fail(500, "The pharmacy could not be registered: " + siteError.message);
  }

  // ── 발급 내역 ────────────────────────────────────────────────────────
  // 같은 번호를 다시 보내도 덮어쓴다. 발급 도구의 큐가 인터넷이 돌아온 뒤
  // 다시 보내는 구조라, 두 번 도착하는 것이 정상이다.
  const { error: issuanceError } = await db.from("license_issuance").upsert({
    serial,
    site_id: siteId,
    label: label || null,
    issued_at: body.issued_at ?? new Date().toISOString(),
    expires_at: body.expires_at ?? null,
    note: body.note ?? null,
  }, { onConflict: "serial" });

  if (issuanceError) {
    return fail(500, "The licence could not be recorded: " + issuanceError.message);
  }

  return new Response(
    JSON.stringify({ ok: true, serial, site_id: siteId }),
    { status: 200, headers: { "content-type": "application/json" } },
  );
});
