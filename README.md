## 哔哩哔哩第三方UWP客户端

### 介绍

fork自逍遥橙子大佬的项目: [xiaoyaocz/biliuwp](https://github.com/xiaoyaocz/biliuwp)(已停止维护)

<img width="1360" height="728" alt="image" src="https://github.com/user-attachments/assets/14cc261d-f31e-492e-8500-f4c664fb1c9e" />

### TODO

已完成所有功能的修复(应该)

### 特色功能

- 推荐页可隐藏竖屏视频
- 评论区支持屏蔽词(本地)和屏蔽只有@的评论
- 推荐页可按视频时长范围筛选

~~好像就这些了，以后想到再加吧~~

### 使用

1. 下载并双击 **`biliuwp-signing.cer`** → 安装证书 → 本地计算机 → "受信任的根证书颁发机构"
2. 开启开发者模式(Win10)：设置 → 更新和安全 → 开发者选项 → 开发人员模式
3. 双击下载的 .appx 安装，或 PowerShell执行：`Add-AppxPackage BiliBili.UWP_XXXXX_x86.appx`（x64 机器选 `..._x64.appx`）

步骤1和2仅首次安装需要，后续更新可直接下载 .appx 并安装

### 其他

目前使用AI进行日常维护与开发，版本[3.12.0](https://github.com/493505110/biliuwp/releases/tag/v3.12.0)是最后一个采用古法编程的版本
