# 项目资源检查与整理建议

> 本文保留为整理前的检查记录。2026-10-03 已按用户要求完成自有资源归位，第三方资源保持原位置；实际目录与日常规则请以 [资源放置规范](ResourcePlacement.md) 为准。原文中关于天空包筛选、第三方迁移的候选建议本次未执行。

检查日期：2026-10-03。项目：`E:\Detective Game\Detective-Game`。本次只检查并生成报告，没有移动、删除或重新导入项目资源，没有修改 Git 提交。

## 建议结论

采用两层分工：外部“美术资产”保存制作源文件、下载原包和历史版本；Unity 项目保存游戏需要的导出模型、贴图、材质、Prefab、场景、代码和配置。按资源组组织，不为每个文件单独建立目录。优先整理源文件与临时产物，再做少量 Unity 内部归位，不同时重构整个项目。

## 检查范围与证据

- 已确认运行中的 Unity 对应本项目，版本为 6000.5.9f1。通过现有本地 Unity MCP 连接读取编辑器状态。
- 当前场景 `Assets/Scenes/Tutorial.unity`，未处于 Play Mode，场景无未保存标记。检查当前场景全部对象，未发现 Missing Script；此次 Console 查询没有返回错误或警告。这不等同于完成游戏运行测试。
- 扫描了 Assets、SourceArt、Packages、ProjectSettings、Documentation、根目录临时文件，以及外部 `E:\Detective Game\美术资产`。大小为磁盘文件字节数，不是构建包大小或显存占用。统计排除 Library、Temp、obj、.vs、.git、.agents；Assets 目录总量包含 .meta。
- 对四个项目场景调用 AssetDatabase.GetDependencies：Tutorial 返回 204 项，Test 返回 161 项，SampleScene 返回 11 项，Scene 返回自身 1 项；包括脚本和包依赖，不能按数量直接衡量场景复杂度。
- 对超过 128 KiB、大小相同的文件进行了 SHA-256 内容比对；同时扫描了序列化 GUID 和编辑器脚本中的路径字符串。未发现 Assets 文件缺少配套 .meta。
- 当前 Git 为 main，与本地 origin/main 对齐，HEAD 为 `3c43cd8`；SourceArt 当前不在 Git 索引中。与此前失败提交检查时不同，应以此次状态为准。没有重新查询远端。
- 没有逐个打开所有 Blender 文件检查外链贴图，也没有切换其他场景、执行完整构建或验证所有运行时动态加载。因此“没有出现在场景依赖里”只表示候选，不等于可以删除。

## 当前最值得处理的问题

### 1. 制作资料比游戏资源更大

`SourceArt` 共 105 个文件，约 **924.1 MiB**；整个 `Assets` 约 **582.0 MiB**。其中 SourceArt 的 `before` 备份和 `.blend1` 合计约 **610.2 MiB**，大量空间来自桌子制作过程的多个版本。

当前磁盘上的 `desk.blend` 约 **104.8 MiB**，`desk_before_lamp_export_20260930_161508.blend` 约 **101.8 MiB**；它们已经不同于此前待推送提交里的版本。直接按普通 Git 全选提交将遇到大文件问题。`.blend1` 已被现有 .gitignore 忽略，但带 `before` 的 `.blend` 没有被忽略。

结论：先外置这批源文件和备份，比给整个项目所有二进制文件统一配置 LFS 更符合现在的组织需求。外部文件不随游戏仓库备份，需要单独的备份安排。

### 2. 下载原包、编辑源文件、导出件混放

当前既有 `SourceArt/Tutorial/Desk_Unity`、`TableLamp_Unity`、`Wardrobe_Unity`，又有正式的 `Assets/Art/...`，外部还存在 `Wardrobe_Unity`、`Telephone_Unity`。同一资产存在多个看起来都像“正式版本”的位置。

内容比对举例：

- 椅子法线图 `T_Chair_Normal.png` 约 22.1 MiB，在 Assets、Desk_Unity/Textures、Desk_Unity/SourceTextures 下出现三份相同内容。
- 桌子金属/光滑度图约 9.4 MiB，在 Assets 和 SourceArt 下重复。
- 衣柜 BaseColor 在 Assets、两处 SourceArt 和外部 Wardrobe_Unity 中出现四份相同内容。
- 两张纸的 BaseColor 当前内容相同；这可作为合并候选，但要先确认是否计划分别修改，不能只按哈希自动删除。

