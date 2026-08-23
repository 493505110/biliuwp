interface Env {
  DB: D1Database;
  CACHE_ADMIN_TOKEN?: string;
  CACHE_TTL_SECONDS?: string;
  CACHE_LEASE_SECONDS?: string;
}

interface CacheMetadata {
  aid: string;
  cid: string;
  provider: string;
  api_url: string;
  model: string;
  prompt_version: string;
  title?: string;
  duration?: number;
}

interface AdSegment {
  start_time: number;
  end_time: number;
  product_name?: string;
  ad_content?: string;
}

interface AiResult {
  ads: AdSegment[];
  msg?: string;
}

interface CacheEntry {
  cache_key: string;
  status: "pending" | "ready";
  result_json: string | null;
  lease_token: string | null;
  lease_until: number | null;
  updated_at: number;
  expires_at: number | null;
  hit_count: number;
}

interface SaveRequest {
  cache_key: string;
  lease_token: string;
  subtitle_hash?: string;
  result: AiResult;
}

interface ReleaseRequest {
  cache_key: string;
  lease_token: string;
}

const MAX_BODY_BYTES = 64 * 1024;
const MAX_ADS = 64;
const MAX_TEXT_LENGTH = 1024;
const DEFAULT_TTL_SECONDS = 180 * 24 * 60 * 60;
const DEFAULT_LEASE_SECONDS = 120;
const PUBLIC_PATH_PREFIX = "/biliuwp/video_ad_jump";

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: corsHeaders() });
    }

    const url = new URL(request.url);
    const pathname = normalizePathname(url.pathname);
    try {
      if (request.method === "GET" && pathname === "/v1/health") {
        return json({ ok: true, service: "bili-jump-cache" });
      }

      if (pathname === "/v1/cache/query" && request.method === "POST") {
        const metadata = validateMetadata(await readJson(request));
        return await queryCache(metadata, env, ctx);
      }

      if (pathname === "/v1/cache/claim" && request.method === "POST") {
        const metadata = validateMetadata(await readJson(request));
        return await claimCache(metadata, env);
      }

      if (pathname === "/v1/cache/save" && request.method === "POST") {
        const body = await readJson(request) as unknown as SaveRequest;
        return await saveCache(body, env);
      }

      if (pathname === "/v1/cache/release" && request.method === "POST") {
        const body = await readJson(request) as unknown as ReleaseRequest;
        return await releaseCache(body, env);
      }

      if (pathname === "/v1/admin/stats" && request.method === "GET") {
        requireAdminToken(request, env);
        return await getStats(env);
      }

      if (pathname.startsWith("/v1/admin/cache/") && request.method === "DELETE") {
        requireAdminToken(request, env);
        const cacheKey = pathname.substring("/v1/admin/cache/".length);
        return await deleteCache(cacheKey, env);
      }

      return json({ error: "not_found" }, 404);
    } catch (error) {
      if (error instanceof HttpError) {
        return json({ error: error.code, message: error.message }, error.status);
      }

      console.error(error);
      return json({ error: "internal_error" }, 500);
    }
  }
};

function normalizePathname(pathname: string): string {
  if (pathname === PUBLIC_PATH_PREFIX) {
    return "/";
  }

  if (pathname.startsWith(`${PUBLIC_PATH_PREFIX}/`)) {
    return pathname.substring(PUBLIC_PATH_PREFIX.length);
  }

  return pathname;
}

class HttpError extends Error {
  public readonly status: number;
  public readonly code: string;

  constructor(status: number, code: string, message: string) {
    super(message);
    this.status = status;
    this.code = code;
  }
}

