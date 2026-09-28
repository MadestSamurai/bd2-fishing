# BD2 Fishing v0.4.3

## 简体中文

### 更新内容

- 修复点击「连接游戏」后窗口未响应的问题，连接等待期间仍可操作窗口。
- 改善停止和关闭响应，避免排队中的旧指令重新开启钓鱼。
- 增加连接过程日志；即使组件尚未就绪，也能通过「打开诊断目录」查看 `connection.log`。
- 支持新版工具在游戏运行中更新与切换，等待当前操作完成后交接。钓鱼和鱼类保留规则不变。

### 下载

两版功能相同，均为单 EXE，内置简体中文／English。

| 版本 | 运行环境 | 建议 |
| --- | --- | --- |
| **Portable** | 内置 .NET 运行时 | 首次使用推荐，下载即用 |
| **Lite** | 需安装 .NET Desktop Runtime 8 x64 | 已安装运行时，下载更小 |

下载一种版本即可。ZIP 附中英文说明和许可证；`SHA256SUMS.txt` 可用于核对下载文件。

### 升级

暂停并关闭旧工具，再打开新版连接。设置自动保留。从 0.4.1 及更早版本首次升级，需要正常重启游戏一次；从 0.4.2 或 0.4.3 测试包升级，游戏可以保持运行。若连接仍有异常，请提供诊断目录中的 `connection.log` 和 `compatibility.json`，并先移除个人信息。

## English

### Changes

- Fixed the window becoming unresponsive after clicking **Connect game**. The window remains usable while connecting.
- Improved stop and close responsiveness, preventing queued old commands from re-enabling fishing.
- Added connection logging from the beginning of each attempt. Use **Open diagnostics** to find `connection.log`, even if the component has not started.
- Updated tools can switch or update while the game stays open, handing over after pending actions finish. Fishing and fish-retention rules are unchanged.

### Downloads

Both editions have the same features, run as a single EXE and include Simplified Chinese / English.

| Edition | Runtime requirement | Recommended for |
| --- | --- | --- |
| **Portable** | .NET included | Most users; download and run |
| **Lite** | .NET Desktop Runtime 8 x64 required | Smaller download if the runtime is installed |

Download one edition. ZIPs include both READMEs and licenses. Use `SHA256SUMS.txt` to verify downloaded files.

### Upgrade

Pause and close the old assistant, then connect with the new version. Settings are retained. Upgrading from 0.4.1 or earlier requires one normal game restart; upgrading from 0.4.2 or the 0.4.3 test build does not. If connection still fails, share `connection.log` and `compatibility.json` from the diagnostics folder after removing personal information.