源文件引用的贴图与游戏导出贴图允许保留两份，因为用途不同；需要减少的是含义不清的多个导出副本。

### 3. 天空包占据 Assets 的大部分

`Assets/SkySeries Freebie` 约 **373.3 MiB**，占 Assets 约 **64%**。Tutorial 实际依赖其中 `AmbienceExposure.mat` 和 `FreebieHdri/AmbienceExposure4k.hdr`；其他三个项目场景的依赖结果没有该素材包。

保留使用中的天空资源和依赖，其余示例场景与天空素材列为归档候选。归档前还需核对全部 Prefab、其他素材包示例和动态引用。不要把 373 MiB 都算成可删除空间，也不要认为它们都会进入最终游戏包。

`StarterAssets` 约 **84.9 MiB**，确实被玩家骨架、动作、控制器和音效使用，不能因名字像示例包就整体移除。BlockadeLabs SDK 约 **22.7 MiB**，未出现在四个场景依赖结果中，但可能仍用于编辑器生产流程，需要按是否继续使用来决定保留。

### 4. 分类粒度不一致

- 床、书桌、衣柜位于 `Art/Props`，台灯在 `Art/TableLamp`，电话模型直接位于 `Art`，电话贴图单独在 `Art/Textures`。
- `Evidence/Data` 只有一个证据数据文件，另有 `GameData/Investigation`，数据入口重复。
- `StudyBlockout` 实际是 11 个白盒材质，适合并入美术目录。
- `Tutorial` 是关卡专用动画、Timeline、灯光和电话配置的组合，有明确用途，值得保留。
- `Recall` 是包含动画、材质、网格和 Prefab 的回溯功能资源组，不应为了“所有 .asset 都在一起”把它拆散。
- `Scripts`、`Editor` 已按系统分组，当前没有必要重新划分。

### 5. 临时工作产物成为版本管理噪声

根目录有 8 个 `.tmp-*` 工作目录，合计约 **49.8 MiB**，包含截图、场景副本、执行结果和一次性脚本。建议忽略 `/.tmp-*/`，从中提取真正需要长期保留的工具和说明：工具进 `Tools/Art`，说明和少量验收图进 `Documentation`。不把全部临时脚本升级成正式工具。

`Assets/_Recovery/0.unity` 是恢复场景，先核对是否包含独有工作，再归档到工程外。`Assets/Scenes/Tutorial/` 是场景烘焙光照数据，不是杂项或源素材，应留在 Unity 内。

## 推荐的外部目录

以下是目标结构，不要求一次创建全部空目录：

```text
E:\Detective Game\
├─ Detective-Game\                 Unity 与代码仓库
├─ 剧情设计\                       剧本文档和设计资料
├─ 图标设计\                       现有图标制作区，暂不重复搬迁
└─ 美术资产\
   ├─ 来源包\                     下载模型原包、原贴图、来源和授权文件
   │  ├─ old-desk\                 保留供应方包结构
   │  ├─ table-lamp\
   │  └─ wardrobe-antique\ ...
   ├─ 制作\
   │  └─ Tutorial\                 当前只有一个关卡资源集，不再细分每件道具
   │     ├─ desk.blend
   │     ├─ bed.blend
   │     ├─ wardrobe.blend
   │     ├─ TutorialWakeUp.blend
   │     ├─ Textures\              用 Desk_ / Lamp_ / Wardrobe_ 文件名前缀区分
   │     └─ Previews\              真正保留的渲染预览
   └─ 归档\
      └─ 2026-10\                  before 版本、旧导出、旧验证记录、场景恢复件
```

制作文件按一套关卡/资源集放一起；下载包因各自带贴图和来源文件，保留原来的包目录是有意义的，不等于每个文件建目录。以后若 Desk 真正发展成几十份独立制作文件，再将它提升为单独资源组。

