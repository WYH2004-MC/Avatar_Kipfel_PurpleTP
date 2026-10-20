# EvilFall → Kipfel 修改日志

本日志截至 2026-10-20，按阶段顺序归档。最新 Unity 交付以 [MenuRestore/COMPLETION.json](evidence/MenuRestore/COMPLETION.json) 为准；历史 `Final*` 文件是累积保存点，不是需要依次安装的补丁。历史过程摘录另存 [历史 RESUME](evidence/BlenderStages/Historical_RESUME.md)；其中提到的 WorkSessions 文件仅完整搬迁历史时可用。

## 2026-10-18～19：面罩、管路与前领

- 按原 FBX 轮廓适配默认面罩：修鼻上沿、侧脸圆盘、球体对嘴和内外管连接，兼容已接受的 HeadMask。保留原独立形态键、权重及控制树；FaceMaskV2 几何继续暂停，没有安装旧候选。
- 撤销用户否决的中央厚楔和过度外推，恢复原版半嵌安装及侧面倾角。中央模块等比缩小至 90%，让顶尖收进鼻框；没有用拉长后裙补缝。
- 用户指出的“左边大右边小”包含单根管沿轴向粗细跳变及套筒偏轴。最终统一接口轴线、首段切线和圆截面，消除斜视 C 形悬唇；隐藏接管段适度收径，保持盘端位置及原开关。
- `MainNeckTube` 外露粉管按后颈出口统一为直径约 5.7064 mm，左右镜像，去中段多次波浪，保宽缓转弯；同步运输相关 8 个 rest 骨。隐藏插座仍保留装配所需的不同尺寸。
- 截图中称“袖口”的接口实际位于 `MainBodySuit` 前领。将银套重新插入黑座，斜管、粉条和夹块共同顺直，邻接衣边同步贴合。之后再修 `MainBodyBreast` 的尖褶和纯侧视脱座自由唇；最终以 `FinalCollarEdgeSeatFit` 保存点取代仅压平峰值的中间稿。

## 2026-10-19：MainCoat 网格和肩袖

- 参考原 FBX，为两组原形态建立共同连续变形场，恢复肩部与外衣轮廓；删除原袖末 32 个封盖四边面，形成真正开口并修腕部贴合。
- 隔离对象后确认肩部银碎片来自固定 `MainArmGlove` 银环及透明 Shield 穿入外衣内壁。采用局部肩套扩容，保留手套、手部和已接受腕区；削孔、缩成细条的候选未采用。
- 用户再次指出四条黑粉装饰框被灰袖筒截断。最终对 32 个长面细分，新增 96 点，完整黑底、发光条、倒角与内返壁共同跟随；保留原 3549 点、两组形态及权重。最终 `MainCoat` 为 3645 点、3556 面、7124 个原生三角面，证据见 `FinalMainCoatPinkFramesFit/COMPLETION.json`。

## 2026-10-19～20：眼镜与有线耳机

- 眼镜后环针对大头与当前五件头发升高、扩径，前组上提 8 mm；后续只保留用户修改的前组 Y，恢复后环位置。各原收起键和宽窄控制语义保留。
- 侧框按用户指定尺寸缩小。两翼外摆导致镜片折角的方案被否决；最终沿 X 整体延长镜片与接桥 6%，保持原 Y/Z 弧面。金属桥使用完整仿射接回，弃用会产生灰尖三角的局部变形。
- `FinalGlassArcExtensionFit` 虽曾标完成，后来被用户截图否决侧框上沿穿发。最终 `FinalGlassSideHairClearance` 保持镜片、后环及侧框尺寸，整体调位侧框，修复真实发面交线；替代早期“允许藏发所以接受交线”的错误结论。
- 耳机球按用户指定位置入耳，对侧镜像，适度嵌耳但不从耳背露出；线头接入球体圆孔。原版接口圈组整体运输，保紧凑圈距，撤销将圈拆远、上部折角的中间方案。
- 最终耳线绕开当前五件头发，采用空间圆弧 V3，去尖 V 投影；保球、孔内下接口、下段和管径。仅改 `HeadACC` 现有 2042 点，仍为 5946 点、5916 面。

