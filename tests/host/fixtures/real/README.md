# 真实 M8 脚本夹具（全量 11 条）

来源：av2669196 的 mode=8「代码弹幕」脚本文本（当年 B 站 Flash 播放器的 M8 脚本语言），
共 11 条，**全部入库**（约 808 KB）。它们是 `real-m8-scripts.test.js` 的输入。

## 为什么必须全量入库

「只放出画面的两条」在仓库里跑不通，实测结论如下：

- `entry_10`（354 KB，主作品）的文字图层要从 `$G` 取 6 张字形表
  （`font.ume.p` / `font.msyahei` / `font.ume` / `font.segoe.black` /
  `font.shingo.heavy` / `font.consolas`），这些表由另外 7 条脚本注册，
  少任何一条都会在 `layer.update` 里读空对象而报错。
- `entry_08`（50 KB）是 Akari 库 + 骨架，**单独加载任何时间点都不落笔**。

所以「出画面」是 `entry_08`（库）+ `entry_10`（主作品）+ 6 条字体脚本共同的功劳。

## 三组对照（都是实测，不是推断）

| 加载 | 结果 |
|---|---|
| 全 11 条 | 0 runtime error；150s 处主画布 79 次 `drawImage` |
| 去掉 `entry_10` | 落笔 0 |
| 去掉 `entry_08` | `entry_10` 直接报错（Akari 库来自它） |

## 文件构成

| 文件 | 体积 | 内容 |
|---|---|---|
| `entry_00/01/02/03/04/05/06/07/09_*.js` | 768 B ~ 150 KB | 字体数据脚本：`$.toIntVector`/`toNumberVector` + `Global._set("font.*")`，只注册字形表、不出画面 |
| `entry_08_1147140491.js` | 50 KB | Akari 库（3D / 图层 / 文字排版框架）+ 骨架 |
| `entry_10_1147217315.js` | 354 KB | 主作品：靠 `entry_08` 的库 + 上面的字形表渲染 |

## 时间轴

`entry_10` 的图层 `inPoint` 在 87s 之后，所以测试把播放头大步推进到 150s 才断言落笔。
