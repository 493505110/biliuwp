# av2676643 / BV1Ks411m7hq 原始 M8 脚本

《【弹幕黑科技】名副其实的弹幕游戏·第二弹（完成）》，cid=4180608，时长 375 秒。
获取日期：2026-10-11。

来源：`https://api.bilibili.com/x/v2/dm/web/view?type=1&oid=4180608&pid=2676643`
返回的专包：`http://i0.hdslb.com/bfs/dm/55ec949dea3f2f33209eac52d0f3f1fc8cbad3d0.bin`。

`scripts.json` 完整保留专包中 58 条 mode=8 脚本的源码、ID 和进度时间；
`duration=0` 表示原始代码弹幕未声明条目窗口。包括原始位图、开始和继续按钮、
游戏主体及全部关卡脚本。138.9 秒与 153.895 秒的非法 break 也原样保留。
测试只在内存中的副本追加观测探针，不改写原始作品。

零依赖回归：`node tests/host/av2676643-game.test.js`。
真实 Edge 验证：同一命令追加 `--browser`，使用 Playwright/browser-host。
