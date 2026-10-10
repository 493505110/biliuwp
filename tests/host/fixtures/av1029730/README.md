# BV1Bs411Z7rR / av1029730 原始 M8 脚本

来源：`https://api.bilibili.com/x/v2/dm/web/view?type=1&oid=1490503&pid=1029730`
返回的专包 `https://i0.hdslb.com/bfs/dm/9ca74f16dbaaa9d25fc852fe3d6f4ffd0a6ec75f.bin`。
获取日期：2026-10-10。脚本保留作者原有版权说明和完整内嵌图片，未修写源码。

`scripts.json` 包含专包的全部三条 mode=8 数据：

- `427804955`，0 秒：游戏主体（键盘移动、弹幕碰撞、复活、成绩累计）。
- `426023650`，329 秒：最终成绩与图片。
- `3763336267`，600000 秒：`trace('2017');`，服务端原始时间戳如此，不参与正常视频播放。

包装字段 `stime` 从 protobuf progress 毫秒换成秒，`duration` 为测试时间窗；
`code` 原样保留，包括主脚本图片字符串中的直接换行。

验证入口：`node --experimental-vm-modules tests/host/av1029730-game.test.js`。
像素与交互测试使用本地 Edge 和 Playwright（与现有 browser-host.js 一致）。
