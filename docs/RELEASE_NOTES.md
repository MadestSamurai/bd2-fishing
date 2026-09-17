# BD2 Fishing 0.4.0

## 简体中文

- 内置英语界面：顶部随时切换简体中文／English，记住选择，运行中的钓鱼不中断；附独立英文 README。
- 合入 PR #2 的鱼种 MAX／MIN 保留设计，感谢贡献者 bupt-lxc。每种鱼可保留最大、最小或两者，并分别应用于传说鱼、锁定鱼。
- 传说鱼、锁定鱼、资料不明鱼改为独立保留开关，默认全部保留。尺寸规则与其他保留规则取并集。
- 关闭锁定鱼保留后，待售鱼先解锁，确认回执与背包后再出售；停止或修改规则会取消后续操作。已经解锁的鱼不会自动重新上锁。
- 保留 0.3.2 的自动靠岸修复。旧版“只保留上锁鱼”迁移为：不保留全部传说鱼，继续保护锁定鱼及资料不明鱼。

| 下载版本 | .NET 要求 | 界面语言 | 建议 |
| --- | --- | --- | --- |
| Portable · Windows x64 | 已内置 | 中文／English | 下载即用 |
| Lite · Windows x64 | 需要 .NET Desktop Runtime 8 x64 | 中文／English | 已安装运行时，下载更小 |

EXE 可直接运行；ZIP 附中英文说明和许可证。升级请关闭旧工具、正常重启游戏，再连接新版。

## English

- Built-in English UI with live Chinese/English switching and a separate English README. Your choice is saved without interrupting fishing.
- Includes per-species MAX/MIN retention from PR #2, contributed by bupt-lxc. Keep the largest, smallest, or both, separately for Legendary and locked fish.
- Independent Keep all Legendary, locked and unknown-data options, all enabled by default. Size rules add protection to these options.
- When locked-fish retention is disabled, selected fish are unlocked and verified before sale. Stopping or changing rules cancels subsequent actions; existing unlocks are not automatically reversed.
- Preserves automatic shoreline approach from 0.3.2 and migrates its locked-only preference without permitting locked-fish sales.

| Download | Runtime requirement | UI languages | Best for |
| --- | --- | --- | --- |
| Portable · Windows x64 | Included | Chinese / English | Download and run |
| Lite · Windows x64 | .NET Desktop Runtime 8 x64 | Chinese / English | Smaller download if the runtime is installed |

Run the EXE directly, or use the ZIP with both READMEs and licenses. Close the old tool and restart the game before connecting the updated version.