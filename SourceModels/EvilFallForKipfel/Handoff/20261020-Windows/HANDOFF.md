# EvilFall → Kipfel：Windows 调试交接

交接基线：2026-10-20，Asia/Shanghai。目标是把当前适配结果、修改依据和待测事项交给 Windows 上的 Codex，继续在已有成果上调试。入口提示见 [START_HERE.txt](START_HERE.txt)，修改经过见 [CHANGELOG.md](CHANGELOG.md)，迁移步骤见 [WINDOWS_MIGRATION.md](WINDOWS_MIGRATION.md)。会话分享链接由用户另外提供。

## 当前结论

当前几何已保存并导入原位 `ForKipfel` 资产；衣服、眼镜、武器、鞋子的 Unity 跟随关系已补修，原版菜单的 5 组 32 个功能已恢复。最新预制件经过原生保存回读、动画路径和骨路由检查；**尚未完成本轮 Unity Play、完整 NDMF/Avatar 构建或 VRChat 实机验证**。Windows 的第一步是复现和测试，不是重新生成适配模型。

此前导出遗漏了 Blender 约束到 Unity 的合并关系，用户实测出现服装仍停在 T Pose、眼镜/武器/鞋子不跟随；这部分已在导出后的预制件中修复。修复后的运行效果仍待新设备实测。旧截图中的 Console 异常也没有充分证据证明已修复，遇到时保留完整堆栈再定位。

## 唯一继续入口

以下路径从工程根目录计算。实际目录拼写是 **ForKipfel**；用户消息中的 ForFipfel/forkipfel 指的是同一目录。

| 用途 | 工程内路径 |
|---|---|
| Unity 预制件 | `Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel.prefab` |
| 已适配模型 | `Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel.fbx` |
| 可继续编辑的完整 Blender 源 | `SourceModels/EvilFallForKipfel/Exports/20261020-Unity/EvilFall_Kipfel_AcceptedForUnity.blend` |
| 菜单入口 | `Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/InstallMenu.asset` |
| 当前主 FX | `Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/Accessories.controller` |
| 场景磁盘位置 | `Assets/MOCHIYAMA/Kipfel/Kipfel.unity` |

预制件 GUID：`4d5300ec6aad44f359dfd5df356ab737`；FBX GUID：`02362ac3b72a643bca5074e35b3eec18`。保留同名原位资产与 `.meta`，用户明确要求覆盖已有交付，不保留第二套 `_Fitted` 预制件。

SHA256 基线：

```text
prefab  b5cc9d70fca94a900696c18aa3a625b52da24ccb7137a007f426a4387b6dfa27
fbx     57894c81e2d7ef1976c282e563da7ab38c7446d850536edd037e3325b0c89602
blend   bf43363dd6c81e47ea5b6aaf4523d6be0f5a383f082264d92dab390f9fea20ef
```

`WorkSessions/20261013-skin-contact-fit/FinalEarphoneCableHairClearance/` 下最终 `.blend` 与 Accepted 副本逐字节相同。老的 `SourceModels/EvilFallForKipfel/EvilFall_Kipfel.blend` 是旧 production，不能作为最新入口。早期 `FinalGlassArcExtensionFit` 曾写完成，后来被用户指出穿发；应以 `FinalGlassSideHairClearance` 及其后续完整保存点为准。

## 已完成内容与必须保留的设计

模型阶段已累计包含面罩轮廓、同轴内管、外颈管统一管径与平顺走线、前领插座、衣边尖褶、MainCoat 网格与肩袖、眼镜及有线耳机适配。各阶段接受/否决方案见修改日志和 `evidence/BlenderStages/`。完整中间历史约 46 GiB，保留在原设备并列入清单，本轮不把所有候选加入 Git 或精简 ZIP。

眼镜前镜保持原来的连续弧面，可以沿左右延长接到侧框；不能把两翼掰成折角。侧框采用用户指定的小尺寸，允许藏在头发下面，但不能与发面相交。前组移动与后环应分开处理；后环适配大头，不能跟随前组 Y 一起误移。

耳机球置于耳内，允许轻微嵌入体现佩戴，不得从耳背冒出。线接球体真实圆孔，参考原版 FBX 的走线逻辑；接口圈紧凑集中在接头处，不拆散到整根线上。最终采用宽缓空间圆弧绕开发面，保留球、孔内接头和管径。后续换发型或动作仍需重新检查。

