# M8 实际渲染修复

本轮对照 `play_20181010_decompiled` 和 av2669196 的 11 条原样脚本，修复“API 能调用，但参数没有进入绘制”及文字排版问题。

- `matrix3D`、z、rotationX/Y 参与父子世界变换和透视投影；相对矩阵保留深度。二维 Matrix 是完整变换，不再重复叠加元素位置。Matrix3D 的 append/prepend 顺序按 Flash 修正。
- 区分独立与绑定投影：独立 PerspectiveProjection 按 500 宽度计算焦距，transform 上的投影采用本地原版实测的固定 300 宽度基准；55° 时焦距约 288.147308，窗口与全屏相同，不使用当前 stageWidth 或播放器宽度计算。赋值复制视角和中心，输入对象后续修改不影响元件；clone 保留投影类型和视角/焦距联动。projectionCenter 读取返回坐标副本。投影替换或参数更新会重投影子树并擦除旧像素；视口尺寸变化时重建根投影中心，子树显式投影保持自己的中心。
- 没有 3D 子节点的容器先缓存成平面，再一次投影。静态缓存复用，隐藏子树跳过绘制准备。缓存字段不可枚举，避免 Akari clone 错走普通对象分支。
- GPU 绘制缓冲复用视口尺寸。普通混合的图元在同一合成层中批量绘制，在遮罩、滤镜或其他混合模式的边界回贴，避免每个字形分别同步 WebGL 与 Canvas；批次内按显示列表顺序混合，并保留各图元透明度。
- GPU 纹理按位图版本复用，颜色、滤镜或内容变更时重新上传；最近使用缓存限制为 64 MiB 或 1024 个纹理，避免长时间播放无限增长。
- 缓存分辨率随最终显示倍率在 1/64–8 倍间分档变化，改善小字形放大时的模糊，也避免把几千单位的字体轮廓当作原尺寸纹理上传；纯移动不重烘。单边位图上限 4096 像素，避免场外大图元占用过多内存。
- BlurFilter、GlowFilter 按各自参数签名创建，依次作用于叶子或整份容器位图；支持滤镜颜色、强度、横纵模糊、quality、inner/knockout。缓存扩边由滤镜确定。ColorTransform 的乘数和偏移进入实际像素运算。
- 盒式模糊参数表示完整核宽，半径为 `(blur - 1) / 2`，支持分数边缘权重；核宽不大于 1 时不模糊，最大为 255，每轮按通道固定精度取整。内发光先模糊 alpha 再取补集，透明缓存边缘也参与补集。
- Blur/Glow 使用可复用的 GPU 分离滤镜，双线性采样合并相邻样本，按核宽选择着色器循环上限；不通过 CSS 高斯模糊代替。GPU 不可用、着色器编译失败或缓存过大时完整退回 CPU。每份中间纹理限制为单边 4096、面积 8 Mi 像素，横长与竖长缓存交替时不会把历史最大边长拼成巨型纹理；二次幂扩容超预算时使用实际 NPOT 尺寸。原作 49 秒的 4096×1405 大字形不再每帧退回 CPU。
- DropShadow 的方向、距离、inner/knockout/hideObject，Bevel 的高光/阴影及 inner/outer/full，ColorMatrix 的 4×5 RGBA 变换和 Convolution 的核、divisor/bias、边缘及 alpha 参数进入绘制。所有滤镜工厂分别保存 Flash 构造器参数。
- scrollRect 按整数本地坐标滚动并裁剪自身内容和子树，透视子树裁剪投影窗口；脚本读取的 transform.matrix 不包含滚动偏移。深度恒定的平面直接用 Canvas 仿射绘制，与透视批次交替时保持半透明层序。
- 自绘位图的滤镜和颜色变换进入缓存，绘图 API 更新会触发缓存失效和重绘；损坏区域包含滤镜扩边，清空后不留下阴影。
- 显示对象 width/height 同步读取几何尺寸，包含子树和缩放，不包含滤镜扩边，供 Akari 的字距、居中与对齐使用。图片原始尺寸独立存储。
- drawPath 保留复合轮廓与 winding，二次/三次曲线真正绘制，WIDE 命令按四个数据消费。曲线极值参与尺寸计算，字形空洞不会被分轮廓填实。
- motion.startDelay 按原版的毫秒单位处理。容器自身 graphics 和嵌套兄弟的边界保留。
- 先清除变更区域，再按显示列表顺序重贴所有相交元件，避免移动时颠倒叠放顺序或累积半透明像素。互不相交的损坏区域保持分离。
- GradientGlow 和 GradientBevel 使用各自的 colors/alphas/ratios 色标表实际绘制，支持方向、距离、横纵模糊、quality、strength、inner/outer/full 和 knockout。Bevel 的 128 色标为基色，高光和阴影使用色标的两端；重复 ratio 不产生除零。空色标或 strength 为 0 时不改变输入。
- DisplacementMap 读取 BitmapData 的 RED/GREEN/BLUE/ALPHA 通道，以 `(channel - 128) * scale / 256` 选择源像素；支持 mapPoint、wrap/clamp/ignore/color 及填充色透明度。映射图外和未选通道不产生位移，分数位移在预乘 alpha 中插值。输入边界排除抗锯齿缓存边缘和后续滤镜预留的扩边，并包含此前滤镜的扩边；负坐标容器与栅格倍率变化均参与映射。
- BitmapData 的 fillRect/setPixel32/draw/dispose 会让引用它的位移滤镜缓存失效，并更新祖先容器。按原版 ScriptBitmap 的默认值填充 ARGB 白色；fillRect/setPixel32 替换原像素，避免写 alpha 通道时错误叠加旧像素。

