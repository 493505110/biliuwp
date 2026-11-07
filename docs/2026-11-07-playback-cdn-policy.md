# 播放与下载的 PCDN/MCDN 筛选

播放设置新增「屏蔽 PCDN/MCDN」，默认关闭。开启后，在接口返回的主地址和备用地址中选择未命中规则的 HTTP(S) 地址，保留原字符串中的路径、查询参数及转义，不替换域名。

覆盖普通视频、番剧的 DASH（系统与 FFmpeg 软解）、旧式分段流、直播候选线路和新建下载。DASH 音视频分别筛选；有音轨但没有合规音频地址时，不能退为无声视频。旧式分段流及下载任意一段无合规地址时，整次获取失败，不能丢弃该段。策略错误保留明确提示，停止 API/格式回退。

## 规则

规则集中在 `Modules/Playback/PlaybackCdnPolicy.cs`，按解析后的 host、路径和独立查询参数判定：

- `mcdn.bilivideo.com/cn/net`、`szbdyd.com`、`mountaintoys.cn`、`nexusedgeio.com`、`ahdohpiechei.com` 本域与子域。
- `bilivideo.com/cn/net` 主机首段中的 `302ppio`、`302kodo`，以及 `upos-sz-mirror14b.bilivideo.com`。
- IP 直连的 `/v数字/resource` 资源接口。
- 查询参数 `os=mcdn`，兼容大小写与百分号编码。

域名特征参考 [wiliwili 的 CDN 策略实现提案](https://github.com/xfangfang/wiliwili/pull/666)，mountaintoys 示例见 [原脚本规则讨论](https://github.com/SukkaW/Make-Bilibili-Great-Than-Ever-Before/issues/26)。这些是社区观察到的节点特征，不是 B 站公布的完整归属清单。

常规地区 CDN、云厂商镜像和未命中规则的其他地址仍可使用。本功能筛选已知 PCDN/MCDN，不保证只走 B 站自建节点。开关对重新获取的地址生效，不中断已有播放或系统后台下载；没有拦截 HTTP 重定向或解析 HLS 清单中的子请求。

## 验证

自动测试覆盖域名边界、大小写、IP 资源接口、查询参数误命中、签名原样保留、主备切换、全屏蔽失败及关闭开关兼容性；直播另外覆盖 FLV/HLS 回退、清晰度选择和 DRM 提示区分。

页面与媒体引擎仍需在 Visual Studio 中 F5 验证：

1. 开关关闭时验证普通视频、番剧、直播和下载。
2. 开启后重新获取含 PCDN 主地址、常规备用地址的源，核对音视频实际使用的主机。
3. 对仅有 PCDN 的响应，核对明确错误提示，没有回退连接被屏蔽地址。
4. 核对 DASH 有音轨但全被屏蔽、旧式多段视频中间一段被屏蔽、直播所有线路被屏蔽时的提示。
