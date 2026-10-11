# av2561316 / BV1Hs411S7Ru 原始 M8 脚本

《高级弹幕游戏 Duet Game 二重奏 无尽模式》，游戏本体 cid=4001327，分 P 时长 1918 秒。
获取日期：2026-10-11。

来源：`https://api.bilibili.com/x/v2/dm/web/view?type=1&oid=4001327&pid=2561316`
返回的专包：`http://i0.hdslb.com/bfs/dm/29c4e7204fd961ac4c391044e2216c878c2673fa.bin`。

`scripts.json` 完整保留专包中 16 条 mode=8 脚本的源码、ID 和进度时间；包装字段
`duration=0` 表示原始代码弹幕没有声明条目窗口。包含帧率设置、开场、游戏主体和
切换 BGM 的定位脚本。测试追加的探针不改写原始源码。

零依赖回归：`node tests/host/av2561316-game.test.js`。
真实 Edge 像素验证：同一命令追加 `--browser`，使用现有 Playwright/browser-host。