## 验证

Node 桩测试验证生命周期、显示列表和真实脚本运行；`rendering-pixels.test.js` 使用本地 Edge 的真实 Canvas，验证 65 条颜色、滤镜、3D、排版、曲线与残影回归。其中覆盖 GPU/CPU 分数核宽、最大核宽、inner/knockout、颜色变换的一致性及着色器编译及滤镜 FBO 创建失败后备路径；新增三个滤镜的 14 项回归覆盖渐变端点/色标/透明度、半透明合成、容器扩边与擦除、位移四种边界模式、四个通道、mapPoint、缩放、负坐标字形及映射图修改后的缓存更新。另有 10 项覆盖独立/绑定投影、clone、视角/焦距联动、resize、参数复制、旧像素擦除和原样 Akari 摄像机的 GPU/Canvas 一致性和静态子树复用；窗口 680×502 到全屏 1360×768 的回归以原版焦距、中心实测值及深度矩形像素为依据，另覆盖移除根显式投影后的默认路径。它需要已有 Playwright 和 Edge，可通过 `NODE_PATH` 指向本机的 Playwright；`BROWSER_EXECUTABLE` 可以指定其他 Chromium。不会修改依赖清单或下载安装浏览器。

用户指定显卡时，可设 `$env:M8_GPU_DEVICE='P106'`。浏览器以 `--force-high-performance-gpu` 启动，再用 WebGL 的实际 renderer 核验设备名称；不匹配或无法创建 WebGL 就失败，不把 AMD 或软件后备算成 P106。该选项仅影响本次测试浏览器，不修改系统显卡偏好。明确禁用 WebGL 的回归用例仍验证 Canvas/CPU 后备。

```powershell
node tests/host/retained-mode.test.js
node tests/host/real-m8-scripts.test.js
node tests/host/tween-easing.test.js
node tests/host/rendering-pixels.test.js
node tests/host/rendering-performance.test.js
dotnet test tests/BiliBili.Tests/BiliBili.Tests.csproj --configuration Release
```

`av2669196-visual.test.js` 以固定随机种子、60 Hz 绝对帧时钟连续推进全部脚本，默认从 0 秒推进到 333 秒，抓取前段和 105–142 秒关键帧，不通过倍速或 seek 跳过中间状态。绝对帧时钟避免浮点累加使第 6600 帧落在 110000 ms 之前。设置 `M8_ARTIFACT_DIR` 保存 PNG 和亮度、非透明像素指标；`M8_BASE_REVISION=HEAD` 对照修改前；`M8_SAMPLE_SECONDS` 调整采样点；`M8_VIEWPORT_WIDTH/HEIGHT` 指定画布尺寸。333 秒要求画布完全透明，页面及桥接均无运行错误。原录屏中的随机动画与种子、播放器画布尺寸及采样时刻不同，亮像素比例不能独立证明逐像素一致。

`M8_START_SECOND` 可用于复核局部场景切换；非零起点的结果属于局部连续播放，不能替代默认从 0 秒开始的完整播放验证。

`M8_MEASURE_SECOND=49` 可在连续推进中统计 49–50 秒的 60 帧 WebGL 上传次数、RGBA 面积估算、绘制次数、Canvas 拷贝和缓存重绘数；随 `M8_ARTIFACT_DIR` 保存 `current-performance.json`。数据量按 `texImage2D` 的源尺寸乘 4 累计，不是实测 PCIe 带宽，也不包含浏览器自身的全部 GPU 操作。计数器只在这一窗口启用，结束时用 1 像素回读等待 Canvas 输出。

