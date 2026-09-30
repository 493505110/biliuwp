# M8 实际渲染修复

本轮对照 `play_20181010_decompiled` 和 av2669196 的 11 条原样脚本，修复“API 能调用，但参数没有进入绘制”及文字排版问题。

- `matrix3D`、z、rotationX/Y 参与父子世界变换和透视投影；相对矩阵保留深度。二维 Matrix 是完整变换，不再重复叠加元素位置。Matrix3D 的 append/prepend 顺序按 Flash 修正。
- 没有 3D 子节点的容器先缓存成平面，再一次投影。静态缓存复用，隐藏子树跳过绘制准备。缓存字段不可枚举，避免 Akari clone 错走普通对象分支。
- GPU 绘制缓冲复用视口尺寸。普通混合的图元在同一合成层中批量绘制，在遮罩、滤镜或其他混合模式的边界回贴，避免每个字形分别同步 WebGL 与 Canvas；批次内按显示列表顺序混合，并保留各图元透明度。
- GPU 纹理按位图版本复用，颜色、滤镜或内容变更时重新上传；最近使用缓存限制为 64 MiB 或 1024 个纹理，避免长时间播放无限增长。
- 缓存分辨率随最终显示倍率在 1/64–8 倍间分档变化，改善小字形放大时的模糊，也避免把几千单位的字体轮廓当作原尺寸纹理上传；纯移动不重烘。单边位图上限 4096 像素，避免场外大图元占用过多内存。
- BlurFilter、GlowFilter 按各自参数签名创建，依次作用于叶子或整份容器位图；支持滤镜颜色、强度、横纵模糊、quality、inner/knockout。缓存扩边由滤镜确定。ColorTransform 的乘数和偏移进入实际像素运算。
- 显示对象 width/height 同步读取几何尺寸，包含子树和缩放，不包含滤镜扩边，供 Akari 的字距、居中与对齐使用。图片原始尺寸独立存储。
- drawPath 保留复合轮廓与 winding，二次/三次曲线真正绘制，WIDE 命令按四个数据消费。曲线极值参与尺寸计算，字形空洞不会被分轮廓填实。
- motion.startDelay 按原版的毫秒单位处理。容器自身 graphics 和嵌套兄弟的边界保留。
- 先清除变更区域，再按显示列表顺序重贴所有相交元件，避免移动时颠倒叠放顺序或累积半透明像素。互不相交的损坏区域保持分离。

## 验证

Node 桩测试验证生命周期、显示列表和真实脚本运行；`rendering-pixels.test.js` 使用本地 Edge 的真实 Canvas，验证 23 条颜色、滤镜、3D、排版、曲线与残影回归。它需要已有 Playwright 和 Edge，可通过 `NODE_PATH` 指向本机的 Playwright；`BROWSER_EXECUTABLE` 可以指定其他 Chromium。不会修改依赖清单或下载安装浏览器。

```powershell
node tests/host/retained-mode.test.js
node tests/host/real-m8-scripts.test.js
node tests/host/tween-easing.test.js
node tests/host/rendering-pixels.test.js
dotnet test tests/BiliBili.Tests/BiliBili.Tests.csproj --no-restore
```

`av2669196-visual.test.js` 以固定随机种子、60 Hz 帧时钟连续推进全部脚本，默认抓取 105–142 秒关键帧，不通过倍速或 seek 跳过中间状态。设置 `M8_ARTIFACT_DIR` 保存 PNG 和亮度指标；`M8_BASE_REVISION=HEAD` 对照修改前；`M8_SAMPLE_SECONDS` 调整采样点；`M8_VIEWPORT_WIDTH/HEIGHT` 指定画布尺寸。原录屏中的随机动画与种子、播放器画布尺寸及采样时刻不同，亮像素比例不能独立证明逐像素一致。

`M8_START_SECOND` 可用于复核局部场景切换；非零起点的结果属于局部连续播放，不能替代默认从 0 秒开始的完整播放验证。

本轮最终验证结果：宿主 43 项、真实脚本 5 项、缓动 9 项、Edge 像素 23 项、C# 247 项均通过；Visual Studio MSBuild 的 Debug|x64 构建及 x86/x64 测试包生成成功。最终源码在 1280×720、60 Hz 下从 105 秒连续推进至 142 秒，11 条脚本无页面或桥接运行错误，105/110/115/119/121 秒均有画面。119 秒的平均亮度约 158，未复现空白画面。从 0 秒开始的最终完整 60 Hz 回归因复杂前段耗时过长，在 40 秒后停止；不将分段结果视为完整连续播放验证。110 秒的圆形位置和动画相位与参考录屏仍有差异，参考的随机种子与准确时间偏移尚未统一。

仍需在 UWP 视频页验证实际 WebView2 的帧率、缩放、暂停恢复和跳转。Blur/Glow 使用重复盒式模糊，不保证与 Flash 原生滤镜逐像素相同；含非共面 3D 子节点的容器滤镜在投影后的屏幕坐标计算。无 WebGL 时使用分块 Canvas 近似投影。DropShadow、Bevel、ColorMatrix 等其余滤镜仍未进入绘制，scrollRect 仍只保存参数。没有以这些回归测试宣称完整 Flash 兼容。