function corsHeaders(): Headers {
  const headers = new Headers();
  headers.set("Access-Control-Allow-Origin", "*");
  headers.set("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
  headers.set("Access-Control-Allow-Headers", "Authorization, Content-Type");
  headers.set("Access-Control-Max-Age", "86400");
  return headers;
}

function json(value: unknown, status = 200): Response {
  const headers = corsHeaders();
  headers.set("Content-Type", "application/json; charset=utf-8");
  headers.set("Cache-Control", "no-store");
  return new Response(JSON.stringify(value), { status, headers });
}

function requireAdminToken(request: Request, env: Env): void {
  const expected = env.CACHE_ADMIN_TOKEN?.trim();
  const received = getBearerToken(request);
  if (!expected || received !== expected) {
    throw new HttpError(401, "unauthorized", "invalid admin token");
  }
}

function getBearerToken(request: Request): string {
  const header = request.headers.get("Authorization") ?? "";
  return header.startsWith("Bearer ") ? header.substring(7).trim() : "";
}

async function readJson(request: Request): Promise<Record<string, unknown>> {
  const contentLength = Number(request.headers.get("Content-Length") ?? 0);
  if (contentLength > MAX_BODY_BYTES) {
    throw new HttpError(413, "payload_too_large", "request body is too large");
  }

  const text = await request.text();
  if (text.length > MAX_BODY_BYTES) {
    throw new HttpError(413, "payload_too_large", "request body is too large");
  }

  try {
    const value = JSON.parse(text);
    if (!value || typeof value !== "object" || Array.isArray(value)) {
      throw new Error("not an object");
    }
    return value as Record<string, unknown>;
  } catch {
    throw new HttpError(400, "invalid_json", "request body must be a JSON object");
  }
}

function validateMetadata(body: Record<string, unknown>): CacheMetadata {
  const aid = readText(body.aid, "aid", 128);
  const cid = readText(body.cid, "cid", 128);
  const provider = readText(body.provider, "provider", 64);
  const api_url = readText(body.api_url, "api_url", 512);
  const model = readText(body.model, "model", 128);
  const prompt_version = readText(body.prompt_version, "prompt_version", 64);
  const title = body.title === undefined ? "" : readText(body.title, "title", MAX_TEXT_LENGTH);
  const duration = body.duration === undefined ? 0 : readNumber(body.duration, "duration", 0, 86400);

  return { aid, cid, provider, api_url, model, prompt_version, title, duration };
}

function readText(value: unknown, name: string, maxLength: number): string {
  if (typeof value !== "string") {
    throw new HttpError(400, "invalid_field", `${name} must be a string`);
  }

  const text = value.trim();
  if (!text || text.length > maxLength || /[\u0000-\u001f]/.test(text)) {
    throw new HttpError(400, "invalid_field", `${name} is invalid`);
  }
  return text;
}

function readNumber(value: unknown, name: string, min: number, max: number): number {
  if (typeof value !== "number" || !Number.isFinite(value) || value < min || value > max) {
    throw new HttpError(400, "invalid_field", `${name} is invalid`);
  }
  return value;
}

async function getCacheKey(metadata: CacheMetadata): Promise<{ cacheKey: string; apiUrlHash: string }> {
  const apiUrlHash = await sha256(metadata.api_url);
  const cacheKey = await sha256([
    metadata.aid,
    metadata.cid,
    metadata.provider,
    apiUrlHash,
    metadata.model,
    metadata.prompt_version
  ].join("\n"));
  return { cacheKey, apiUrlHash };
}

async function sha256(value: string): Promise<string> {
  const data = new TextEncoder().encode(value);
  const digest = await crypto.subtle.digest("SHA-256", data);
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
}

async function queryCache(metadata: CacheMetadata, env: Env, ctx: ExecutionContext): Promise<Response> {
  const { cacheKey } = await getCacheKey(metadata);
  const entry = await getEntry(cacheKey, env);
  const now = unixTime();

  if (!entry) {
    return json({ status: "miss", cache_key: cacheKey });
  }

  if (entry.status === "ready" && entry.expires_at !== null && entry.expires_at > now) {
    const result = parseStoredResult(entry.result_json);
    if (result) {
      ctx.waitUntil(env.DB.prepare(
        "UPDATE ad_cache SET hit_count = hit_count + 1, updated_at = ? WHERE cache_key = ?"
      ).bind(now, cacheKey).run());
      return json({
        status: "hit",
        cache_key: cacheKey,
        result,
        updated_at: entry.updated_at,
        expires_at: entry.expires_at
      });
    }
  }

  if (entry.status === "pending" && (entry.lease_until ?? 0) > now) {
    return json({
      status: "pending",
      cache_key: cacheKey,
      lease_until: entry.lease_until,
      retry_after_ms: 1000
    });
  }

  return json({ status: "miss", cache_key: cacheKey });
}

async function claimCache(metadata: CacheMetadata, env: Env): Promise<Response> {
  const { cacheKey, apiUrlHash } = await getCacheKey(metadata);
  const now = unixTime();
  const leaseToken = crypto.randomUUID();
  const leaseUntil = now + getSeconds(env.CACHE_LEASE_SECONDS, DEFAULT_LEASE_SECONDS, 30, 600);

  const result = await env.DB.prepare(`
    INSERT INTO ad_cache (
      cache_key, aid, cid, provider, api_url_hash, model, prompt_version,
      title, duration, status, result_json, lease_token, lease_until,
      created_at, updated_at, expires_at, hit_count
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'pending', NULL, ?, ?, ?, ?, NULL, 0)
    ON CONFLICT(cache_key) DO UPDATE SET
      status = 'pending',
      result_json = NULL,
      title = excluded.title,
      duration = excluded.duration,
      lease_token = excluded.lease_token,
      lease_until = excluded.lease_until,
      updated_at = excluded.updated_at,
      expires_at = NULL
    WHERE (ad_cache.status = 'pending' AND COALESCE(ad_cache.lease_until, 0) <= excluded.updated_at)
       OR (ad_cache.status = 'ready' AND COALESCE(ad_cache.expires_at, 0) <= excluded.updated_at)
  `).bind(
    cacheKey,
    metadata.aid,
    metadata.cid,
    metadata.provider,
    apiUrlHash,
    metadata.model,
    metadata.prompt_version,
    metadata.title ?? "",
    metadata.duration ?? 0,
    leaseToken,
    leaseUntil,
    now,
    now
  ).run();

  if ((result.meta?.changes ?? 0) > 0) {
    return json({
      status: "leader",
      cache_key: cacheKey,
      lease_token: leaseToken,
      lease_until: leaseUntil
    });
  }

  const entry = await getEntry(cacheKey, env);
  if (entry?.status === "ready" && (entry.expires_at ?? 0) > now) {
    const stored = parseStoredResult(entry.result_json);
    if (stored) {
      const hitUpdate = await env.DB.prepare(`
        UPDATE ad_cache
        SET title = CASE WHEN ? <> '' THEN ? ELSE title END,
            duration = CASE WHEN ? > 0 THEN ? ELSE duration END,
            hit_count = hit_count + 1,
            updated_at = ?
        WHERE cache_key = ? AND status = 'ready'
          AND COALESCE(expires_at, 0) > ?
      `).bind(
        metadata.title ?? "",
        metadata.title ?? "",
        metadata.duration ?? 0,
        metadata.duration ?? 0,
        now,
        cacheKey,
        now
      ).run();

      if ((hitUpdate.meta?.changes ?? 0) > 0) {
        return json({ status: "hit", cache_key: cacheKey, result: stored, updated_at: now, expires_at: entry.expires_at });
      }
    }
  }

  return json({
    status: "pending",
    cache_key: cacheKey,
    lease_until: entry?.lease_until ?? null,
    retry_after_ms: 1000
  });
}

async function saveCache(body: SaveRequest, env: Env): Promise<Response> {
  if (!body || typeof body.cache_key !== "string" || !/^[a-f0-9]{64}$/.test(body.cache_key)) {
    throw new HttpError(400, "invalid_field", "cache_key is invalid");
  }
  if (typeof body.lease_token !== "string" || body.lease_token.length < 16 || body.lease_token.length > 128) {
    throw new HttpError(400, "invalid_field", "lease_token is invalid");
  }

  const result = validateResult(body.result);
  const resultJson = JSON.stringify(result);
  if (resultJson.length > MAX_BODY_BYTES) {
    throw new HttpError(413, "payload_too_large", "result is too large");
  }
  const subtitleHash = body.subtitle_hash === undefined
    ? null
    : readText(body.subtitle_hash, "subtitle_hash", 128);
  const now = unixTime();
  const expiresAt = now + getSeconds(env.CACHE_TTL_SECONDS, DEFAULT_TTL_SECONDS, 3600, 31536000);

  const update = await env.DB.prepare(`
    UPDATE ad_cache
    SET status = 'ready', result_json = ?, subtitle_hash = COALESCE(?, subtitle_hash),
        lease_token = NULL, lease_until = NULL, updated_at = ?, expires_at = ?
    WHERE cache_key = ? AND status = 'pending' AND lease_token = ?
      AND COALESCE(lease_until, 0) >= ?
  `).bind(resultJson, subtitleHash, now, expiresAt, body.cache_key, body.lease_token, now).run();

  if ((update.meta?.changes ?? 0) === 0) {
    throw new HttpError(409, "lease_conflict", "cache lease is missing or expired");
  }

  return json({ status: "saved", cache_key: body.cache_key, expires_at: expiresAt });
}

async function releaseCache(body: ReleaseRequest, env: Env): Promise<Response> {
  if (!body || typeof body.cache_key !== "string" || !/^[a-f0-9]{64}$/.test(body.cache_key)
    || typeof body.lease_token !== "string") {
    throw new HttpError(400, "invalid_field", "cache lease is invalid");
  }

  await env.DB.prepare(
    "DELETE FROM ad_cache WHERE cache_key = ? AND status = 'pending' AND lease_token = ?"
  ).bind(body.cache_key, body.lease_token).run();
  return json({ status: "released", cache_key: body.cache_key });
}

function validateResult(value: unknown): AiResult {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new HttpError(400, "invalid_result", "result must be an object");
  }

  const raw = value as Record<string, unknown>;
  if (!Array.isArray(raw.ads) || raw.ads.length > MAX_ADS) {
    throw new HttpError(400, "invalid_result", "ads must be an array");
  }

  const ads = raw.ads.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      throw new HttpError(400, "invalid_result", `ads[${index}] is invalid`);
    }

    const ad = item as Record<string, unknown>;
    const start = readNumber(ad.start_time, `ads[${index}].start_time`, 0, 86400);
    const end = readNumber(ad.end_time, `ads[${index}].end_time`, 0, 86400);
    if (end <= start) {
      throw new HttpError(400, "invalid_result", `ads[${index}] has an invalid range`);
    }

    return {
      start_time: start,
      end_time: end,
      product_name: optionalText(ad.product_name, `ads[${index}].product_name`),
      ad_content: optionalText(ad.ad_content, `ads[${index}].ad_content`)
    };
  });

  return {
    ads,
    msg: optionalText(raw.msg, "msg")
  };
}

