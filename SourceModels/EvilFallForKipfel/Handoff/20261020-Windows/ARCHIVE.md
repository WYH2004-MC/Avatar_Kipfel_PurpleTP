# 归档内容与核对方法

生成文件：`SourceModels/EvilFallForKipfel/Archives/EvilFall_Windows_Handoff_20261020.zip`。ZIP 内保持工程相对路径，可以解压到独立目录检查，不要直接覆盖已有新工作。

归档包含本交接目录、已接受 Blender 完整源、本轮 `Assets/YaokiSubWork` 资源及 meta、范围内的 Git 规则。`user-work/` 是原有用户编辑的磁盘副本；`ram/Blender_Unreviewed.blend` 是未复核内存副本。预览图片是各已接受阶段当时的检查截图，不代表后来运行测试。

归档**不是完整 Unity 项目或 Git 仓库**，不含其他角色资产、整个 Packages、`.git`、Unity 平台缓存及约 46 GiB 历史候选。完整搬迁仍按 WINDOWS_MIGRATION.md；保留本地未推送提交需要另带原 `.git/`。

`FILE_MANIFEST.json` 逐文件记录本轮资源和交接文件的大小、SHA256、工程相对路径，排除清单自身。Python 3 只读验证：

```powershell
python SourceModels/EvilFallForKipfel/Handoff/20261020-Windows/tools/verify_handoff.py --project-root .
```

完整性验证通过只说明与交接基线一致，不代表 Unity 运行、NDMF 构建或 VRChat 测试通过。之后合法修改也会产生 CHANGED，应先比较再决定恢复。

ZIP 的 SHA256 与生成时两条本地提交号记在同目录旁的 `.receipt.json`，生成归档本身未重复提交进 Git。
