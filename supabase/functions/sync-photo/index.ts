// CamPOS v2 — 제품 사진 한 장을 받는다.
//
// sync와 따로 둔 이유는 싣는 것이 다르기 때문이다. sync는 JSON을 읽고, 이쪽은
// 이미지 바이트를 그대로 받는다. 한 함수에서 content-type으로 갈라도 되지만,
// 그러면 두 가지 일이 한 파일에서 얽히고 둘 중 하나를 고칠 때마다 다른 쪽을
// 깨뜨릴 수 있다.
//
// 사진을 JSON 묶음에 싣지 않는 이유도 같다. 장당 수백 KB라 base64로 넣으면
// 묶음 하나가 수십 MB가 되고, 사진 한 장이 실패하면 그 안의 원장 행까지 전부
// 다시 보내야 한다.
//
// 규칙은 sync와 같다:
//  1. site_id는 토큰이 정한다. 보내온 값을 쓰지 않는다.
//  2. 같은 사진을 다시 받아도 쌓이지 않는다 — 키가 (site_id, product_id)이고
//     경로도 그 둘로 정해지므로 덮어쓴다.
//  3. 해시가 이미 저장된 것과 같으면 받지 않고 건너뛴다. 약국의 회선으로
//     같은 사진을 두 번 올릴 이유가 없다.

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const BUCKET = "product-photos";

// 800px JPEG 한 장은 보통 100~300KB다. 이보다 큰 것은 ProductPhotoService를
// 거치지 않은 것이므로 받지 않는다.
const MAX_BYTES = 5 * 1024 * 1024;

const ALLOWED_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);

function fail(status: number, message: string) {
  return new Response(JSON.stringify({ ok: false, error: message }), {
    status,
    headers: { "content-type": "application/json" },
  });
}

async function sha256Hex(bytes: Uint8Array): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(digest))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

/// sync/index.ts와 같은 이유로 두 이름을 모두 본다.
function readSecretKey(): string {
  const dictionary = Deno.env.get("SUPABASE_SECRET_KEYS");

  if (dictionary) {
    try {
      const parsed = JSON.parse(dictionary);
      const key = parsed["default"] ?? Object.values(parsed)[0];

      if (typeof key === "string" && key.length > 0) {
        return key;
      }
    } catch {
      // 평문 쪽으로 넘어간다.
    }
  }

  const legacy = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");

  if (legacy) {
    return legacy;
  }

  throw new Error(
    "The function cannot find its database key "
      + "(neither SUPABASE_SECRET_KEYS nor SUPABASE_SERVICE_ROLE_KEY is set).",
  );
}

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return fail(405, "Use POST.");
  }

  const token = request.headers.get("x-campos-token");
  const productId = request.headers.get("x-campos-product-id");
  const claimedHash = (request.headers.get("x-campos-sha256") ?? "").toLowerCase();
  const contentType = (request.headers.get("content-type") ?? "").split(";")[0].trim();

  if (!token) {
    return fail(401, "This device is not registered for sync.");
  }

  if (!productId) {
    return fail(400, "The photo did not say which product it belongs to.");
  }

  if (!ALLOWED_TYPES.has(contentType)) {
    return fail(400, `This server does not accept ${contentType || "an unnamed type"} for photos.`);
  }

  let db;

  try {
    db = createClient(Deno.env.get("SUPABASE_URL")!, readSecretKey(), {
      auth: { persistSession: false },
    });
  } catch (thrown) {
    return fail(500, thrown instanceof Error ? thrown.message : String(thrown));
  }

  const tokenHash = await sha256Hex(new TextEncoder().encode(token));

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
    return fail(401, "This device is not registered for sync.");
  }

  const siteId = registration.site_id as string;

  const bytes = new Uint8Array(await request.arrayBuffer());

  if (bytes.length === 0) {
    return fail(400, "The photo was empty.");
  }

  if (bytes.length > MAX_BYTES) {
    return fail(413, "That photo is too large. Photos are resized before they are stored.");
  }

  // 보내온 해시를 믿지 않고 다시 센다. 틀린 해시가 저장되면 그 사진은 바뀌어도
  // 다시 올라오지 않거나, 안 바뀌었는데 매번 올라온다 — 둘 다 조용하다.
  const actualHash = await sha256Hex(bytes);

  if (claimedHash && claimedHash !== actualHash) {
    return fail(400, "The photo does not match the checksum that was sent with it.");
  }

  // 이미 같은 사진이 있으면 받지 않는다. 약국 회선으로 같은 것을 두 번 올릴
  // 이유가 없고, 클라이언트도 이 답을 "올린 것"으로 기록한다.
  const { data: existing } = await db
    .from("product_photo")
    .select("sha256")
    .eq("site_id", siteId)
    .eq("product_id", productId)
    .maybeSingle();

  if (existing?.sha256 === actualHash) {
    return new Response(
      JSON.stringify({ ok: true, skipped: true, sha256: actualHash, site_id: siteId }),
      { status: 200, headers: { "content-type": "application/json" } },
    );
  }

  // 경로는 (site_id, product_id)로 정해진다. 해시를 파일 이름에 넣으면 사진을
  // 고칠 때마다 옛 파일이 남아 버킷이 끝없이 커지고, 지우는 일을 누군가 기억해야
  // 한다. 같은 자리에 덮어쓰면 그 일이 없어진다.
  const extension = contentType === "image/png" ? "png" : contentType === "image/webp" ? "webp" : "jpg";
  const path = `${siteId}/${productId}.${extension}`;

  const { error: uploadError } = await db.storage
    .from(BUCKET)
    .upload(path, bytes, { contentType, upsert: true });

  if (uploadError) {
    return fail(500, "The photo could not be stored: " + uploadError.message);
  }

  const { error: rowError } = await db.from("product_photo").upsert({
    site_id: siteId,
    product_id: productId,
    storage_path: path,
    content_type: contentType,
    byte_size: bytes.length,
    sha256: actualHash,
    photo_updated_at: Number(request.headers.get("x-campos-photo-updated-at")) || null,
  }, { onConflict: "site_id,product_id" });

  if (rowError) {
    // 파일은 올라갔는데 표가 비면, 다음 전송에서 같은 사진을 다시 올린다.
    // 덮어쓰기라 쌓이지 않으므로 그 재시도는 해롭지 않다.
    return fail(500, "The photo was stored but not recorded: " + rowError.message);
  }

  return new Response(
    JSON.stringify({ ok: true, skipped: false, sha256: actualHash, site_id: siteId, path }),
    { status: 200, headers: { "content-type": "application/json" } },
  );
});
