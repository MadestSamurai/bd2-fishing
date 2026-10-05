# BD2 Fishing v0.4.5

## 简体中文

### 更新内容

- 修复网络波动后长期等待回执、开始和停止按钮不可用的问题；恢复期间保持状态更新，网络恢复后自动继续。
- 出售和解锁先核对当前背包再继续，结果未知时不重复消耗鱼饵；短暂切图不再取消整轮整理。
- 修复普通鱼的最大／最小尺寸保留及钓鱼期间背包摘要不更新的问题，改善设置保存和统一宿主连接。

### 下载

适用于 Windows x64。两版功能相同，均为单 EXE，内置简体中文／English。

| 版本 | 运行环境 | 建议 |
| --- | --- | --- |
| **Portable** | 内置 .NET 运行时 | 首次使用推荐，下载即用 |
| **Lite** | 需安装 .NET Desktop Runtime 8 x64 | 已安装运行时，下载更小 |

下载一种版本即可。ZIP 附中英文说明和许可证；SHA256SUMS.txt 可用于核对下载文件。

### 升级

暂停并关闭旧工具，再打开新版连接；本机设置保留。当前组件支持在游戏保持运行时更新和交接；如游戏本身仍显示断线或登录提示，请先恢复游戏连接。

[使用说明与风险声明](https://github.com/MadestSamurai/bd2-fishing/blob/main/README.md)

## English

### Changes

- Fixed recovery after missing network responses. Start and Stop remain usable, and automation resumes when the game recovers.
- Sales and unlocks reconcile the current inventory first; uncertain bait operations are not repeated. Temporary scene transitions preserve bag cleanup.
- Fixed per-species MIN/MAX retention for ordinary fish and live inventory summaries; improved settings saves and shared-host connections.

### Downloads

For Windows x64. Both editions have the same features, run as a single EXE and include Simplified Chinese / English.

| Edition | Runtime requirement | Recommended for |
| --- | --- | --- |
| **Portable** | .NET included | Most users; download and run |
| **Lite** | .NET Desktop Runtime 8 x64 required | Smaller download if the runtime is installed |

Download one edition. ZIPs include both READMEs and licenses. Use SHA256SUMS.txt to verify downloaded files.

### Upgrade

Pause and close the old assistant, then connect with the new version. Local settings are retained. Current components support updates and handoff while the game stays open. If the game itself is disconnected or asking you to log in, restore its connection first.

[Usage and risk disclaimer](https://github.com/MadestSamurai/bd2-fishing/blob/main/README.en.md)
