# av2785213 / BV1Bs411m7eM 原始 M8 脚本

《【高级弹幕】弹幕也能玩音乐游戏~(完成)》，作者 hello80。
获取日期：2026-10-31；Easy、Normal 各 151 秒，各保留完整四条 mode=8 脚本。

| 分 P | CID | 原始专包 |
| --- | --- | --- |
| Easy | 4350048 | https://i0.hdslb.com/bfs/dm/a4dcd6afd2704fced84129b4ec336c05f471491f.bin |
| Normal | 4350484 | https://i0.hdslb.com/bfs/dm/ae8f8e98a3838006edb3b53da2a1733587286023.bin |

专包索引：`https://api.bilibili.com/x/v2/dm/web/view?type=1&oid=<CID>&pid=2785213`。
`easy/scripts.json` 与 `normal/scripts.json` 保留源码、ID 和进度时间，`duration=0`
表示原始条目没有声明时间窗。测试仅在内存中的游戏主体副本追加观测探针。

运行 `node tests/host/av2785213-input.test.js`，覆盖四轨原始判定、模拟消息延后、
旧消息兼容、回调时钟恢复和高亮寿命。这是宿主逻辑回归，不是 UWP 实际输入延迟测量。
另逐拍运行两个难度的完整谱面，检查 Perfect 时音符与白色判定线相交，
并确认首次遇到未声明的缓动变量时不会重跑正文或重排音符定时器。

运行 `node tests/host/av2785213-input-browser.test.js`，在真实 Edge 中验证原游戏的
音符消除和击打高亮已在下一次 RAF 前写入主画布，并报告首次输入绘制耗时与 GPU。
同时在第一拍 Perfect 时刻检查音符到线位置与游戏正文执行次数。
需要已有 Playwright；`M8_BASE_REVISION=HEAD` 可对照修改前的宿主。
