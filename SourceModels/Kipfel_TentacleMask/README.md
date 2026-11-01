# Kipfel TentacleMask

TentacleMask 的 Kipfel 独立预制件适配源文件。Sticky 和 Moist 两种预制件共用贴合后的面罩与触手布局。

## 文件位置

| 内容 | 位置 |
| --- | --- |
| 最终 Blender 源文件 | [Kipfel_TentacleMask_Fitted.blend](Kipfel_TentacleMask_Fitted.blend) |
| Sticky 预制件 | [MA_TentacleMask_Kipfel_Sticky.prefab](../../Assets/TentacleMask/MA_TentacleMask_Kipfel_Sticky.prefab) |
| Moist 预制件 | [MA_TentacleMask_Kipfel_Moist.prefab](../../Assets/TentacleMask/MA_TentacleMask_Kipfel_Moist.prefab) |
| Unity 网格、动画、材质和特效 | [Assets/TentacleMask](../../Assets/TentacleMask/) |
| 触手着色器 | [Assets/lilToon_unebeta](../../Assets/lilToon_unebeta/) |

## Blender 源文件

使用 Blender 3.6 保存，只有一个最终场景，按以下集合组织：

- `Final Meshes`：8 个最终网格，包括面罩、耳绳、内衬、中央触手和四组周边触手。
- `Rig`：对应的蒙皮骨架，保留权重与形态键。
- `Fit Reference`：Kipfel 脸部参考，默认隐藏，可在大纲视图中显示。

源文件使用简化材质查看几何，不依赖外部贴图或本机绝对路径。实际着色、动画、PhysBone 和液体效果由 Unity 预制件提供。

Unity 成品引用 `Assets/TentacleMask` 内的网格资源；修改本目录的 Blender 文件不会自动更新预制件。继续修改时应同步对应网格并在 Unity 中验证蒙皮、动画和效果位置。

## 仓库维护

本目录只保留最终 `.blend` 和本说明。历史备份、试验脚本、截图及采样数据存放在项目外，不作为构建或使用依赖。

仓库已有 `.blend` 的 Git LFS 规则。提交时同时保留 Unity 资源及其 `.meta`；克隆后使用 Git LFS 获取 Blender 文件，并按项目现有包配置恢复 Unity 依赖。
