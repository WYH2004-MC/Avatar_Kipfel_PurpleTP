<!-- CURRENT_DELIVERY_POINTER_BEGIN -->
当前交付以 [2026-10-21 背部徽标与衣领接缝修复报告](../../Diagnostics/20261021-BackLogoClearance/COMPLETION.json) 和 [CURRENT_STATE](../../WorkSessions/20261021-BackLogoClearance/CURRENT_STATE.json) 为准。衣领连接边21点双形态坐标固定，之前候选的空隙已闭合；只平顺调整内部衣片避开直饰带，原饰带不动。Blender/原FBX已保存，原prefab及场景引用已核对。32参数保持原版 `EvilFall/...` 名称及设置。本轮仅当前姿态静态检查，安装根部接触保留。下文为历史阶段，禁止重放旧脚本/旧备份覆盖当前成果。
<!-- CURRENT_DELIVERY_POINTER_END -->

# EvilFall → Kipfel 修改日志

本日志截至 2026-10-21，按阶段顺序归档。当前工作区最新交付以 [尾巴物理和武器显隐完成报告](../../Diagnostics/20261021-TailTubePhysics/COMPLETION.json)（承接 [腰侧细节和袖部护片报告](../../Diagnostics/20261021-WaistAndArmRing/COMPLETION.json)）（承接 [肩甲、背片和武器管路报告](../../Diagnostics/20261021-DetailAndWeapon/COMPLETION.json)） 和 [当前状态](../../WorkSessions/20261020-RearFrameContour/CURRENT_STATE.json) 为准；菜单恢复历史证据见 [MenuRestore/COMPLETION.json](evidence/MenuRestore/COMPLETION.json)；历史 `Final*` 文件是累积保存点，不是需要依次安装的补丁。历史过程摘录另存 [历史 RESUME](evidence/BlenderStages/Historical_RESUME.md)；其中提到的 WorkSessions 文件仅完整搬迁历史时可用。

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

同时恢复 20 个粒子系统、总计 19 个 PhysBone 及缺失辅助链/Blocker；保已有遮肤、猫耳尾巴反应与骨跟随。菜单阶段最终 MA 合并组件总数为 30。原生保存回读及独立检查通过：1178 条动画绑定有效，181 条骨跟随路由匹配，38 个网格默认形态与骨引用保留。菜单恢复阶段当时的 prefab SHA256：`b5cc9d70fca94a900696c18aa3a625b52da24ccb7137a007f426a4387b6dfa27`。

原版参数及动画中已有的大小写、空格和拼写按资产保留，包括 `OhHand`，没有为统一命名而改写绑定。武器保留已接受的姿态数据，再恢复原组语义；其余位移动画按原静止形态到当前静止形态的差量适配。菜单恢复没有重导模型、改原版菜单或覆盖用户场景，未对无关材质和贴图进行重新制作。临时执行脚本及自动触发文件已清理，备份和检查报告保留在导出目录。

## 2026-10-20 Windows：胸片下缘覆盖与褶皱修复

- 修复 `MainBodyBreast` 下缘与中央管座两侧黑框之间的露肤。改为连续平滑位移场，消除短过渡产生的放射尖褶；主问题边由约 35～41° 降至约 2°。不改黑框、管座、拓扑、UV、权重或材质槽。
- 当前 Blender 界面已同步，保留用户视角、选择、编辑模式和帧；完整 accepted 源原位保存。原位 FBX 和配套胸片形态法线数据同时更新，保留原 FBX/prefab GUID；其余 37 网格及既有修复守恒。
- 七个端点和两个已测混合组合均无实际身体交线；两次 Unity 原生重导完整数据逐字节一致，38 个网格的材质、骨骼与默认状态保留。Blender/FBX/法线数据 SHA 前缀分别为 `797313df` / `a0518cc6` / `f5617574`；完整校验见上方最新完成报告。
- `Breasts_big` 原有特殊收缩折叠仍作为验证边界保留；未宣称全姿态、全组合无自交，也未完成新 NDMF/VRChat 构建验收。较早候选只作历史证据，不得覆盖当前成果。

## 2026-10-20 Windows：PC 材质采样精度提升

- 7 张共享贴图原位添加 Standalone 高质量覆盖：MatCap 2/4 恢复原生 512 BC7，MatCap 3 保持 1024 并改 BC7；MainNormal/TransNormal 保持 2K 并改 BC5；TransBase/MainMaskEX 保持 2K 并改 BC7。
- 与原生尺寸未压缩参考进行 GPU 解码比较：主法线 RMS 角误差降低 44.08%，透明法线实际强度 0.5 降低 47.47%；5 张颜色图 RGB RMS 误差降低 81.27%～96.44%。这些是采样误差数据，不代表肉眼画质提升百分比。Alpha 有小幅压缩量化权衡，已记录并检查实材质预览。
- 正式导入与批准候选逐像素一致；源 PNG、GUID、Blender、FBX、预制件及材质参数保持，默认/其他平台与 mip/streaming 设置保持。原预制件通过现有共享依赖直接生效；全 mip 纹理块存储增加约 3.896 MiB。
- 同镜头/灯光、归一化取景对照背部、胸片与鞋子；实验资产已清理，原 meta 和完整验证报告保留。此前胸片下缘等几何修复均保留。

