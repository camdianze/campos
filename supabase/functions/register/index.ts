// CamPOS — 라이선스 코드를 토큰으로 바꿔 준다.
//
// 설치할 때 사람이 넣는 것을 <b>라이선스 코드 하나</b>로 줄이기 위한 함수다.
// 전에는 라이선스와 동기화 토큰 두 개를 따로 발급해 따로 입력했는데, 토큰은
// 약국이 볼 이유가 없는 값이고 손으로 나르는 동안 잘못 붙여넣을 자리만 됐다.
//
// 하는 일:
//   1. 라이선스 코드의 서명을 공개키로 확인한다 (ECDSA P-256)
//   2. 일련번호로 발급 대장에서 어느 약국인지 찾는다
//   3. 그 약국의 토큰을 새로 만들어 돌려준다
//
// 공개키만 쓰므로 이 함수는 코드를 <b>만들 수 없다.</b> 서명하는 개인키는
// 발급 도구에만 있고 이 저장소에도 서버에도 없다 — 함수가 통째로 털려도
// 새 라이선스를 찍어낼 수는 없다.
//
// 일련번호로 약국을 찾는 이유: 라이선스 코드 안에는 고객 이름이 없다. 번호와
// 약국의 대응은 발급 대장이 들고 있고, 그 대장이 license_issuance다.

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

/// 앱이 들고 있는 것과 같은 공개키. 비밀이 아니다 — 확인만 할 수 있고 서명은 못 한다.
const PUBLIC_KEY_SPKI =
  "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEfaQ8ngaQCwVbt7v7R+oNxz14RCeMYyd1UH0AJaZULQl7LGl1XUiK+jslChMDEo+wdvc/dsuo4u+uZvQXqwVVwg==";

const CODE_PREFIX = "CAMPOS";

/// 사람이 옮겨 적을 수 있는 글자만. I·L·O·U가 없다 — 1, 0과 헷갈리기 때문이다.
const BASE32_ALPHABET = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

const PAYLOAD_BYTES = 13;
const SIGNATURE_BYTES = 64;

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

/// Base32.cs와 같은 규칙. 두 저장소의 그 파일과 뜻이 어긋나면 코드가 안 풀린다.
function base32Decode(text: string): Uint8Array {
  const out: number[] = [];
  let buffer = 0;
  let bits = 0;

  for (const ch of text.toUpperCase()) {
    const value = BASE32_ALPHABET.indexOf(ch);
    if (value < 0) throw new Error("bad character");

    buffer = (buffer << 5) | value;
    bits += 5;

    if (bits >= 8) {
      bits -= 8;
      out.push((buffer >> bits) & 0xff);
    }
  }

  return new Uint8Array(out);
}

async function sha256Hex(bytes: Uint8Array): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(digest))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

/// 암호학적 난수 64자. 토큰은 사람이 보지 않으므로 읽기 쉬울 필요가 없다.
function newToken(): string {
  const raw = new Uint8Array(32);
  crypto.getRandomValues(raw);
  return Array.from(raw).map((b) => b.toString(16).padStart(2, "0")).join("");
}

/// 라이선스 코드를 풀어 페이로드와 서명으로 나눈다. LicenseCodeCodec.cs와 짝이다.
function decodeCode(code: string): { payload: Uint8Array; signature: Uint8Array } {
  let body = code.replace(/[\s-]/g, "").toUpperCase();

  if (body.startsWith(CODE_PREFIX)) {
    body = body.slice(CODE_PREFIX.length);
  }

  const combined = base32Decode(body);

  if (combined.length < PAYLOAD_BYTES + SIGNATURE_BYTES) {
    throw new Error("too short");
  }

  return {
    payload: combined.slice(0, PAYLOAD_BYTES),
    signature: combined.slice(PAYLOAD_BYTES, PAYLOAD_BYTES + SIGNATURE_BYTES),
  };
}

