# av2612400 原始 M8 脚本

《【高级弹幕游戏】听说你们很喜欢躲弹幕，那这次来躲流星雨吧》，作者深海的海参。
视频：<https://www.bilibili.com/video/BV14s411S7AH/>，CID 4120195，时长 700 秒。
获取日期：2026-10-11。

索引：<https://api.bilibili.com/x/v2/dm/web/view?type=1&oid=4120195&pid=2612400>。
专包：<https://i0.hdslb.com/bfs/dm/0ec2f52b24e0f3f8b0e398c2171813c58933dcbd.bin>。

`scripts.json` 保留四条 mode=8 弹幕的源码、作者头部、ID 和时间戳。
1042796465 是游戏正文，其余三条执行时间跳转。测试仅在内存副本中追加观测探针。

运行 `node tests/host/av2612400-game.test.js`；追加 `--browser` 运行真实 Edge 像素验证，
需要已有 Playwright。`M8_ARTIFACT_DIR` 可指定截图目录，`M8_BASE_REVISION=HEAD`
可对照修改前的浏览器宿主（配合 `--browser-only`）。
Home 回归通过真实 keyUp 消息执行原始开关，验证滚动弹幕屏蔽、恢复及顶底弹幕保留。