### 2026-10-01 GPU 占用优化

- 同值标量不再标脏；新建但参数相同的 Blur/Glow 数组复用内容缓存。参数快照独立于原对象，原地修改并重新赋值仍触发重绘。复杂滤镜仍保守失效，BitmapData 和矩阵变化不被忽略。
- 平面容器自身的移动/旋转与内容失效分开；孩子变化或栅格倍率改变才重烘。屏幕合成输出与本地平面缓存分开，修复根 3D 平面容器清空自身输入导致的空白。
- 大纹理在上传前按视口（批次含 256px 扩边）与近裁剪面做齐次裁剪，以 UV 边界裁剪源位图。保留原像素分辨率、2px 双线性采样边缘和 64px 分档，裁剪面积至少减少 25% 才复制；源版本或裁剪块变化才更新。小于 1 Mi 像素的源直接跳过 UV 裁剪；检测到内容连续更新时直接上传，稳定后恢复裁剪，避免动态场景多一次 Canvas 复制与跨上下文同步。动态判断使用合成帧序号，同一帧多次取样不会因墙钟推进而错误恢复裁剪。
- 投影纹理缓存预算为 128 MiB、最多 1024 项。49 秒的两张裁剪后纹理合计约 68 MiB，原 64 MiB 预算会让它们逐帧互相淘汰。预算上限增加 64 MiB，以减少反复上传；不是取消预算。原始离屏 Canvas 仍可能达到 4096×4096，本轮没有缩小全部内容缓存或测得浏览器实际显存用量。

P106-100、1280×720、种子 1，从 0 秒连续推进后的 49–50 秒 60 帧对照：原实现 719 次上传、RGBA 面积估算 15,842,778,920 字节、959 次 GPU 绘制、1320 次 Canvas 拷贝、540 次缓存重绘；裁剪和失效修正但仍用 64 MiB 时为 403 次上传、5,614,713,000 字节。128 MiB 预算对照为 7 次上传、34,848,848 字节、403 次绘制、127 次拷贝、66 次重绘，无页面或桥接错误。微基准经过脚本时钟推进、命令往返及同步，不能换算为 UWP 实时 FPS，也不能将上传量降幅称为 GPU 占用百分比降幅。尚未测得 Flash 原版在同条件下的占用。

`rendering-performance.test.js` 的 8 项真浏览器回归覆盖同值赋值、原地滤镜修改、复杂矩阵更新、平面缓存复用、内容更新、场外/近裁剪/resize、半透明窄条纹、两张总计超过 64 MiB 的静态纹理复用，以及动态位图跳过复制、帧内墙钟变化和稳定后恢复裁剪。条纹对照检查 alpha 覆盖率、主体颜色和整幅误差；低 alpha 边缘的反预乘舍入不要求 RGB 逐值相等。

全程对照发现，对所有图元都做 UV 裁剪及复制会引入额外开销：第一版 0–333 秒推进耗时 457.3 秒，仅关闭裁剪、保留其余修正时为 252.1 秒。限制裁剪到大纹理并跳过连续变化的内容后，同条件全程为 143.6 秒，49–50 秒仍为 7 次上传、34,848,848 字节；19,980 帧全部 11 条脚本无页面或桥接错误，333 秒画布完全透明。此耗时是手动帧时钟的测试总耗时，包含抓帧与同步，不能作为 UWP 页面实时帧率或 Flash 原版占用的比较。

最终改用稳定合成帧序号的版本，P106 全程推进耗时 138.6 秒，49–50 秒的计数保持上述结果，全部 11 条脚本无运行错误，180 和 333 秒画布完全透明。65 项像素、8 项性能、43 项宿主、5 项真实脚本及 265 项 C# 回归通过，Visual Studio MSBuild 的 Debug|x64 三目标编译通过。最终关键帧、计数和对比文件保存在本次可视化目录 `m8-performance-verified/`。尚未生成部署包或在 UWP 页面测得实际 GPU 占用降幅。

此前 GPU 续作验证结果：宿主 43 项、真实脚本 5 项、缓动 9 项、Edge 像素 41 项、C# 265 项均通过；Visual Studio MSBuild 的 Debug|x64 编译通过。

