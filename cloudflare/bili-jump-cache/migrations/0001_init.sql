CREATE TABLE IF NOT EXISTS ad_cache (
    cache_key TEXT PRIMARY KEY,
    aid TEXT NOT NULL,
    cid TEXT NOT NULL,
    provider TEXT NOT NULL,
    api_url_hash TEXT NOT NULL,
    model TEXT NOT NULL,
    prompt_version TEXT NOT NULL,
    subtitle_hash TEXT,
    title TEXT NOT NULL DEFAULT '',
    duration REAL NOT NULL DEFAULT 0,
    status TEXT NOT NULL CHECK (status IN ('pending', 'ready')),
    result_json TEXT,
    lease_token TEXT,
    lease_until INTEGER,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    expires_at INTEGER,
    hit_count INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_ad_cache_video
ON ad_cache(aid, cid);

CREATE INDEX IF NOT EXISTS idx_ad_cache_expire
ON ad_cache(expires_at);

CREATE INDEX IF NOT EXISTS idx_ad_cache_status
ON ad_cache(status, lease_until);
