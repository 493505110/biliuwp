# bili-jump-cache

每个视频分 P 共享一份广告识别结果。`POST /v1/cache/query` 传 `aid` 和 `cid`，`POST /v1/cache/claim` 另传 `model` 以记录识别模型。申请成功后使用返回的 `cache_key` 和 `lease_token` 保存结果或释放租约。

升级时先应用 D1 迁移 0002，再部署配套 Worker。迁移只导入当时仍有效的已完成结果，同一分 P 保留最近更新的一条。旧租约结束，新缓存结果保留至管理员删除。完整接口与部署步骤见 [AGENTS.md](AGENTS.md)。
