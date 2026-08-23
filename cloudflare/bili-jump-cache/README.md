# Bili Jump 公共缓存 Worker

这是用于哔哩哔哩 UWP 客户端字幕广告 AI 识别的 Cloudflare Worker 公共缓存服务。Worker 本身不会调用 AI 提供商，客户端在请求 AI 前先查询 D1 缓存，识别完成后再提交经过校验的结果。

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

## 客户端接口

生产环境固定地址为 `https://api.zhou2008.cn/biliuwp/video_ad_jump`。Worker 会自动移除这个固定路径前缀，然后处理以下接口：

- `POST /biliuwp/video_ad_jump/v1/cache/query`：查询缓存，返回 `hit`、`miss` 或 `pending`。
- `POST /biliuwp/video_ad_jump/v1/cache/claim`：原子申请短期 AI 识别租约。
- `POST /biliuwp/video_ad_jump/v1/cache/save`：提交租约对应的识别结果。
- `POST /biliuwp/video_ad_jump/v1/cache/release`：释放识别失败或取消的租约。
- `GET /biliuwp/video_ad_jump/v1/health`：健康检查，无需身份验证。

本地开发时，也可以直接使用 `/v1/cache/...` 和 `/v1/health` 路径访问同一组接口。

缓存接口面向公众开放，不需要客户端令牌。管理员接口使用 `Authorization: Bearer <CACHE_ADMIN_TOKEN>` 请求头，仅用于维护缓存。

## 数据存储

D1 只保存规范化后的广告识别结果和视频元数据，不保存字幕内容、AI API 密钥、Cookie 或用户登录信息。
