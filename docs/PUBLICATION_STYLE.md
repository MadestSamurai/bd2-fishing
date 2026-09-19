# 文档与发布格式 / Publication style

四个独立助手使用同一套结构；功能内容仍以各仓库的实际行为为准。
The four standalone assistants share a structure, with tool-specific content describing actual behavior.

## 仓库简介 / Repository description

`Standalone <mode> assistant for BrownDust II on Windows. Chinese/English UI; Portable/Lite releases.`

使用产品名 BrownDust II；简介不罗列内部实现、测试数量或版本号。
Use BrownDust II consistently; keep implementation details, test counts and versions out of the short description.

## README

- H1：英文项目名；中文页追加中文工具名。H1 is the English project name, with a Chinese tool name on the Chinese page.
- 免责声明仅一次，紧接 H1；然后语言切换、下载／反馈链接与一句功能简介。One disclaimer directly below H1, then language links, download/issues links and a short overview.
- 中文章节：下载 → 快速开始 → 功能与设置 → 界面语言 → 兼容与限制 → 诊断与反馈 → 开发与贡献 → 许可。
- English sections: Download → Quick start → Features and settings → Language → Compatibility and limits → Diagnostics and feedback → Development and contributions → License.
- Portable／Lite 使用同一三列表格：版本／运行环境／建议；Edition / Runtime requirement / Recommended for.
- 中文和英文使用独立文件，功能、默认值、限制和链接保持对应。Keep behavior, defaults, limits and links aligned between both files.
- 保持 UTF-8 与正常换行；不对通用单词进行全局替换。Use UTF-8 and normal newlines; do not replace generic tokens globally.

## Release

标题：`BD2 <Tool> vX.Y.Z`。正文使用 [RELEASE_TEMPLATE.md](RELEASE_TEMPLATE.md)，先简体中文、后英文，每种语言依次列出更新内容／下载／升级。
Title: `BD2 <Tool> vX.Y.Z`. Use [RELEASE_TEMPLATE.md](RELEASE_TEMPLATE.md): Chinese first, then English; Changes, Downloads and Upgrade in each language.

只写本版本用户可感知的变化、下载选择和操作步骤。内部 Runtime 编号、源码状态标签、测试日志与指纹写维护文档，不放发布正文。
Describe user-visible changes, download choices and instructions. Keep internal Runtime IDs, implementation-status labels, test logs and fingerprints in maintenance documentation.

历史版本按当时真实功能说明：没有 Lite 或英文界面的版本不得补称已有；整理文字不替换资产、不移动标签。
Historical notes must reflect the original release: do not claim Lite or English existed before they were introduced. Formatting edits never replace assets or move tags.

发布前核对双语版本号、默认值、文件链接与免责声明位置；检查打包内 README，再核对 GitHub 渲染结果。
Before publishing, verify versions, defaults, links and disclaimer placement in both languages. Check bundled READMEs and GitHub rendering.
