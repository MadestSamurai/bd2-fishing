# BD2 Fishing · 独立自动钓鱼

[下载 Windows EXE](https://github.com/MadestSamurai/bd2-fishing/releases/latest) · [使用与兼容说明](docs/COMPATIBILITY.md) · [问题反馈](https://github.com/MadestSamurai/bd2-fishing/issues)

BrownDust II Windows 客户端的独立钓鱼辅助工具。通过游戏正常手动钓鱼入口完成当前钓点的连续钓鱼，包含蓄力、提竿、收线、长按、结算、自动用饵和满背包出售。不会调用游戏内自动钓鱼。

## 使用

1. 从 Releases 下载 `BD2Fishing-0.3.0-Portable-win-x64.exe`，或者下载包含使用说明和许可证的 ZIP。Portable 无需安装 .NET；Lite 需要 .NET Desktop Runtime 8 x64。两版都无需 Python、Visual Studio 或游戏开发 SDK。
2. 启动游戏，进入钓点并面向水面，关闭游戏内自动钓鱼。升级工具前需正常重启游戏，让旧的已注入组件退出。
3. 打开工具，点击 **连接游戏**。首次连接会读取本机客户端接口并自动生成适配组件，通常需要数秒。
4. 显示“钓点已识别”后点击 **开始钓鱼**。工具不会自行连接或开启自动化。
5. 点击 **停止钓鱼**或关闭窗口可停止。异常退出后控制租约最多 10 秒过期，输入会在游戏恢复帧时释放。

需要与游戏相同的权限。当前支持官方 Windows x64 Mono 客户端；不支持手机、模拟器内的 Android 客户端或 IL2CPP 客户端。

## 选择下载版本

| 版本 | 自带 .NET 运行库 | 运行要求 | 适用情况 |
| --- | --- | --- | --- |
| **Portable** | 是 | Windows x64，直接运行 EXE | 不确定电脑是否已安装运行库，建议选此版 |
| **Lite 精简版** | 否 | Windows x64，预先安装 **.NET Desktop Runtime 8 x64** | 已有运行库，下载体积更小 |

两版功能、跨版本 Hook 适配和设置完全一致，都提供 EXE 与含说明／许可证的 ZIP。Lite 需要的是 **Desktop Runtime**，只有 .NET Runtime 或 ASP.NET Runtime 不够；[微软 .NET 8 下载页](https://dotnet.microsoft.com/download/dotnet/8.0)选择 Windows x64 的 Desktop Runtime，建议安装最新 8.0 补丁。[微软单文件部署说明](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview)。

## 功能与设置

| 功能 | 默认行为 |
| --- | --- |
| 下一竿间隔 | 1000 ms，支持 0–60000 ms，运行中可修改 |
| 抛竿蓄力 | 90%，支持 5–95% |
| 收线与长按 | 按游戏帧读取真实判定区；优先弱点 |
| 背包满自动出售 | 默认开启；背包满后按保留选项出售鱼 |
| 保留全部传说鱼和锁定鱼 | 默认开启；关闭后，自动出售也会包含可售的传说鱼和锁定鱼。资料不明的鱼始终保留 |
| 自动鱼饵 | 增益到期后使用已有鱼饵一份；增益期间不重复使用，用完继续普通钓鱼 |
| 到期往返 | 默认开启；按游戏剩余时间，提前 5 分钟完成本竿后往返钓鱼大厅与原钓场，确认新倒计时再继续 |
| 昼夜更替 | 完成当前一竿和结算后让游戏正常切图，随后继续 |
| 网络确认 | 等待原始回执并回读库存／增益；超时或失败时暂停，不自动重发 |

不自动购买补给或解锁钓场。往返仅回到原钓场，返回位置由游戏正常入场逻辑安排。关闭往返开关或停止工具会取消尚未发出的返程。停止蓄力会走正常松开回调，可能完成已经开始的这一竿。

## 跨版本适配

发行包不包含游戏 DLL，也不锁定某个客户端 SHA 或 MVID。连接时使用内置接口指纹识别类型和成员；支持混淆名称变化、元数据重排和不影响目标接口的更新。随后用内置 Roslyn 编译器针对本机接口编译 Hook，用户无需重新打包。

如果钓鱼接口、数据结构或协议发生不兼容变化，工具会明确报出检查失败，停止继续连接或操作；不会通过猜测接口强行运行。**跨版本适配不等于保证兼容所有未来更新。** [兼容范围、验证证据与维护方法](docs/COMPATIBILITY.md)。

## 诊断

点击“打开诊断目录”查看 `%LOCALAPPDATA%\BD2Fishing`：

- `compatibility.json`：本次接口适配结果，改名类型／成员数量或失败原因。
- `runtime.json`、`latest.json`：组件状态和最新钓鱼快照。
- `runtime.log`：操作、回执、出售／用饵回读和切图诊断。

这些文件只保存在本机，不会自动上传。报告问题时请先检查文件中的个人信息。其他已注入的 BD2 工具无法随窗口关闭自动卸载；如提示模块冲突，关闭相关工具并正常重启游戏。

## 从源码构建

构建机要求 Windows x64、.NET 8 SDK（或兼容的更新 SDK）、可访问 NuGet。**不需要安装游戏**。

```powershell
git clone https://github.com/MadestSamurai/bd2-fishing.git
cd bd2-fishing
.\build.ps1 -Locked
.\package.ps1 -Locked
```

`build.ps1` 编译桌面程序并执行钓鱼状态机、出售／用饵保护、跨版本解析回归。`package.ps1` 再执行单 EXE 身份与 WPF 界面检查，输出 `dist/v版本号/` 中的 Portable／Lite EXE、ZIP、SHA256 校验文件和发行信息；不会连接游戏。

详见[维护与发布流程](docs/RELEASING.md)。提交到 main 自动检查；推送 `vX.Y.Z` 标签自动构建 GitHub Release。

## 许可

项目代码采用 [MIT](LICENSE)。SharpMonoInjector、Harmony、Mono.Cecil、Roslyn 和 .NET 的许可保留于 [第三方说明](THIRD_PARTY_NOTICES.md) 和 `licenses/`。项目与游戏官方无关联；仓库及发行包不提供游戏程序集、资源、账号信息或游戏通信凭据。
