# BD2 Fishing v0.4.1

## 简体中文

### 更新内容

- 主窗口增加免费开源署名：GitHub MadestSamurai／B站 MadSamurai。
- 新增「来源与说明」，可查看并复制官方仓库与下载链接；随界面切换中英文。
- 统一双语 README、来源与风险说明，ZIP 附带完整说明；MIT 许可证保持不变。

### 下载

| 版本 | 运行环境 | 建议 |
| --- | --- | --- |
| **Portable** | 自带 .NET，无需另装运行库 | 大多数用户 |
| **Lite** | 需要 .NET Desktop Runtime 8 x64 | 已安装桌面运行库、希望减小下载体积 |

两版功能相同，内置简体中文／English。EXE 可独立使用；ZIP 附带双语说明与许可证。用 `SHA256SUMS.txt` 核对下载。

### 升级

停止自动操作并关闭旧工具，再打开新版。已有设置保留；本次主要更新来源与说明界面。

作者发布版免费。第三方收费不代表作者参与、背书或提供服务。[使用说明与风险提示](https://github.com/MadestSamurai/bd2-fishing/blob/main/README.md)。

## English

### Changes

- Adds free-release attribution to the main window: GitHub MadestSamurai / Bilibili MadSamurai.
- Adds About & source with selectable official repository and download links, following the selected UI language.
- Standardizes bilingual READMEs and source/risk notices, also included in ZIPs. The MIT License is unchanged.

### Downloads

| Build | Runtime | Recommended for |
| --- | --- | --- |
| **Portable** | Includes .NET; no separate runtime needed | Most users |
| **Lite** | Requires .NET Desktop Runtime 8 x64 | Smaller download when the desktop runtime is installed |

Both builds have identical features and include Simplified Chinese / English. EXEs run independently; ZIPs include bilingual documentation and licenses. Verify downloads with `SHA256SUMS.txt`.

### Upgrade

Stop automation and close the old tool, then open the new version. Existing settings are retained; this update primarily changes attribution and source information.

Official releases are free. Third-party fees do not imply the author's involvement, endorsement or support. [Usage and risk notice](https://github.com/MadestSamurai/bd2-fishing/blob/main/README.en.md).

---

# BD2 Fishing v0.4.0

## 简体中文

### 更新内容

- 内置英语界面：顶部随时切换简体中文／English，记住选择，运行中的钓鱼不中断；附独立英文 README。
- 合入 PR #2 的鱼种 MAX／MIN 保留设计，感谢贡献者 bupt-lxc。每种鱼可保留最大、最小或两者，并分别应用于传说鱼、锁定鱼。
- 传说鱼、锁定鱼、资料不明鱼改为独立保留开关，默认全部保留。尺寸规则与其他保留规则取并集。
- 关闭锁定鱼保留后，待售鱼先解锁，确认回执与背包后再出售；停止或修改规则会取消后续操作。已经解锁的鱼不会自动重新上锁。
- 保留 0.3.2 的自动靠岸修复。旧版“只保留上锁鱼”迁移为：不保留全部传说鱼，继续保护锁定鱼及资料不明鱼。

### 下载

| 版本 | 运行环境 | 建议 |
| --- | --- | --- |
| **Portable** | 内置 .NET 运行时 | 首次使用推荐，下载即用 |
| **Lite** | 需安装 [.NET Desktop Runtime 8 x64](https://dotnet.microsoft.com/download/dotnet/8.0) | 已安装运行时，下载更小 |

适用于 Windows x64。EXE 可独立运行，ZIP 附说明与许可证；使用 `SHA256SUMS.txt` 校验下载。两版功能相同，均内置简体中文／English。

### 升级

暂停并关闭旧工具，正常重启游戏，再打开新版连接。本机设置保留；具体功能与设置迁移见上面的更新内容。

[使用说明与风险声明](https://github.com/MadestSamurai/bd2-fishing/blob/main/README.md)

## English

### Changes

- Built-in English UI with live Chinese/English switching and a separate English README. Your choice is saved without interrupting fishing.
- Includes per-species MAX/MIN retention from PR #2, contributed by bupt-lxc. Keep the largest, smallest, or both, separately for Legendary and locked fish.
- Independent Keep all Legendary, locked and unknown-data options, all enabled by default. Size rules add protection to these options.
- When locked-fish retention is disabled, selected fish are unlocked and verified before sale. Stopping or changing rules cancels subsequent actions; existing unlocks are not automatically reversed.
- Preserves automatic shoreline approach from 0.3.2 and migrates its locked-only preference without permitting locked-fish sales.

### Downloads

| Edition | Runtime requirement | Recommended for |
| --- | --- | --- |
| **Portable** | .NET included | Most users; download and run |
| **Lite** | [.NET Desktop Runtime 8 x64](https://dotnet.microsoft.com/download/dotnet/8.0) | Smaller download if the runtime is installed |

For Windows x64. EXEs run on their own; ZIPs include documentation and licenses. Verify downloads against `SHA256SUMS.txt`. Both editions have the same features and include Simplified Chinese / English.

### Upgrade

Pause and close the old assistant, restart the game normally, then connect with the new version. Local preferences are retained; see Changes above for feature and setting migrations.

[Usage and risk disclaimer](https://github.com/MadestSamurai/bd2-fishing/blob/main/README.en.md)
