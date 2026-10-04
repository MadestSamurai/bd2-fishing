# BD2 Fishing v0.4.4

## 简体中文

### 更新内容

- 修复大背包自动出售只处理 100 条鱼就恢复钓鱼的问题。背包满后会连续分批整理，直到没有符合保留规则的可售鱼。
- 每批最多 100 条；收到成功回执并核对背包后才继续。每批重新检查最大／最小、锁定和资料保护规则。
- 整理期间显示本轮已确认出售数量和剩余可售数量，完成后显示本轮总数。停止或修改保留设置会取消后续出售。

### 下载

两版功能相同，均为单 EXE，内置简体中文／English。

| 版本 | 运行环境 | 建议 |
| --- | --- | --- |
| **Portable** | 内置 .NET 运行时 | 首次使用推荐，下载即用 |
| **Lite** | 需安装 .NET Desktop Runtime 8 x64 | 已安装运行时，下载更小 |

下载一种版本即可。ZIP 附中英文说明和许可证；`SHA256SUMS.txt` 可用于核对下载文件。

### 升级

暂停并关闭旧工具，再打开新版连接。设置自动保留。从 0.4.2 及之后版本升级时，游戏可以保持运行；从 0.4.1 及更早版本首次升级，需要正常重启游戏一次。

## English

### Changes

- Fixed automatic sales stopping after 100 fish in larger bags. A full bag now starts a cleanup that continues in batches until no eligible fish remain.
- Each batch contains at most 100 fish and is followed by reply and inventory verification. MIN/MAX, lock and unknown-data protections are rechecked before every batch.
- Cleanup status shows the confirmed total and remaining eligible fish, followed by the completed total. Stopping or changing retention settings cancels subsequent sales.

### Downloads

Both editions have the same features, run as a single EXE and include Simplified Chinese / English.

| Edition | Runtime requirement | Recommended for |
| --- | --- | --- |
| **Portable** | .NET included | Most users; download and run |
| **Lite** | .NET Desktop Runtime 8 x64 required | Smaller download if the runtime is installed |

Download one edition. ZIPs include both READMEs and licenses. Use `SHA256SUMS.txt` to verify downloaded files.

### Upgrade

Pause and close the old assistant, then connect with the new version. Settings are retained. The game can stay open when upgrading from 0.4.2 or later. Upgrading from 0.4.1 or earlier requires one normal game restart.