function optionalText(value: unknown, name: string): string {
  if (value === undefined || value === null) {
    return "";
  }

  if (typeof value !== "string") {
    throw new HttpError(400, "invalid_result", `${name} must be a string`);
  }

  const text = value.trim();
  if (text.length > MAX_TEXT_LENGTH || /[\u0000-\u001f]/.test(text)) {
    throw new HttpError(400, "invalid_result", `${name} is invalid`);
  }
  return text;
}

function parseStoredResult(value: string | null): AiResult | null {
  if (!value) {
    return null;
  }
  try {
    return validateResult(JSON.parse(value));
  } catch {
    return null;
  }
}

async function getEntry(cacheKey: string, env: Env): Promise<CacheEntry | null> {
  return await env.DB.prepare(`
    SELECT cache_key, status, result_json, lease_token, lease_until,
           updated_at, expires_at, hit_count
    FROM ad_cache WHERE cache_key = ?
  `).bind(cacheKey).first<CacheEntry>();
}

async function getStats(env: Env): Promise<Response> {
  const row = await env.DB.prepare(`
    SELECT COUNT(*) AS total,
           SUM(CASE WHEN status = 'ready' THEN 1 ELSE 0 END) AS ready,
           SUM(CASE WHEN status = 'pending' THEN 1 ELSE 0 END) AS pending,
           COALESCE(SUM(hit_count), 0) AS hits
    FROM ad_cache
  `).first<{ total: number; ready: number; pending: number; hits: number }>();
  return json({
    total: Number(row?.total ?? 0),
    ready: Number(row?.ready ?? 0),
    pending: Number(row?.pending ?? 0),
    hits: Number(row?.hits ?? 0)
  });
}

async function deleteCache(cacheKey: string, env: Env): Promise<Response> {
  if (!/^[a-f0-9]{64}$/.test(cacheKey)) {
    throw new HttpError(400, "invalid_field", "cache_key is invalid");
  }
  await env.DB.prepare("DELETE FROM ad_cache WHERE cache_key = ?").bind(cacheKey).run();
  return json({ status: "deleted", cache_key: cacheKey });
}

function unixTime(): number {
  return Math.floor(Date.now() / 1000);
}

function getSeconds(value: string | undefined, fallback: number, min: number, max: number): number {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    return fallback;
  }
  return Math.min(Math.max(Math.floor(parsed), min), max);
}