服装修复参考原版 FBX，保留独立形态键、权重及控制含义。MainCoat 的肩套包覆手套固定银环/Shield，四条黑粉饰框完整连续，不能只削孔或裁成细条来藏问题。明确设计的插座半嵌和内层装配接触仍保留；不能笼统声称全模型任意状态均零交线。

`FaceMaskV2` 的几何适配此前暂停；本次恢复了它的原版控制功能，没有新增外观验收。

## Unity 骨骼与原版功能恢复

FBX 导出为 49 对象、38 网格、9 骨架，共 431 骨。Unity 内公开适配骨架为 `armature`（原 Fit212），原服装源骨架重命名 `EvilFall_SourceRig`（原 Source166）。预制件补齐 Source→Fit、武器→Fit、面部支链和对象跟随。最新检查为 181 条原版跟随路由匹配、38 个 SMR 骨引用有效。

最新状态有 **30 个 MA MergeArmature：29 个内部直接引用、1 个外部 Avatar `armature` 引用**。较早 RigBindingFix 报告中的 28 个是菜单恢复前的历史数值，不要用旧脚本覆盖当前 30 个。名字映射保留 `Foot.L/R → foot.L/R`、`MiddleProximal → MiddleProxima`；不要擅自纠正原资源拼写导致绑定失效。

原版功能对照源：

- `Assets/YaokiSubWork/EvilFallFullSet/ForLime&Chiffin/prefabe/Lime_EvilFall S breast.prefab`
- `Assets/YaokiSubWork/EvilFallFullSet/FX&Animation/EvilFallFullSetAnImation Lime.controller`
- `Assets/YaokiSubWork/EvilFallFullSet/FX&Animation/MenuIco/EvilFallMenu.asset` 及其子菜单

原版菜单恢复结果：

| 当前资产 | 作用 |
|---|---|
| InstallMenu | EvilFall 安装入口 |
| AccessoriesMenu | 原版五组入口 |
| ClothingMenu | Outfit，8 项 |
| FaceMenu | Face，7 项 |
| WeaponMenu | Weapon，4 项 |
| R18ToyMenu | 5 项 |
| PlugToyMenu | 8 项 |

总共 7 菜单、32 Toggle、32 Bool；参数前缀 `EvilFallKipfel/`，保留原名称、图标、顺序、默认值、保存设置与互斥语义。原简化版 FaceMode/Weapon 整数控制不再是当前活动方案。只有根 MA MergeAnimator 合并 `Accessories.controller`；旧 `Clothing.controller` 保留为资产，不应再次合并。

当前主 FX 有 26 层、60 状态、67 跃迁、77 条条件和 12 个互斥 ParameterDriver；59 个适配动画位于 `OriginalAnimations/`。1178 条动画绑定及 29 条形态绑定有效。另恢复 20 个粒子系统、共 19 个 PhysBone 和 2 个 Blocker。原有 16 个 PhysBone 序列化块以及 3 个 ShapeChanger、2 个 ObjectToggle 块未改。

必须保留的穿戴反应：

- `MainLegHeel` 开启：`Body_Base` 的 `Knee_OFF_left/right`、`Foot_OFF_left/right`、`Toe_OFF_left/right`、`Lower_leg_01_OFF`、`Lower_leg_02_OFF` 共 8 项设为 100。
- `HeadACC` 开启：原生 `Cat_Ear` 关闭。
- `EvilFallTail` 开启：原生 `Cat_Tail` 关闭。

这些使用 Avatar 相对引用。Windows 必须验证关闭后恢复以及反复切换；静态组件存在并不等于运行反应已测。

## 验证证据与限制

最新 Unity 完成报告为 [MenuRestore/COMPLETION.json](evidence/MenuRestore/COMPLETION.json)，保存回读和独立检查报告也在该目录；此次重新核对其 68 个交付文件哈希全部一致。`evidence/RigBindingFix/` 是此前跟随补修的历史证据。报告中的旧 macOS 绝对路径用于追溯，文件副本从本交接目录读取。

Blender 已做实际多角度观察、保存重开及有限形态/姿态检查；最终眼镜检查包括 Basis/Wider/Narror 静态端点，耳线检查当前五件头发与眼镜端点。不是任意姿态/任意发型的证明。Unity 原生保存回读一致，默认 BakeMesh 最大世界误差约 `3.63e-7 m`；未运行本轮 Play、完整 NDMF build 或 VRChat。

优先待测：

