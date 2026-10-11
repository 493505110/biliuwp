# BPM 原始 M8 脚本

- 视频：<https://www.bilibili.com/video/BV1Ks411m7A7/>（av2677148），《【高级弹幕视频】BPM》，作者深海的海参。
- CID：4181326；2026-10-11 从视频弹幕专包读取三条 mode=8 弹幕。
- 专包：<https://i0.hdslb.com/bfs/dm/bacd1d09b05a895a71fbbf261edeef8295ec5c2e.bin>。
- `1065539003`：Akari 框架；`1066497941`：压缩字体；`1086695021`：主动画。

`scripts.json` 保留原始脚本文本（含版权与许可头）、弹幕 ID 和默认零时间点。
回归测试直接加载原文，不预先修改关键字、跨行返回或字体数据。
Akari 源码与许可按原始头部声明：<https://github.com/akaza-akari/Akari.biliScript>。

运行 `node tests/host/av2677148-compat.test.js`；设置 `M8_BROWSER_TESTS=1` 可额外运行真实浏览器验证，需已有 Playwright 与 Edge。
