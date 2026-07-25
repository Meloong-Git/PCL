# Plain Craft Launcher 2 完全开源版

本项目允许修改主题，与原版 PCL2 独立，并采用 MIT 开源协议。

[![Build](https://github.com/CortexaX/PCL/actions/workflows/build.yml/badge.svg)](https://github.com/CortexaX/PCL/actions/workflows/build.yml)
[![Pages](https://github.com/CortexaX/PCL/actions/workflows/pages.yml/badge.svg)](https://github.com/CortexaX/PCL/actions/workflows/pages.yml)
[![Release](https://img.shields.io/github/v/release/CortexaX/PCL?label=Release)](https://github.com/CortexaX/PCL/releases)
[![Stars](https://img.shields.io/github/stars/CortexaX/PCL?style=flat&label=Stars)](https://github.com/CortexaX/PCL)

<p align="center">
  <img src="docs/assets/pcl-cover.jpg" alt="Plain Craft Launcher 2 完全开源版" width="760">
</p>

这是一个第三方基于 Plain Craft Launcher 2 整理、恢复和维护的完全开源版本。项目目标是让 PCL2 的源码、构建流程和发布产物都保持公开、可审计、可复现，方便学习、研究、构建和二次开发。

项目网页：https://cortexax.github.io/PCL/

本仓库不是官方 PCL2 项目，不代表原作者或官方社区立场。Plain Craft Launcher、PCL、PCL2 等名称及相关权益归原作者与对应权利人所有。具体使用、分发和二次创作规则请以仓库中的 [LICENCE](LICENCE) 以及各第三方组件许可证为准。

## 当前状态

- 源码公开：包含启动器主体、UI、动画、下载、Minecraft 启动、资源管理、PCLCS 与 MeloongCore 等代码。
- 构建公开：使用 GitHub Actions 在 Windows 环境中执行 Release 构建，并上传构建产物。
- 发布公开：发布页提供可直接下载的构建包与校验信息。
- 本地恢复：已恢复主题解锁、主题调色逻辑与 Taowa/Terracotta 测试联机逻辑。
- 联机源码：Terracotta v0.4.2 源码已作为第三方源码包放入 [ThirdParty/Terracotta](ThirdParty/Terracotta)，PCL2 现在默认使用已移植到 VB/.NET 的内部联机实现，不再携带或启动 `terracotta.exe`。

## 版本分支

点下面的分支名可以直接跳转到对应源码快照：

- [`main`](https://github.com/CortexaX/PCL/tree/main)：当前继续维护的完全开源正式分支，最新正式发布为 [v2.13.0.1-cortexa.2](https://github.com/CortexaX/PCL/releases/tag/v2.13.0.1-cortexa.2)。这一版默认使用内部 VB/.NET Taowa 联机实现，构建产物不再包含 `terracotta.exe` 与 `VCRUNTIME140.DLL`。
- [`reference/v2.13.0.1-cortexa.1`](https://github.com/CortexaX/PCL/tree/reference/v2.13.0.1-cortexa.1)：旧正式版参考分支，对应 [v2.13.0.1-cortexa.1](https://github.com/CortexaX/PCL/releases/tag/v2.13.0.1-cortexa.1) 与提交 `d58a27b0e28704b132c3740590a6bad2b78c3eee`。这个分支保留给需要对照旧正式版恢复过程、行为差异或迁移改动的人参考。
- [`reference/v2.13.0.1-taowa-test.1`](https://github.com/CortexaX/PCL/tree/reference/v2.13.0.1-taowa-test.1)：Taowa 测试版参考分支，对应 [v2.13.0.1-taowa-test.1](https://github.com/CortexaX/PCL/releases/tag/v2.13.0.1-taowa-test.1) 与提交 `543ad27bc262651becda51029af29958908af3b1`。
- [`reference/v2.13.0.1-taowa-test.2`](https://github.com/CortexaX/PCL/tree/reference/v2.13.0.1-taowa-test.2)：Taowa 测试版 source.2 参考分支，对应 [v2.13.0.1-taowa-test.2](https://github.com/CortexaX/PCL/releases/tag/v2.13.0.1-taowa-test.2) 与提交 `d08a4f6ccb300e996c3195f3fe196b45bdf4594a`。这两个测试版分支用于对照原测试版联机恢复逻辑和后续内部化迁移差异。

## 构建

推荐使用 Windows 与 Visual Studio Build Tools / MSBuild 构建：

```powershell
git clone https://github.com/CortexaX/PCL.git
cd PCL
git submodule update --init --recursive
msbuild "Plain Craft Launcher 2.sln" /t:Restore /p:Configuration=Release /p:Platform="Any CPU"
msbuild "Plain Craft Launcher 2.sln" /m /p:Configuration=Release /p:Platform="Any CPU"
```

也可以直接查看 GitHub Actions 中的构建记录与发布产物：

- Actions: https://github.com/CortexaX/PCL/actions
- Releases: https://github.com/CortexaX/PCL/releases

## 致谢

感谢尹海航（龙腾猫跃 / LTCat）创建并长期维护 Plain Craft Launcher 2。PCL2 的界面、交互、启动流程、下载能力和大量工程实现都来自原作者与社区长期积累，本仓库的恢复与维护工作建立在这些基础之上。

感谢 PCL2 社区、问题反馈者、贡献者和文档维护者。没有这些长期沉淀，很多功能行为和边界都很难准确还原。

感谢这次恢复、构建和发布过程中使用过的工具与服务：

- Git、GitHub、GitHub CLI：用于版本管理、仓库上传、Release 发布与协作流程。
- GitHub Actions、MSBuild、Visual Studio Build Tools、.NET SDK：用于 Windows Release 构建与产物验证。
- ripgrep、Bash、Python zipfile：用于代码检索、脚本化检查和发布包整理。
- ILSpy 与 .NET 反编译/分析工具链：用于阅读既有构建产物并对照恢复缺失逻辑。
- Terracotta / EasyTier：为测试联机模式提供参考实现与底层联机能力；Terracotta 源码按 AGPL-3.0 随仓库公开，EasyTier 仍作为底层联机过渡资产随构建提供。
- OpenAI Codex / Codex CLI：用于辅助代码阅读、修改、验证、提交和发布流程。
- Newtonsoft.Json、NAudio、Ookii.Dialogs.Wpf、Imazen.WebP、CacheCow、ThrottleDebounce 等第三方库：为 PCL2 的数据处理、音频、对话框、图片、缓存和交互能力提供支持。

## 说明

这个仓库会优先保持源码透明、构建可复现和改动可追踪。后续如果继续做独立化改造，应尽量分批推进，并在每次改动后通过 GitHub Actions 验证构建结果。