1. 当前预制件装到目标 Avatar 后，抬臂/屈肘、转头、持武器、屈膝与足部动作时是否正确跟随。
2. 五组全部 32 项控制的默认值、保存、互斥与多次切换；武器 Off/Blade/Gun/Knife 和独立面罩功能。
3. 鞋子 8 个隐藏键，以及猫耳/原生尾巴的隐藏与恢复。
4. 当前发型下眼镜、耳线、衣边的动态穿模；粒子、PhysBone、Blocker。
5. 再运行完整 NDMF/VRChat 构建；遇到异常记录参数、对象路径、动作和完整堆栈，分清新问题与旧 Console 记录。

## 未保存编辑、用户工作与归档范围

Blender 交接时 `is_dirty=true`，已使用原生 save-as-copy 额外保存 [ram/Blender_Unreviewed.blend](ram/Blender_Unreviewed.blend)，没有改变当前原文件路径或清除 dirty 标志。这是**未复核内存副本**，与已接受源分开保留，未来先比较再决定采用，不能直接重导覆盖交付。

Unity 当前 UI 状态读取超时。场景内存副本没有触发保存；因此不保证 Unity 尚未保存的场景或 Prefab Stage 编辑已包含。只保留了磁盘场景快照。若旧设备 Unity 恢复响应，应在退出前自行确认未保存修改；完整情况见 `evidence/unity_ram_capture.json`。临时捕获脚本已从 Assets 删除，仅留审计源，且未创建触发请求。

本轮原有脏工作区没有重置或混入功能提交。14 个磁盘文件副本在 `user-work/`，包含场景、层级设置、材质、贴图 meta 和新紫色发材质；列表及 SHA 在 `evidence/user-work-snapshot.json`。已跟踪差异另存 `logs/tracked-working-tree.patch`。原 `ProjectSettings` 在早期状态列表出现过变化，归档前已回到 HEAD，无对应差异副本；初始列表仅作当时状态。

精简 ZIP 包含交接资料、Accepted 源、本轮 EvilFall 资源和这组用户工作副本；**不是完整 Unity 工程**，不含全部角色资产、完整 Packages 或 46 GiB 历史。迁移完整工程请按 WINDOWS_MIGRATION 保留 Assets、Packages、ProjectSettings、所有 meta 和本地 Git。不要在新设备盲目执行 patch 或覆盖新编辑。

归档生成时，Git 提交分为适配成果、交接资料两条本地提交，尚未 push。随后这两条及紫色材质提交已随 `7a2c0428` 推送到 `origin/main`，当前从远端 clone 可取得。功能提交号记录于 `logs/functional-commit.json`；文档提交可通过 `git log --oneline -- SourceModels/EvilFallForKipfel/Handoff/20261020-Windows` 查询。以后新的本地未推送提交仍须随 `.git/` 或 Git bundle 搬迁。归档 ZIP 可重新生成，不重复提交大 ZIP。

最新补充：当前两个已跟踪 `.blend` 已转入 Git LFS，实际模型内容与上述 SHA256 不变。Windows 先安装 Git LFS 并执行 `git lfs pull`，再核对模型；详细步骤见 WINDOWS_MIGRATION。旧 ZIP/receipt 保留原始归档基线，当前仓库清单已更新；Git 中启用前的大对象历史保留。

## 新 Codex 操作规则与核对命令

先确认用户迁移后有没有新增修改，再读上述文档和 `git status`。工程目录名仍为 `Avatar_Kipfel_BlueTP`，最近项目名称有 PurpleTP，不能据目录名擅自改名。使用 Unity 2022.3.22f1、Blender 3.6.23 基线；保留现有依赖版本，先重导入和复现，再决定升级。

在 Windows 的工程根目录执行只读检查（需要 Python 3）：

```powershell
python SourceModels/EvilFallForKipfel/Handoff/20261020-Windows/tools/verify_handoff.py --project-root .
git status --short
git log -3 --oneline
```

验证脚本只读文件、不改资产；`CHANGED` 表示与交接基线不一致，也可能是迁移后的合法编辑，必须先比较。`FILE_MANIFEST.json` 含交接内容和本轮资源的 SHA256；`source-history-inventory.json` 只有大历史的路径/大小，不是完整内容哈希备份。

`tools/` 同时包含**已经应用过的一次性修复历史**与未触发的 `CaptureUnityMigrationState.cs`；它们有基线假设、绝对 Mac 路径或自动触发逻辑，不能复制回 Assets 或整批重放。`verify_handoff.py` 是明确的只读核对例外。后续局部修复从 Accepted 的新副本和当前预制件开始，保持已有骨架、32 功能与反应组件。用户偏好直接覆盖同名交付，并自行测试；未经要求不要创建多套近似预制件或改无关场景/材质。
