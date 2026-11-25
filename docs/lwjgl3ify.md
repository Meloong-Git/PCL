# lwjgl3ify 支持

PCL 对 [lwjgl3ify](https://github.com/GTNewHorizons/lwjgl3ify) 的原生支持：1.7.10 的 Forge 实例装上该 Mod 后，可以直接用 Java 17 及以上版本启动。

## 背景

lwjgl3ify 让 1.7.10 的 Minecraft 通过 LWJGL3 运行，从而摆脱 Java 8 的限制。但它不是「丢进 mods 就能用」的 Mod，启动器必须配合完成四件事：

1. 把 LWJGL2 支持库换为 LWJGL3；
2. 追加 RetroFuturaBootstrap 前置支持库，并让它排在 classpath 最前面；
3. 把主类换成 `com.gtnewhorizons.retrofuturabootstrap.MainStartOnFirstThread`；
4. 追加 `-Djava.system.class.loader`、`--enable-native-access` 以及 34 组 `--add-opens`。

在此之前的 PCL 会把 1.7.10 Forge 版本的 Java 要求限制在 Java 8 以下，并按原版方式拼装 classpath，因此在 Java 17 下必然崩溃：

```
java.lang.ClassCastException: class jdk.internal.loader.ClassLoaders$AppClassLoader
cannot be cast to class java.net.URLClassLoader
    at net.minecraft.launchwrapper.Launch.<init>(Launch.java:34)
```

## 原理

lwjgl3ify 把完整的启动配置打包在自己的 Jar 中：

| 路径 | 内容 |
| --- | --- |
| `me/eigenraven/lwjgl3ify/relauncher/version.json` | 支持库列表、主类、JVM 参数 |
| `me/eigenraven/lwjgl3ify/relauncher/forgePatches.zip` | 前置支持库（RFB 与 Forge 补丁类） |

因此实现上不写死任何版本号，而是读取用户实际安装的那个 Mod 的元数据来生成启动配置。这样启动配置与 Mod 版本永远匹配，也无需额外联网。

## 实现

新增 [`Plain Craft Launcher 2/Modules/Minecraft/ModLwjgl3ify.vb`](../Plain%20Craft%20Launcher%202/Modules/Minecraft/ModLwjgl3ify.vb)：

| 能力 | 说明 |
| --- | --- |
| 识别 | 扫描 `mods` 文件夹中的 `lwjgl3ify-*.jar`，仅对 1.7.10 + Forge 生效，结果按目录时间戳缓存 |
| 支持库替换 | 以 Mod 元数据的 81 项支持库为准替换版本原有列表；按平台过滤掉其他系统的 LWJGL3 natives（全平台约 142 MB，Windows 约 60 MB）；保留版本自己额外安装的自定义库 |
| 前置处理 | `lwjgl3ify:<版本>:forgePatches` 置于 classpath 首位，并直接从 Mod 的 Jar 中提取（校验和与 Maven 上的一致），省去一次 7.9 MB 下载 |
| 主类 | 替换为 `com.gtnewhorizons.retrofuturabootstrap.MainStartOnFirstThread` |
| JVM 参数 | 注入元数据中的 72 项参数，跳过由 PCL 负责的 `-cp`、`${classpath}`、`-Djava.library.path` |
| Java 要求 | 调整为 Java 17 及以上，并跳过 Forge / OptiFine / LiteLoader 原有的低版本限制 |
| 兼容处理 | 该模式下禁用 JLW 与 LUA（与替换后的系统类加载器冲突），日志编码固定 UTF-8 |
| 可回退 | 选中的 Java 低于 17 时撤销补丁，退回 lwjgl3ify 自带的重启机制（Relauncher） |
| 环境检查 | Forge 版本与预期不一致、缺少 UniMixins 时给出日志或提示 |

补丁只作用于内存中的版本 JSON，启动结束或下次启动前会还原，不会写脏实例文件。

集成点：

| 文件 | 改动 |
| --- | --- |
| [`ModJava.vb`](../Plain%20Craft%20Launcher%202/Modules/Minecraft/ModJava.vb) | `GetJavaRequirement` 增加 lwjgl3ify 分支 |
| [`ModLaunch.vb`](../Plain%20Craft%20Launcher%202/Modules/Minecraft/ModLaunch.vb) | 预检测阶段应用补丁、Java 版本不足时撤销、注入 JVM 参数、禁用 JLW/LUA、修正编码 |
| `Plain Craft Launcher 2.vbproj` | 注册新文件 |

## 启动顺序

补丁**必须在补全文件之前**应用，否则启动器会按原始的 LWJGL2 支持库判断缺失文件，导致后续 classpath 中的 LWJGL3、log4j 等文件根本没有被下载。

```
预检测（应用 lwjgl3ify 补丁）
  → 选择 Java（低于 17 则撤销补丁）
  → 补全文件（按打完补丁的支持库列表下载）
  → 生成参数（注入 JVM 参数）
  → 解压 natives → 启动进程
```

## 已修复的问题

| 问题 | 原因 | 修复 |
| --- | --- | --- |
| 补丁从未生效，Java 17 下仍在 `launchwrapper` 崩溃 | PCL 会把 `1.7.10` 归一化为 `Version(7, 0, 10)`，版本判断写成了 `1.7.10` | 改为与 `7.0.10` 比较 |
| `找不到主类 java.base/java.lang.invoke=ALL-UNNAMED` | 参数去重按「值」进行，把重复出现的 34 个 `--add-opens` 键删成了 1 个 | 不再按值去重，交由 PCL 的成对去重处理 |
| GTNH 等实例报 `NoClassDefFoundError: org/apache/logging/log4j/LogManager` | `获取 Java` 是并行 loader，补全文件在补丁之前完成，缺少的 log4j 等文件未被下载 | 把补丁提前到预检测阶段 |

## 验证

- CI（Debug / Release）构建通过；
- 用真实 1.7.10 实例拼出补丁后的启动命令，Java 21 与 Java 27 均进入主界面：`LWJGL Version: 3.4.3+4`，UniMixins、GTNH Lib、lwjgl3ify 正常加载。

## 使用说明

1. 在 1.7.10 Forge 实例的 `mods` 文件夹中放入 `lwjgl3ify-*.jar` 与 UniMixins；
2. Java 选择「自动选择」即可（会挑选 Java 17+ 中的最高版本），也可以手动指定 17 / 21；
3. 首次启动会下载约 60 MB 的 LWJGL3 支持库，之后可离线启动；
4. 建议 Forge 版本为 10.13.4.1614，与其他版本混用可能无法启动。

## 提交记录

```
6b83141 feat(launch): 支持 lwjgl3ify，1.7.10 Forge 实例可直接用 Java 17+ 启动
9a36d67 style: 移除 lwjgl3ify 支持代码中新增的注释
4503f0d fix(launch): 修正 lwjgl3ify 的版本识别，1.7.10 应比较 7.0.10
9fdd143 fix(launch): 保留 lwjgl3ify 启动参数中重复的 --add-opens 键
537163e style: 移除 lwjgl3ify 模块中的 #Region 指令
5e80732 fix(launch): 在补全文件前应用 lwjgl3ify 启动配置
```
