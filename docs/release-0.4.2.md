# 0.4.2 本地候选 / Local candidate

此版本未发布到 GitHub；以下变更随 0.4.3 正式发布。
This candidate was not released on GitHub. Its changes ship with 0.4.3.

## 中文

- 更新与工具切换改用统一组件交接，常规更新无需重启游戏。
- 实时指令、心跳和快照使用本机命名管道；旧窗口失去控制后不能继续发出指令。
- 等待当前操作收尾后切换，配置、日志和采集记录继续保留。
- 从旧体系首次升级时，需要关闭旧工具并重启游戏一次。

| 版本 | 适用情况 |
| --- | --- |
| Portable | 包含 .NET Desktop Runtime，下载后直接运行 |
| Lite | 体积更小，需要 .NET Desktop Runtime 8 x64 |

两种版本均为单 EXE，内置中文和英文切换。

## English

- Unified component handoff supports routine updates and tool switches without restarting the game.
- Live commands, heartbeats and snapshots use local named pipes. A replaced controller cannot continue sending commands.
- Pending operations finish before handoff. Settings, logs and captured records are retained.
- The first migration from the old components requires closing the old tools and restarting the game once.

| Edition | Requirements |
| --- | --- |
| Portable | Includes .NET Desktop Runtime; ready to run |
| Lite | Smaller download; requires .NET Desktop Runtime 8 x64 |

Both editions are single EXEs with built-in Chinese and English switching.
