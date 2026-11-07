CREATE TABLE ad_cache_video (
    cache_key INTEGER PRIMARY KEY AUTOINCREMENT,
    aid TEXT NOT NULL,
    cid TEXT NOT NULL,
    model TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('pending', 'ready')),
    result_json TEXT,
    lease_token TEXT,
    lease_until INTEGER,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    hit_count INTEGER NOT NULL DEFAULT 0,
    UNIQUE (aid, cid)
);

-- Old leases reference retired keys. Only completed results can be transferred.
WITH ranked AS (
    SELECT *, ROW_NUMBER() OVER (
        PARTITION BY aid, cid
        ORDER BY updated_at DESC, cache_key
    ) AS preference,
    SUM(hit_count) OVER (PARTITION BY aid, cid) AS video_hits
    FROM ad_cache
    WHERE status = 'ready' AND expires_at > unixepoch()
)
INSERT INTO ad_cache_video
    (aid, cid, model, status, result_json, created_at, updated_at, hit_count)
SELECT aid, cid, model, status, result_json, created_at, updated_at, video_hits
FROM ranked WHERE preference = 1;

DROP TABLE ad_cache;
ALTER TABLE ad_cache_video RENAME TO ad_cache;
CREATE INDEX idx_ad_cache_status ON ad_cache(status, lease_until);
