# Bili Jump Cache Worker

This Cloudflare Worker provides shared cache storage for the UWP subtitle ad recognizer. The Worker does not call an AI provider. Clients query D1 before calling AI, then submit a validated result after recognition.

## Setup

1. Install Node.js and run `npm install`.
2. Run `npx wrangler login`.
3. Create the database with `npx wrangler d1 create bili-jump-cache`.
4. Put the returned `database_id` into `wrangler.jsonc`.
5. Copy `.dev.vars.example` to `.dev.vars` and replace the admin token.
6. Apply the local migration with `npm run db:migrate:local`.
7. Start local development with `npm run dev`.
8. Apply the remote migration with `npm run db:migrate:remote`.
9. Configure the production admin secret with `npx wrangler secret put CACHE_ADMIN_TOKEN`.
10. Deploy with `npm run deploy`.

## Client API

The production base URL is `https://api.zhou2008.cn/biliuwp/video_ad_jump`. The Worker removes this fixed prefix before routing requests, so the following paths are available:

- `POST /biliuwp/video_ad_jump/v1/cache/query`: return `hit`, `miss`, or `pending`.
- `POST /biliuwp/video_ad_jump/v1/cache/claim`: atomically acquire a short AI recognition lease.
- `POST /biliuwp/video_ad_jump/v1/cache/save`: commit a result held by the lease.
- `POST /biliuwp/video_ad_jump/v1/cache/release`: release a failed recognition lease.
- `GET /biliuwp/video_ad_jump/v1/health`: unauthenticated health check.

For local development, the same endpoints can also be called directly as `/v1/cache/...` and `/v1/health`.

The cache endpoints are public and do not require a client token. The admin endpoints use `Authorization: Bearer <CACHE_ADMIN_TOKEN>` and are intended for maintenance only.

The D1 database stores normalized result JSON and metadata, never subtitles, API keys, cookies, or user login data.