/// LicensePayload.cs의 13바이트 배치. 버전 1 · 일련번호 · 발급 · 만료.
function readPayload(bytes: Uint8Array) {
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);

  return {
    version: bytes[0],
    serial: view.getUint32(1, false),
    issuedAt: view.getUint32(5, false),
    expiresAt: view.getUint32(9, false),
  };
}

async function verifySignature(payload: Uint8Array, signature: Uint8Array): Promise<boolean> {
  const spki = Uint8Array.from(atob(PUBLIC_KEY_SPKI), (c) => c.charCodeAt(0));

  const key = await crypto.subtle.importKey(
    "spki",
    spki,
    { name: "ECDSA", namedCurve: "P-256" },
    false,
    ["verify"],
  );

  return await crypto.subtle.verify(
    { name: "ECDSA", hash: "SHA-256" },
    key,
    signature,
    payload,
  );
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return fail(405, "Use POST.");
  }

  let code: string;

  try {
    const body = await request.json();
    code = String(body?.license_code ?? "").trim();
  } catch {
    return fail(400, "The request body is not valid JSON.");
  }

  if (code.length === 0) {
    return fail(400, "No licence code was sent.");
  }

  // ── 코드가 진짜인가 ──────────────────────────────────────────────────
  let payload;

  try {
    const parts = decodeCode(code);

    if (!await verifySignature(parts.payload, parts.signature)) {
      // 틀린 코드와 손댄 코드를 구분해서 알려주지 않는다.
      return fail(401, "That licence code was not recognised.");
    }

    payload = readPayload(parts.payload);
  } catch {
    return fail(401, "That licence code was not recognised.");
  }

  if (payload.version !== 1) {
    return fail(400, "That licence code is for a newer version of CamPOS.");
  }

  // 만료된 코드로는 토큰을 주지 않는다. 0은 무기한이다.
  const now = Math.floor(Date.now() / 1000);

  if (payload.expiresAt !== 0 && payload.expiresAt < now) {
    return fail(403, "That licence has expired. Renew it first.");
  }

  let db;

  try {
    db = createClient(Deno.env.get("SUPABASE_URL")!, readSecretKey(), {
      auth: { persistSession: false },
    });
  } catch (thrown) {
    return fail(500, thrown instanceof Error ? thrown.message : String(thrown));
  }

  // ── 어느 약국인가 ────────────────────────────────────────────────────
  // 서명이 맞다고 해서 어느 약국인지 알 수 있는 것은 아니다. 라이선스 코드에는
  // 고객 이름이 없고, 번호와 약국의 대응은 발급 대장이 들고 있다.
  const { data: issuance, error: lookupError } = await db
    .from("license_issuance")
    .select("site_id, revoked_at")
    .eq("serial", payload.serial)
    .maybeSingle();

  if (lookupError) {
    return fail(500, "The server could not look up this licence: " + lookupError.message);
  }

  if (!issuance) {
    // 서명은 맞는데 대장에 없다. 발급 기록을 아직 올리지 않았다는 뜻이다.
    return fail(404, `Licence ${payload.serial} is not registered on the server yet.`);
  }

  if (issuance.revoked_at) {
    return fail(403, "That licence has been withdrawn.");
  }

  const siteId = issuance.site_id as string;

  // ── 토큰 발급 ────────────────────────────────────────────────────────
  // 옛 토큰은 그대로 둔다. 같은 약국이 PC를 한 대 더 놓는 경우가 있고, 여기서
  // 자동으로 거두면 그 PC가 다음 전송에서 조용히 거부된다. 거두는 것은 사람이
  // revoked_at을 채워서 한다.
  const token = newToken();

  const { error: insertError } = await db.from("site_token").insert({
    token_sha256: await sha256Hex(new TextEncoder().encode(token)),
    site_id: siteId,
    label: `licence ${payload.serial}`,
  });

  if (insertError) {
    return fail(500, "The token could not be issued: " + insertError.message);
  }

  return new Response(
    JSON.stringify({ ok: true, token, site_id: siteId }),
    { status: 200, headers: { "content-type": "application/json" } },
  );
});
