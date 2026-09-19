# AGENTS.md

哔哩哔哩 UWP 客户端字幕广告 AI 识别的 Cloudflare Worker 公共缓存服务。Worker 本身**不调用** AI 提供商：客户端在请求 AI 之前先查 D1 缓存，识别完成后再提交经过校验的结果。

本目录独立于仓库根的 UWP 工程，不在 `BiliBili.sln` 中，不参与 Visual Studio 构建，与根目录 `AGENTS.md` 描述的项目没有编译期依赖。

## 部署

1. 安装 Node.js，并运行 `npm install`。
2. 登录 Cloudflare：`npx wrangler login`。
3. 创建 D1 数据库：`npx wrangler d1 create bili-jump-cache`。
4. 将命令返回的 `database_id` 填入 `wrangler.jsonc`。
5. 将 `.dev.vars.example` 复制为 `.dev.vars`，并替换管理员令牌。
6. 应用本地数据库迁移：`npm run db:migrate:local`。
7. 启动本地开发服务：`npm run dev`。
8. 应用远程数据库迁移：`npm run db:migrate:remote`。
9. 设置生产环境管理员密钥：`npx wrangler secret put CACHE_ADMIN_TOKEN`。
10. 部署 Worker：`npm run deploy`。

## 文件职责

| 路径 | 说明 |
|---|---|
| `src/index.ts` | 唯一的 Worker 源文件：路由、校验、D1 访问和响应封装全部在这里，没有拆分子模块 |
| `migrations/` | D1 迁移，`migrations_dir` 已在 `wrangler.jsonc` 中指定为 `migrations` |
| `wrangler.jsonc` | Worker 名称、入口、`compatibility_date`、D1 绑定 `DB` 和 `database_id` |
| `.dev.vars.example` | 本地变量模板（`CACHE_ADMIN_TOKEN`、`CACHE_TTL_SECONDS`、`CACHE_LEASE_SECONDS`） |
| `tsconfig.json` | `strict` + `noEmit`，只做类型检查，构建由 wrangler 负责 |

`tsconfig.json` 的 `noEmit` 为 `true`，因此 `npx tsc --noEmit` 是唯一的静态检查手段；本目录没有 lint、格式化配置和测试，也没有 CI 流程（仓库的 `.github/workflows/` 只覆盖 UWP 主工程），部署全部靠手动执行，本地验证靠 `npm run dev` 加本地 D1。

## 接口

生产环境固定地址为 `https://api.zhou2008.cn/biliuwp/video_ad_jump`。`normalizePathname()` 会剥掉这个固定前缀再匹配路由，因此**同一份代码在生产走带前缀的路径，本地开发直接走裸 `/v1/...`**。

公开接口（无需令牌）：

| 方法 | 路径 | 用途 |
|---|---|---|
| `POST` | `/v1/cache/query` | 查询缓存，返回 `hit`、`miss` 或 `pending` |
| `POST` | `/v1/cache/claim` | 原子申请短期 AI 识别租约，返回 `leader`、`hit` 或 `pending` |
| `POST` | `/v1/cache/save` | 提交租约对应的识别结果 |
| `POST` | `/v1/cache/release` | 释放识别失败或取消的租约 |
| `GET` | `/v1/health` | 健康检查 |

管理员接口使用 `Authorization: Bearer <CACHE_ADMIN_TOKEN>`，仅用于维护缓存：

| 方法 | 路径 | 用途 |
|---|---|---|
| `GET` | `/v1/admin/stats` | 返回 `total` / `ready` / `pending` / `hits` 统计 |
| `DELETE` | `/v1/admin/cache/{cache_key}` | 删除单条缓存 |

所有响应都带 CORS 头（`Access-Control-Allow-Origin: *`），`OPTIONS` 直接返回 204；JSON 响应固定 `Cache-Control: no-store`。未知路由返回 `404 {"error":"not_found"}`，抛出的 `HttpError` 会转成 `{error, message}` 加对应状态码，其余异常统一 `500 internal_error`。

## 缓存语义