## 验证边界与 Windows 后续

Blender 已做实际视角检查、保存重开及有限形态/姿态验证；明确的插座半嵌和内层装配接触仍保留，未宣称全场景零交线。Unity 已完成原生序列化回读、动画绑定和静态路由检查，未执行 agent Play 测试或完整 NDMF/Avatar 构建。Windows 端继续实测动作、32 项菜单互斥/组合、物理骨、粒子和穿模；如有新反馈，从当前完整源及原位 prefab 局部修复，不回退旧候选或重新生成简化菜单。


## 2026-10-21：肩甲、背片顺直与武器原版走线

- 保留原版样式，复用真实较大肩甲标志；背部印花改为单相位蜂巢和对称设计直线，同步Base/发光Mask/Alpha，新增局部材质绑定。
- 按用户原版图重新修正 MainBodySuit 下段374点，恢复顺直曲面，UV/权重/拓扑不变，全部形态共同连续跟随。两侧设计线偏离减少约97%；保留原接触边界。
- 保留用户四件武器新位置，按原装备走线连接后管；38骨、8约束及四模式840TRS重新定基准，State3单骨防折回。已写回原FBX/预制件/四动画。
- 更新严格形态法线guard，保旧DN和81个skip；两次native完整回读一致。当前Blender从live保存，保HeadACC雕刻界面，未保存用户Unity场景。
- 细节见本阶段COMPLETION.json及evidence；用户新指出腰侧黑板清晰度/网格和袖部透明Shield疑似相交，继续局部检查，不回滚本阶段成果。

## 2026-10-21：腰侧标记、下装甲与袖部透明护片

- 三种腰侧白色标记保原版轮廓清晰化，左右原法线分别保留；新增3张1024×2048图、2个局部材质，粉色槽口仍为原发光几何。下装甲完整197面板按原版相似形恢复；共424点局部修复，Waist_corset绝对端点保持。
- 确认截图为黑袖穿出透明护片，同时框架使用了不同的早期变形。恢复原版共同装配，并用连续过渡调整下方袖布；总630点，透明片146点、肩甲与MainCoat保持，ArmRingOff框隐藏端点保留。
- Blender当前界面已同步并原位保存，保留HeadACC编辑模式与frame207；未重开/覆盖live文件。FBX原GUID和全部38个mesh fileID保留，原预制件引用直接生效。其余37个native网格全数据逐值不变，原UV/权重/材质与开关保持。
- 实际Unity三角面验证3个Off的8种端点组合：护片/框对袖布、局部袖布与护片对身体、自交均为0。保留原金属根座和MainCoat装配嵌合，不宣称整个装配全交线为0；未绑定Waist中间混合域及完整动作/VRChat构建仍为验证边界。
- 当前Blender/FBX/prefab SHA前缀为ef895899/28aa5728/54dee5ef。完整审计、前后图和验证边界见最新COMPLETION.json。历史helper已应用，不可重放；旧备份不得覆盖当前成果。

## 2026-10-21：尾巴物理链和武器显示时隐藏Cat_Tail

- 对照原版定位到MA zip合并将尾巴PB根保留成孤叶，随后AAO删除无效PB、尾巴退化为单骨。仅把现有PB rootTransform指向完整Fit .001链，保原组件位置/物理参数/启停动画。
- 在四件武器实际mesh（含后管）添加MA Object Toggle隐藏Cat_Tail，保原EvilFallTail规则与武器/尾巴互斥。任一装备可见时隐藏原尾巴，全关恢复初始值；不加新菜单参数。
- 原版WeaponTube没有独立PhysBone，动态来自骨约束和武器模式。8个当前约束source有效，保留原MainBlade物理；未错误添加自由摆动管物理。
- 原预制件保存为070e9c6f，GUID/meta不变。当前scene继承修复且114个旧override完整保留；1188个旧prefab块精确保留，只有1个PB引用和4个新组件/引用发生预定变化。Blender/FBX及既有细节成果SHA保持。
- 实际已保存版本的隔离完整NDMF成功，尾巴恢复14个有效skin骨、13动态骨受PB控制；10组真实FX开关向量与1536组独立显隐状态验证通过。后管另做四种身体转动FK加实际BakeMesh，确认能变形。
- 未进行VRChat实时物理/完整SDK上传验证；这与NDMF、编辑器动画和运动学检查分别记录。临时构建资产已清理；最新COMPLETION.json保留完整证据和边界。