当前迁移时应先整体复制 SourceArt/Tutorial 到外部制作区并比对文件数量及哈希，保留相对目录以免 Blender 贴图丢失；随后在 Blender 内核对/重设外部文件路径，再把重复导出和旧备份归档。不要一开始只拖走 .blend 而留下一堆未知用途贴图。迁移确认前不删除原件。

正式 FBX/PNG 的交付位置就是 Unity 的 `Assets/Art`。需要检查中间结果时可临时导出到工作区；验收后不长期维护第二套 `XXX_Unity`“正式导出库”。来源署名文件与正式资源一同保留，例如台灯的 Attribution.txt。

## 推荐的 Unity 内部结构

保持现有主体，做少量归并，不新增一层 `_Project` 再全量搬家：

```text
Assets/
├─ Art/
│  ├─ Props/
│  │  ├─ Desk/                    桌、椅、纸共用一组，不继续拆
│  │  ├─ Bed/
│  │  ├─ Wardrobe/
│  │  ├─ TableLamp/               从 Art/TableLamp 归位
│  │  └─ Telephone/               收拢电话模型和号码盘贴图
│  └─ Blockout/                   从 StudyBlockout 归位
├─ Characters/Player/             保留现有玩家专用资源
├─ Scenes/                        Tutorial、Test 及配套光照文件
├─ Scripts/                       按 Gameplay 系统分组，沿用现状
├─ Editor/                        编辑器工具，沿用现状
├─ GameData/
│  ├─ Investigation/
│  └─ Evidence/                   接收 Evidence/Data
├─ Dialogue/                      Ink 剧情源文件，保留
├─ Recall/                        成套回溯资源，保留
├─ Tutorial/                      关卡专用配置、动画、Timeline、灯光，保留
├─ UI/                            Fonts、Tutorial、Test，先保留
├─ Settings/
├─ Shaders/
├─ Resources/                     保留确实按 Resources.Load 加载的资源
└─ 第三方原目录                   暂保留 Ink、StarterAssets、SkySeries 等包名
```

仅一个电话 FBX 和一张贴图时，直接放 Telephone 下即可，不强制创建 Models、Textures、Materials、Prefabs 四层空架子。Desk 已有 3 模型、3 Prefab、4 材质和 10 贴图，现有类型子目录是有帮助的，保留即可。

第三方资源以后可统一到 `ThirdParty`，但不是第一批工作：现有工具硬编码了 StarterAssets 路径，Ink 工具也有默认目录。为了根目录看起来整齐而立即全搬，收益小于风险。Packages 中的嵌入式 `com.cqf.urpvolumetricfog` 应留在 Packages 并纳入版本管理。

UI/Tutorial 与 UI/Test 有类似模板，但内容相似不代表重复垃圾。先确认哪些样式要共享，再考虑共同基础 Prefab 加变体；不要本次顺便做 UI 架构重构。

## 命名与维护规则

1. 文件夹回答“这个资源属于哪组工作”，文件名回答“它是什么”。继续使用 Desk、Bed、Telephone 等有意义的组名。
2. 沿用现有 `SM_` 模型、`M_` 材质、`T_` 贴图；Prefab 可逐步采用 `PF_Desk`、`PF_Chair` 等。避免一次批量改所有旧名。
3. 当前有效制作文件保持固定名字，如 desk.blend；日期与 before 后缀只进归档，不让有效文件夹中同时出现很多候选“最终版”。
4. 共用资源按实际复用提取，不提前建立空的 Common、Shared、Misc 等目录。小组内直接放文件；资源增长到难以浏览时再按类型细分。
5. 建一个轻量的 `Documentation/ArtAssets.md` 索引，记录资源组、相对于“美术资产”的源文件路径、Unity 交付路径、来源署名、导出说明。不为每张贴图维护单独文档。
6. 外部源美术安排独立备份，例如另一块盘的版本备份或独立 LFS 仓库；仅移动到同一硬盘的相邻文件夹不会形成额外备份。

## 整理时必须同步检查的引用

