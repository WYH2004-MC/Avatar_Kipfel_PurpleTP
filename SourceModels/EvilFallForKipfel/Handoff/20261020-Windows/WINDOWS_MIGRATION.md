# Windows 迁移与继续调试

归档日期：2026-10-20（Asia/Shanghai）。`Assets/`、`Packages/`、`ProjectSettings/`、`SourceModels/` 路径以工程根为基准；`evidence/`、`ram/`、`user-work/`、`logs/`、`tools/` 以本交接目录为基准。旧报告里的 macOS 绝对路径仅用于追溯。

## Git LFS 补充（2026-10-20）

两条适配/交接提交及紫色材质提交已随 `7a2c0428` 推送到 `origin/main`。随后将当前两个已跟踪 `.blend` 转入 Git LFS，模型的原始字节、SHA256 和权威关系保持不变。规则是 `*.blend filter=lfs diff=lfs merge=lfs -text`；采用追加提交，启用前普通 Git 中的历史大对象保留。

Windows 安装 Git LFS 后，克隆或更新仓库，再在工程根执行：

```powershell
git lfs install --local
git lfs pull
git lfs ls-files
```

应能看到 Accepted 源及 `ram/Blender_Unreviewed.blend` 两项，工作区应为实际 Blender 文件。若文件只有几行 `version https://git-lfs.github.com/spec/v1` 文本，先完成 `git lfs pull`，再运行交接哈希检查或打开 Blender。大历史候选仍受忽略规则保护，不会因新增 `*.blend` 规则而自动加入仓库。

已有 ZIP 和旁边 receipt 保留启用 LFS 前的归档基线；其中模型本身已是完整二进制。当前仓库的 `FILE_MANIFEST.json` 随 LFS 规则和本说明更新，旧 ZIP 内的清单仍只用于核对该 ZIP。

## 搬迁范围

建议 Windows 使用 `D:\Kipfel` 等短根目录。历史证据最长相对路径有 238 个字符，深层目录应使用支持长路径的复制工具；先核对清单，再清理旧设备。

完整搬迁应保留 `Assets/`、`Packages/`、`ProjectSettings/`、全部 `.meta` 及 `.git/`。本工程有嵌入式 SDK、Modular Avatar 等包，不能只复制 Packages 内的三个 manifest。若后续仍需改模型，还要保留权威 `.blend` 和本交接目录；约 46 GiB 的 `SourceModels` 中间历史不进入本轮 Git 提交，完整搬工程时可一并保留 `SourceModels/EvilFallForKipfel/WorkSessions/`。清单在 `evidence/source-history-inventory.json`。

Windows 第一次打开让 Unity 重新生成 `Library/`，不依赖 macOS 的 `Library/`、`Temp/` 或 `Obj/`。交接 ZIP 是文档、证据及工作副本归档，**不是完整 Unity 工程**；单独下载 ZIP 不能还原全部角色资产和依赖。

## 编辑器与依赖基线

使用 **Unity 2022.3.22f1（887be4894c44）**。以下是实际安装目录与 lock 核对后的版本：

| 包 | 当前版本 |
|---|---|
| VRChat avatars / base | 3.10.5 |
| Modular Avatar / NDMF | 1.17.1 / 1.14.4 |
| VRCFury | 1.1339.0 |
| lilToon / Poiyomi | 2.3.2 / 9.3.64 |
| Gesture Manager / Av3Emulator | 3.9.9 / 3.4.13 |
| Avatar Optimizer | 1.9.14 |
| FT Framework / BlendShare | 1.0.18 / 1.0.3 |

`Packages/vpm-manifest.json` 的 `dependencies` 包含较旧约束；以 `locked` 和已安装包为基线，**不要照旧 dependencies 降级**。先复现当前问题，再决定升级。UPM Git 包也应沿用 `packages-lock.json` 的已锁定提交。

## 文件权威与用户编辑

Unity 当前交付位于 `Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/`，继续使用原名 `EvilFall_Kipfel.prefab`、`EvilFall_Kipfel.fbx` 和 `InstallMenu.asset`，保持 GUID；不要另建 `_Fitted` 副本。

权威建模源是 `SourceModels/EvilFallForKipfel/Exports/20261020-Unity/EvilFall_Kipfel_AcceptedForUnity.blend`。本目录 `ram/Blender_Unreviewed.blend` 是迁移时额外保存的 Blender 内存状态，**尚未验收，不得直接覆盖已接受源或重新导出生产 FBX**。旧生产主文件和历史阶段也不能仅凭文件名当成最新成果。

本轮按用户要求分开本地提交：功能提交收录 EvilFall 资产与已接受 Blender 源；交接文档和证据随后另行提交，不 push。实际提交号查看交接主文档及 `git log`。

归档生成时两条提交尚未推送；后来已随 `7a2c0428` 推送，当前从远端克隆可取得它们。若旧设备还有新的本地提交或未提交编辑，仍需另外搬迁并核对。不要用远端分支覆盖本地未推送提交，也不要为消除脏状态执行重置或清理命令。

原有用户场景、材质等编辑没有默认合入功能提交；磁盘副本位于 `user-work/`，已跟踪文件差异位于 `logs/tracked-working-tree.patch`。仅通过 Git 迁移时，需要按需恢复这些文件；先比较目标文件，避免把 Windows 新编辑覆盖掉。完整复制带脏工作区的工程时则先检查是否已包含，勿重复应用补丁。

迁移前确认想保留的场景编辑已保存或已有独立副本。`user-work/Assets/MOCHIYAMA/Kipfel/Kipfel.unity` 是磁盘快照，不能据此声称 Unity 当前全部未保存内存修改已写回原场景。

较早的尝试文件、失败报告与中间模型用于追溯，不应自动回滚到这些状态。若后续需要重新修改眼镜、耳机或衣服，先从已接受源创建新的工作副本，保留本交接基线；每轮分别记录几何改动、预制件配置改动及实际测试结果，避免把静态检查通过等同于佩戴效果已验收。

## 新设备首轮操作

1. 将本交接文档与当前会话分享链接交给新 Codex；先读主交接、修改日志和 `evidence/current_state_audit.json`，核对文件哈希、GUID、Git 状态。
2. 保持原菜单和骨骼配置，使用指定 Unity 版本完成重导入，检查缺脚本、材质和包错误。
3. 在场景副本中测试衣服、眼镜、武器、鞋子的跟随；逐项验证 5 组 32 个菜单、互斥、默认值及重复开关。检查鞋子 8 个 OFF 形态键、原生猫耳/尾巴隐藏与恢复，以及运动时穿模。
4. 当前已完成静态绑定和保存回读；尚无本轮 Play、完整 NDMF build 或 VRChat 实机验收。记录实际失败的参数、对象路径和复现动作后再改。

`tools/` 的一次性修复与导出脚本保留了 `/Users/...`、`/tmp/...` 等旧绝对路径，只用于审计和参考。Windows 上须先阅读并映射工程根、核对触发条件，不能整批重放；不得为恢复路径而覆盖现有用户成果。
