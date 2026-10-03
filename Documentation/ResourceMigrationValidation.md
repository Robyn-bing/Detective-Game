# 资源迁移验证记录

日期：2026-10-03。对应规范：[资源放置规范](ResourcePlacement.md)。

## 完成范围

- `Assets/Art/TableLamp` → `Assets/Art/Props/TableLamp`。
- `Assets/Art/telephone.fbx` 与 `Assets/Art/Textures/TEL_Static_NumberRing_BaseColor.png` → `Assets/Art/Props/Telephone`。
- `Assets/StudyBlockout` → `Assets/Art/Blockout`。
- `Assets/Evidence/Data` → `Assets/GameData/Evidence`。
- `SourceArt` 的 105 个文件按用途迁至外部制作区、历史归档，以及工程内 `Tools/Art/BuildWakeUp.py`。
- 外部美术目录的 13 个原入口归入“来源包”或旧导出归档；日常制作入口为 `美术资产/制作/Tutorial`。
- `_Recovery` 完整归档；空的 Evidence、Art/Textures 目录及其 .meta 保存在忽略目录 `.tmp-resource-organization/empty-folders`。
- `.tmp-*`、旧 SourceArt 和 Python 缓存已加入 .gitignore。起床动画生成脚本已适配外部源目录，并增加显式覆盖开关。
- 第三方目录保留原位。BlockadeLabs SDK 在本次迁移开始前已经不在 Assets 中，本次未对其进行删除或恢复。

## 文件完整性及 Unity 引用

整理前保存了文件 SHA-256 清单和可解压快照。按移动映射逐文件比对，**1,339 个 Assets、Packages、ProjectSettings 文件内容保持不变**，其中包含 **755 个 StarterAssets、SkySeries Freebie、Ink 文件**。比对以迁移前工作区为基准，保留当时已有的未提交改动，不以 Git HEAD 覆盖工作区。

Unity 内资源使用 AssetDatabase.MoveAsset 迁移，文件和目录 GUID 保持不变。四个项目场景的递归依赖与迁移前一致，仅资源路径按映射变化：

- Tutorial：204 项，GUID 映射一致。
- Test：161 项，GUID 映射一致。
- SampleScene：11 项，GUID 映射一致。
- Scene：1 项，GUID 映射一致。

恢复执行后重新读取了 Unity 依赖并比对文件，结果仍一致。核对时 Tutorial 未运行、无未保存标记；上轮验证结束时当前场景 Missing Script 数量为 0，Console 查询没有返回错误或警告。

`Tools/Art/BuildWakeUp.py` 的 Python 语法检查通过，没有执行动作重新生成或覆盖现有 FBX。已检查活动脚本和说明中的旧移动路径；整理前分析报告及归档记录故意保留历史路径。

## Blender 文件

16 个制作文件及历史备份均在 Blender 5.2.2 后台重新打开、重设外部路径、保存并再次打开验证。各文件的对象、网格、顶点、多边形、动作和材质数量保持一致；保留了否则可能在保存时清理的未使用制作数据块。

修正了原来就失效的灯具贴图路径，以及起床动画中旧 Mannequin 贴图名称对应的路径。角色制作需要的贴图复制到外部 Textures，未改动 StarterAssets 原图。

原目录归档后再次打开全部 16 个迁移文件，未发现缺失的未打包贴图。部分历史源文件读取时仍会提示其原作者路径不可用，但 Blender 保留了内嵌贴图；迁移后的外链验证通过。没有进行所有模型的逐像素渲染对比。

## 已执行的运行测试

以下测试在中断前已完成，结果在当前聊天的执行记录中可核对；本次恢复没有重复改动资源或重跑已经通过的玩法测试。

- **TutorialDeskInteractionTests：15 项通过，0 项失败。** 包括台灯开关、镜头与输入恢复、纸张查看、电话拨号入口、电话回溯与实体模型对应关系。
- **CaseNotebookSmokeTests：28 项通过，0 项失败。** 包括笔记本/证据页面、菜单输入、回溯预览、推理选择提交，以及对话与笔记本之间切换。
- 两组测试完成后 Console 查询未返回错误或警告，已退出 Play Mode，并恢复打开 Tutorial。

Tutorial 测试原结果位于 `.tmp-desk-interactions/tests.json`。笔记本原结果写在 `Temp/NotebookQA/report.json`，恢复时此临时文件已不存在；上述 28 项结果依据中断前实际读取的完整报告，不将临时文件缺失表述为重新测试通过。

没有执行完整 Player 构建，也没有完整跑通起床动画至离开房间的全部剧情。上述验证覆盖本次移动的文件完整性、引用关系和相关交互，不等同于完整游戏验收。

## 归档与回退

外部归档位置：`E:\Detective Game\美术资产\归档\2026-10`。

- `整理前项目快照.zip`：1,556,759,442 字节，创建时通过 ZIP 校验。
- `迁移原件`：保留旧目录的完整文件，未删除原内容；含 `原件位置映射.json`。
- `迁移文件清单.json`：新位置映射与原始文件哈希。
- `Blender迁移验证.json`：保存后的 Blender 文件哈希和验证摘要。

自动审批曾拒绝递归删除旧副本，认为删除范围较广、存在损失风险。该删除没有执行，之后改用完整移动原件到归档的方案，归档操作已成功。无需为本次任务继续删除归档来“完成清理”。

本次没有执行 Git commit、push、reset 或 discard。全仓库 `git diff --check` 会报告迁移前已存在的 Unity YAML 空白和脚本末尾空行；资源哈希证明本次未改写这些文件。新增的 .gitignore 变更专项检查通过。