- `SourceArt/Tutorial/BuildWakeUp.py` 写死源文件保存位置和 `Assets/Tutorial/Animations/WakeUp.fbx` 导出位置。建议将可复用脚本放到 `Tools/Art`，用参数指定外部源目录，不继续生成已搬空的 SourceArt。此脚本会覆盖动作，不应为了验证路径而直接重跑。
- `TutorialWakeUpBuilder.cs` 写死 StarterAssets 的原模型和 Idle 动作路径。
- `TutorialTelephoneBuilder.cs`、`TutorialWindowLightingSetup.cs` 等写死 Tutorial 子目录；因此本方案保留 Tutorial。
- `TutorialPhoneCallBuilder.cs` 写死 Dialogue 和 GameData 的路径；本方案保留它们。
- `SceneUIAuthoringBuilder.cs` 使用 `Assets/UI/` 加场景名。若以后把 Test 改名，必须同步处理工具输出位置。
- `RecallAuthoringTools.cs` 会向 `Assets/Recall/Generated` 生成资源，不要只搬文件而不改生成逻辑。
- `InteractionHighlight.cs` 按名称 `Resources.Load<Shader>("InteractionOutlineMask")` 加载资源。Resources 不能当普通冗余目录清走。
- `TutorialExitDoor.cs` 默认目标场景是 `Test`。Test 目前承担实际流程，不应凭名称移除或改成不参与构建的测试场景。
- 当前构建场景顺序是 SampleScene、Tutorial、Test。若正式试玩应从 Tutorial 开始，确认 SampleScene 是否承担启动职责后，再调整 Build Profile 顺序；本次没有改动。

Unity 内移动资源优先使用 Project 窗口或 AssetDatabase.MoveAsset，保留资源及文件夹 .meta/GUID。只保留 GUID 仍不能修复代码中的字符串路径，这两类引用需分别检查。官方说明：https://docs.unity3d.com/cn/2022.3/Manual/AssetMetadata.html

## 体积优化的第二步

当前道具贴图的 Read/Write 均关闭，并配置了压缩；不能仅按磁盘大小判断显存问题。

- 桌子 MetallicSmoothness 源图是 4096×4096，但导入上限为 2048。若近景对比确认无明显损失，可在外部保留 4K 母版，在 Unity 存放真正导出的 2K 文件；只修改 Import Settings 不会减少 Git 中源 PNG 的大小。
- 椅子法线图是 2048×2048，却占约 22.1 MiB，可检查位深、通道和编码。先验证外观和精度需求，再重新导出；不要统一压小所有贴图。
- 台灯当前使用 2K，SourceArt 另有 1K 版本。第一人称可近距离观察台灯，不能只因为存在 1K 就自动替换。
- 中文字体约 15.7 MiB，当前被界面使用。若以后裁剪字库，需覆盖动态剧情文字；不作为第一批清理对象。

## 推荐执行顺序与验收

1. 建立当前项目和未提交工作快照；不要用撤销修改或重新拉仓库来整理资源。
2. 外部复制与验证 SourceArt；保存原始纹理及外链依赖；归档重复导出和 before 备份。添加美术索引、提取有用工具、调整临时文件忽略规则。
3. Unity 内仅整理台灯、电话、白盒材质、证据数据这几处；保留 GUID，核对硬编码路径。
4. 单独处理天空包的未使用候选和恢复场景，避免和大量移动混为一次无法审查的操作。
5. 验收 Tutorial 起床、电话拨号/剧情、台灯、纸张调查、出门进入 Test；验收 Test 的证据板、笔记本和回溯；检查 Console、Missing Script、材质/Prefab 引用；最后用当前构建配置进行一次构建。
6. Unity 工程提交 Assets、Packages、ProjectSettings、Documentation、Tools 和配置文件。SourceArt 外置之后，普通美术源文件不进入游戏仓库；对仍在 Assets 中的较大 FBX/贴图按需要配置 LFS。不要全选把 .tmp-*、下载原包和历史制作备份一起提交。

第一阶段可将约 924 MiB 的 SourceArt 从游戏工程目录外置，仍保留原始数据。这是工作目录分工调整，不是硬盘空间回收，也不是保证 Git 推送减少同等体积。实际推送大小还受历史、压缩和 Git 跟踪状态影响。

依赖检查方法参考：https://docs.unity3d.com/cn/2023.1/ScriptReference/AssetDatabase.GetDependencies.html