补齐三个滤镜后的验证：Edge 像素 55 项、宿主 43 项、真实脚本 5 项、缓动 9 项、C# 265 项及 Visual Studio MSBuild 的 Debug|x64 编译通过，该阶段未重跑 333 秒全程。

此前绑定投影按视口宽度计算的阶段，P106-100（ANGLE Direct3D11）上的 63 项像素回归全部通过，宿主 43 项、真实脚本 5 项、缓动 9 项、C# 265 项及 Visual Studio MSBuild 的 Debug|x64 三目标编译均通过。未生成或部署 UWP 测试包。

按原版实测固定 300 基准校准后，P106-100（ANGLE Direct3D11）上的 65 项像素回归全部通过，新增窗口/全屏用例也在禁用 WebGL 的 Canvas 后备路径通过。宿主 43 项、真实脚本 5 项、缓动 9 项、C# 265 项及 Visual Studio MSBuild 的 Debug|x64 三目标编译均通过。使用 JavaScript 双精度公式计算的焦距与原版输出相差约 0.000011，实测用例按小于 0.00002 的容差比较。

固定 300 基准后的完整连播也使用已核验的 P106-100：1280×720、种子 1、60 Hz，从 0 秒连续推进到 333 秒，共 19,980 帧，耗时约 318.0 秒。全部 11 条脚本无页面或桥接错误，105/110/115/119/121 秒关键帧均有画面，180 与 333 秒非透明像素为 0。保存了 0/2/10/40/105/110/115/119/121/142/180/333 秒 PNG 及指标 JSON，位于本次可视化输出目录的 `m8-projection-300/`。此为本地 Edge 连续推进验证，不是 UWP 页面实时帧率或原版逐像素一致性的证明。

此前按视口宽度计算时，使用已核验的 P106-100，在 1280×720、种子 1、60 Hz 下从 0 秒连续推进至 333 秒，共 19,980 帧，耗时约 310.2 秒。全部 11 条脚本无页面或桥接运行错误，105/110/115/119/121 秒关键帧有效，119 秒平均亮度约 158.02；180/240/300/332/333 秒非透明像素均为 0。此次记录指标，未输出 PNG。运行期间曾并行执行像素回归，且与此前 AMD 运行的代码不同，因此该耗时不作为两张显卡的性能对照或 UWP 实时帧率。

1280×720、种子 1、60 Hz 下从 0 秒连续推进至 333 秒，共 19,980 帧，耗时约 1005 秒。11 条脚本无页面或桥接运行错误，105/110/115/119/121 秒均有画面；119 秒平均亮度约 158。180/240/300/332/333 秒非透明像素均为 0，未残留末帧或滤镜扩边。滤镜 FBO 失败后备分支在独立像素测试中验证，连播走正常 GPU 路径。

本机 Edge/AMD Radeon R5 240 的 40 秒场景微基准，6 帧在等待 Canvas 像素回读完成后约 786 ms；优化前相同 GPU 路径约 3074 ms，CPU 路径约 6698 ms。此前约 24 ms 的数字只反映 GPU 指令提交，不能作为实际绘制速度。49 秒大字形场景修复前 6 帧约 22864 ms，修复后约 3733 ms，确认滤镜没有 CPU 像素回读。该微基准不是 UWP 实时帧率，复杂场景性能仍需改进。

110 秒参考图中的半径 160 圆属于 109575 ms 开始的入场；圆和日文字形各自独立调用 Math.random 选择方向。对 109.6–110.1 秒逐帧采样，固定中文区域在约 109.8–109.85 秒更接近标为 110 秒的参考图，提示录屏采样存在约 150–200 ms 偏移，但尚未以整段录屏确定统一偏移。保留脚本原始关键帧和随机逻辑，不为单张参考图硬编码位置或时间。

2026-10-01 用户在本地原版 Flash 中通过代码弹幕预览采集了窗口与播放器全屏参数，确认焦距与当前舞台尺寸独立。实测如下，两组弹幕层位置均为 (0,0)，scaleX/scaleY 均为 1，fieldOfView 均为 55°：

| 状态 | Player.time（ms） | stageWidth/Height | $.width/height | focalLength | projectionCenter |
|---|---:|---|---|---:|---|
| 窗口 | 2524 | 980×620 | 680×502 | 288.1473083496094 | (340,251) |
| 播放器全屏 | 5220 | 1360×768 | 1360×768 | 288.1473083496094 | (680,384) |