最新完整几何是 [FinalEarphoneCableHairClearance](../../WorkSessions/20261013-skin-contact-fit/FinalEarphoneCableHairClearance/EvilFall_Kipfel_EarphoneCableHairClearance.blend)，该 WorkSessions 路径仅完整搬迁历史时可用；完整交付副本为 [AcceptedForUnity.blend](../../Exports/20261020-Unity/EvilFall_Kipfel_AcceptedForUnity.blend)。`SourceModels/EvilFallForKipfel/EvilFall_Kipfel.blend` 是旧 production，不得当作当前版本。所有一次性 helper 已应用，禁止在最新文件重放。

## 2026-10-20：Unity 导出与跟随补修

- 导出 49 对象、38 网格、9 骨架共 431 骨，保形态键与权重；按用户要求覆盖 `Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel.fbx` 和同名 `.prefab`，保持 GUID，清理 `_Fitted` 重复资产。
- 最初遗漏了 Blender 约束对应的 Unity 合并关系，用户实测衣服、眼镜、武器、鞋子不跟随。随后将 Fit212 作为公开 `armature`，Source166 改名 `EvilFall_SourceRig`，用 MA 合并和保世界父级复现 181 条 CopyTransforms、Base ChildOf 及 9 个对象跟随；另纠正 11 个生成错误的材质槽顺序。
- `MainLegHeel` 开启时，共 8 条 `Body_Base` 形态设为 100：左右 Knee/Foot/Toe 的 OFF 六项及 `Lower_leg_01_OFF`、`Lower_leg_02_OFF`。`HeadACC` 开启关闭 `Cat_Ear`；`EvilFallTail` 开启关闭 `Cat_Tail`，均为 Avatar 相对引用。

## 2026-10-20：原版菜单功能恢复

恢复 7 个菜单资产、5 组 32 个 Toggle 功能及 32 个 Bool 参数，保原图标、顺序、名称、默认值和互斥语义；恢复 26 层、60 状态、12 个互斥 Driver，适配 59 动画。入口是 `ForKipfel/InstallMenu.asset`；唯一 MA MergeAnimator 使用 `Accessories.controller`，旧 `Clothing.controller` 留作资产但不重复合并。

同时恢复 20 个粒子系统、总计 19 个 PhysBone 及缺失辅助链/Blocker；保已有遮肤、猫耳尾巴反应与骨跟随。菜单阶段最终 MA 合并组件总数为 30。原生保存回读及独立检查通过：1178 条动画绑定有效，181 条骨跟随路由匹配，38 个网格默认形态与骨引用保留。最新 prefab SHA256：`b5cc9d70fca94a900696c18aa3a625b52da24ccb7137a007f426a4387b6dfa27`。

原版参数及动画中已有的大小写、空格和拼写按资产保留，包括 `OhHand`，没有为统一命名而改写绑定。武器保留已接受的姿态数据，再恢复原组语义；其余位移动画按原静止形态到当前静止形态的差量适配。菜单恢复没有重导模型、改原版菜单或覆盖用户场景，未对无关材质和贴图进行重新制作。临时执行脚本及自动触发文件已清理，备份和检查报告保留在导出目录。

## 验证边界与 Windows 后续

Blender 已做实际视角检查、保存重开及有限形态/姿态验证；明确的插座半嵌和内层装配接触仍保留，未宣称全场景零交线。Unity 已完成原生序列化回读、动画绑定和静态路由检查，未执行 agent Play 测试或完整 NDMF/Avatar 构建。Windows 端继续实测动作、32 项菜单互斥/组合、物理骨、粒子和穿模；如有新反馈，从当前完整源及原位 prefab 局部修复，不回退旧候选或重新生成简化菜单。
