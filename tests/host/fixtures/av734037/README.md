# av734037 / BV1Ss411f7LF 原始 M8 脚本

《【蓝白弹幕祭03应援】— Brave Heart》，cid=1076044，时长 94 秒。
获取日期：2026-10-11。

来源：`https://api.bilibili.com/x/v2/dm/web/view?type=1&oid=1076044&pid=734037`
返回的专包：`https://i0.hdslb.com/bfs/dm/b5f5207926a69728281dcd2f79e0d2183909cf84.bin`。

`scripts.json` 保留专包中唯一一条 mode=8 脚本（ID=295719347）的源码、作者说明和时间戳。
`duration=0` 表示原始条目未声明窗口。原作里的未声明变量、`shape_light.endFill()`
及坐标数量不足的 `drawPath` 均原样保留；测试仅在内存副本末尾添加观测探针。

零依赖回归：`node tests/host/av734037-compat.test.js`。
真实 Edge 完整时间轴、画面像素、跳转及缩放验证：同一命令追加 `--browser`。

贝塞尔插值语义参照 BetweenAS3 的
[BezierUpdater](https://github.com/hyoshida/BetweenAS3/blob/master/src/fp10/org/libspark/betweenas3/core/updaters/BezierUpdater.as)：
一个控制点为二次曲线；多个控制点以相邻中点为段界，而非高阶单段曲线。