焦距对应 `300 / (2 * tan(55° / 2))`；300 现为该原版实例的实测投影宽度基准，不能称为运行时 stageWidth。投影中心取播放器宽高的一半。生产实现与默认后备路径均据此校准，独立投影的 500 基准保持不变。`$.stageWidth/Height` 在当前 WebView2 宿主仍返回其自身视口：窗口模式原版另有侧栏等舞台区域，不把原版的 980×620 硬编码到宿主 API。当前参考环境的参数已经确认；参考录屏的导出尺寸、时间和随机状态尚未完成对齐，因此参数校准不等于整段画面逐像素复刻。

仍需在 UWP 视频页验证实际 WebView2 的帧率、缩放、暂停恢复和跳转。GPU/CPU 回归一致性不能证明与 Flash 原生滤镜逐像素相同；Bevel 和本轮补齐的三个滤镜尚未进行 Flash 输出逐像素对照。GradientGlow、GradientBevel 和 DisplacementMap 当前走 CPU 像素处理，包含它们的滤镜链完整走 CPU；av2669196 没有使用这三种滤镜，因此其连续播放不能替代新增滤镜用例。含非共面 3D 子节点的容器滤镜仍在投影后的屏幕坐标计算，映射语义同样受此限制。无 WebGL 时使用分块 Canvas 近似投影。滤镜随元件缩放的处理仍依赖当前本地位图缓存模型，尚未完整复现 Flash 将舞台缩放与元件缩放分开处理的规则。没有以这些回归测试宣称完整 Flash 兼容。

## 参数依据

- [Adobe SWF 19 规范](https://open-flash.github.io/mirrors/swf-spec-19.pdf)：滤镜使用亚像素盒式模糊。
- [AIR DisplayObject.scrollRect](https://airsdk.dev/reference/actionscript/3.0/flash/display/DisplayObject.html#scrollRect)：本地滚动窗口及整数滚动语义。
- [AIR 默认透视投影](https://airsdk.dev/docs/development/display/working-in-three-dimensions/projecting-3d-objects-onto-a-2d-view)：根投影的舞台尺寸和新建投影对象的 500×500 默认尺寸需区分。
- 原版 `scripts/org/lala/plugins/CommentView.as:151` 将 `cplay.clip` 交给 ScriptDisplay；`scripts/org/lala/comments/CommentPlayer.as:230-231` 在 resize 中新建投影并设置弹幕层中心；`scripts/tv/bilibili/script/ScriptUtils.as:121-130` 通过注册类别名和 ByteArray/AMF 克隆对象。
- [Ruffle 投影测试](https://github.com/ruffle-rs/ruffle/tree/master/tests/tests/swfs/avm2/perspective_projection)的 Test.as 与 Flash 预期 output.txt：300 宽舞台中，独立对象焦距约 480.25，绑定元件的 getter 返回约 288.15；输入对象与绑定投影后续更新相互独立。[投影实现](https://github.com/ruffle-rs/ruffle/blob/master/core/src/avm2/globals/flash/geom/perspective_projection.rs)同样区分宽度来源。此为属性行为的交叉依据，不是本机 Flash 画面验证；该实现的原生 3D 绘制仍含 stub，不能作为图像一致性的证明。
- [Ruffle blur shader](https://github.com/ruffle-rs/ruffle/blob/master/render/wgpu/shaders/filter/blur.wgsl) 和 [blur 实现](https://github.com/ruffle-rs/ruffle/blob/master/render/wgpu/src/filters/blur.rs)：完整核宽、255 上限、边缘权重及逐轮取整的交叉核对。
- 用户提供的 `play_20181010_decompiled/scripts/tv/bilibili/script/ScriptDisplay.as:319-336`：三个工厂直接调用 Flash 同名滤镜构造器，保留其参数顺序及默认值；`scripts/ScriptBitmap.as:29-32`：BitmapData 默认填充 `0xffffffff`。
- [AIR GradientGlowFilter](https://airsdk.dev/reference/actionscript/3.0/flash/filters/GradientGlowFilter.html) 和 [GradientBevelFilter](https://airsdk.dev/reference/actionscript/3.0/flash/filters/GradientBevelFilter.html)：色标、基色、合成类型和 strength 为 0 的语义。
- [AIR DisplacementMapFilter](https://airsdk.dev/reference/actionscript/3.0/flash/filters/DisplacementMapFilter.html)：位移采样公式及四种边界模式；[Ruffle 位移着色器](https://github.com/ruffle-rs/ruffle/blob/master/render/wgpu/shaders/filter/displacement_map.wgsl)：通道编号及未选通道的中性值。上述实现依据与 Flash 原生输出逐像素对照是两种不同证据。