- `cache_key = sha256(aid \n cid \n provider \n sha256(api_url) \n model \n prompt_version)`，`api_url` 只以哈希参与，避免在 D1 中存明文接口地址。
- `claim` 用 `INSERT ... ON CONFLICT ... WHERE` 做原子抢占：只有当前行已过期（pending 且租约超时，或 ready 且 TTL 超时）才会被覆盖，成功者拿到 `lease_token` 成为 `leader`；抢不到时若已有有效结果则返回 `hit`，否则返回 `pending` + `retry_after_ms: 1000`。
- `save` 必须带匹配的 `lease_token` 且租约未过期，否则 `409 lease_conflict`；写入后 `status = 'ready'` 并计算 `expires_at`。
- `release` 直接删除 `status = 'pending'` 且 `lease_token` 匹配的行。
- 默认 TTL 180 天、租约 120 秒；环境变量会被夹到区间内（TTL 3600~31536000，租约 30~600），非法值回退到默认值。
- `query` 命中时通过 `ctx.waitUntil` 异步累加 `hit_count`，不阻塞响应。

## 输入校验

- 请求体必须是 JSON 对象（数组和非对象一律 `400 invalid_json`），Content-Length 或实际长度超过 64 KiB 返回 `413 payload_too_large`。
- 文本字段去首尾空白后不得为空、不得超过各自长度上限（普通字段 64~512，`title` / `msg` / `product_name` / `ad_content` 为 1024），且不得含 `U+0000~U+001F` 控制字符。
- `duration`、`start_time`、`end_time` 必须在 `0~86400`，且每段 `end_time > start_time`；`ads` 最多 64 条。
- `cache_key` 必须是 64 位小写十六进制，`lease_token` 长度 16~128。

## 数据存储

D1 只保存规范化后的广告识别结果和视频元数据，**不保存字幕内容、AI API 密钥、Cookie 或用户登录信息**；`subtitle_hash` 只存哈希。新增字段或日志时保持这条边界。

## 后台清理

`wrangler.jsonc` 的 `triggers.crons` 配置了 `0 3 * * *`（**UTC**，每天一次），对应 `src/index.ts` 的 `scheduled` handler，它只做一件事：清理两类行。

| 清理对象 | 条件 |
|---|---|
| 过期结果 | `status = 'ready'` 且 `expires_at <= now` |
| 陈旧租约 | `status = 'pending'` 且 `lease_until <= now - 1 小时`（宽限期给慢客户端留补救窗口） |

两类分别走 `idx_ad_cache_expire` 和 `idx_ad_cache_status` 索引，通过 `DELETE ... WHERE cache_key IN (SELECT ... LIMIT ?)` 分批删除，每批 500 行、每次最多 10 批，避免单次查询过长撞上 D1 的耗时与子请求限制；一轮没清空就留到下一次 cron。执行行数写入 `console.log`，可在 `wrangler tail` 或 observability 日志中查看。

清理是幂等的，只在 `scheduled` 里跑，不挂在请求路径上。

## 关键陷阱

- 不要随意改动 `getCacheKey()` 的参与字段或拼接顺序：任一变化都会让全量缓存 key 改变，等价于清空线上缓存。
- `CACHE_ADMIN_TOKEN` 未设置时管理员接口**不是放行而是全部 401**（`requireAdminToken()` 在 `!expected` 时直接拒绝）。本地调试必须准备 `.dev.vars`。
- 前缀剥离只识别 `/biliuwp/video_ad_jump`（含其后的 `/`），其它路径按原样匹配；改动前缀会影响线上客户端，需与客户端同时上线。
- 迁移文件一旦应用过就不要修改，新变更追加编号更大的文件；本地与远程迁移要分别执行 `db:migrate:local` / `db:migrate:remote`。
- 部署即对生产生效，没有预发环境；改动先在 `npm run dev` 配合本地 D1 验证。
- `triggers.crons` 的改动要重新 `npm run deploy` 才同步到生产。本地验证用 `npx wrangler dev --test-scheduled`，再 `curl "http://127.0.0.1:8787/__scheduled?cron=0+3+*+*+*"` 手动触发一次。
- 路由、请求/响应字段变化时，同步更新本文件与 `README.md`。

## 常用命令

```bash
npm install                      # 安装依赖
npx wrangler login               # 登录 Cloudflare
npm run dev                      # 本地开发服务
npm run deploy                   # 部署到生产
npm run db:migrate:local         # 应用本地迁移
npm run db:migrate:remote        # 应用远程迁移
npx wrangler secret put CACHE_ADMIN_TOKEN   # 设置生产管理员密钥
npx tsc --noEmit                 # 类型检查
```
