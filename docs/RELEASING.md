# 更新与发行检查

## 普通应用更新

- [ ] 修改 `Directory.Build.props` 的版本，并更新 README 下载文件名与发布说明。
- [ ] 升级 .NET 时同步检查桌面项目的 `RuntimeFrameworkVersion`、`Directory.Build.targets` 中链接器版本与依赖锁文件，避免不同 SDK 的隐式依赖漂移。
- [ ] 执行 `build.ps1 -Locked`，确认决策／保护与兼容测试通过。
- [ ] 执行 `package.ps1 -Locked`，确认单 EXE 和 WPF 离线检查通过。
- [ ] 如修改 Hook，针对合法安装的当前客户端运行 `compatibility-cli check` 与 `abi-probe`；可用旧版则一并验证。
- [ ] 确认提交清单仅含源码、接口契约、文档、依赖锁文件和许可证。禁止提交客户端 DLL、采集文件、诊断、连接状态或账号信息。
- [ ] 审阅 `docs/RELEASE_NOTES.md`，准确标注实机验证状态。
- [ ] 推送源码后再创建并推送 `vX.Y.Z` 标签。GitHub Actions 自动构建 EXE／ZIP、校验和及 Release；流程只使用 `contents: write` 发布权限。
- [ ] 下载 Release 资产核对 SHA256，检查 README、许可证及版本显示。

发布采用 [GitHub CLI 的 Release 命令](https://cli.github.com/manual/gh_release_create)。CI 不需要游戏安装、私有仓库、账号、服务器 SSH 密钥或游戏资源。

## 游戏客户端更新

正常更新先用**已发布 EXE**的连接或离线检查，不要因为 SHA/MVID 变化重新生成契约。

```powershell
# 不注入、不连接服务器，只检查元数据并编译内存组件。
BD2Fishing-0.2.0.exe --check-client "C:\YourGame\BrownDust II_Data\Managed" "compatibility-result.json"
```

维护者的详细检查：

```powershell
dotnet run --project compatibility-cli -c Release -- check "C:\YourGame\BrownDust II_Data\Managed" .build/client-check
dotnet run --project abi-probe -c Release -- "C:\YourGame\BrownDust II_Data\Managed" .build/client-check/BD2Fishing.Runtime6.dll
```

- [ ] 验证原生 UI 按下／松开、提竿、收线、长按入口。
- [ ] 验证状态名、血量／计时、针与判定区、特殊鱼技能。
- [ ] 验证排队切图与实际切图两个标志仍独立，结算可正常关闭。
- [ ] 验证六类原始回执仍唯一；不替换回调、不自行构造重发请求。
- [ ] 验证稀有度、锁定鱼、出售 DTO 和背包回读。
- [ ] 验证鱼饵表、具体库存、使用数量、增益和回执。
- [ ] 验证停止、租约失效、关闭窗口与模块冲突处理。

只有接口确认发生不兼容变动时，才审阅失败项并更新 Runtime 或维护者引导定义 `ContractGenerator.cs`。更新后用 `generate <Managed> <hook source dir> <contract.json>` 生成新契约，再对可用版本执行检查。契约只保存本功能所需的接口描述和不可逆指纹，不保存方法正文或游戏资源。

不要将“用新客户端重建了契约、再在同一客户端通过”当成跨版本验证；必须保留旧契约对新版本的检查结果，以及新契约对仍需支持版本的复核结果。
