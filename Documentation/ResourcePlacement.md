# 资源放置规范

更新日期：2026-10-03。本文件描述已落地的目录，作为新增、导出和整理资源时的放置依据。

## 一、项目与制作资料的分工

`E:\Detective Game\Detective-Game` 是可运行的 Unity 工程及代码仓库；`E:\Detective Game\美术资产` 是美术制作区。游戏运行需要的资源必须在 Unity 工程中；可编辑源文件、下载原包和历史版本放到外部。外部源文件不随游戏 Git 仓库同步，应单独做版本备份。

```text
E:\Detective Game\
├─ Detective-Game\
│  ├─ Assets\                 游戏实际使用的资源
│  ├─ Packages\               包配置与嵌入式包
│  ├─ ProjectSettings\        Unity 项目配置
│  ├─ Tools\Art\              可复用的美术制作脚本
│  └─ Documentation\          项目说明、资源索引、少量验收图
├─ 美术资产\
│  ├─ 来源包\                 下载的原模型包、贴图、压缩包和来源说明
│  ├─ 制作\Tutorial\          当前关卡制作文件
│  │  ├─ desk.blend            桌椅、纸张和台灯制作工作文件
│  │  ├─ bed.blend
│  │  ├─ wardrobe.blend
│  │  ├─ TutorialWakeUp.blend
│  │  ├─ Textures\            制作贴图，包含修复后的角色贴图副本
│  │  └─ Previews\            制作预览图，不作为正式游戏贴图入口
│  └─ 归档\2026-10\
│     ├─ 制作备份\            before 版本、转换为可打开 .blend 的自动备份
│     ├─ 旧导出与记录\        旧 FBX/贴图交付副本、验证记录、说明
│     ├─ 迁移原件\            本次迁移前的完整原目录、恢复场景
│     ├─ 整理前项目快照.zip    整理前 Assets/SourceArt/配置/文档快照
│     ├─ 迁移文件清单.json     源文件到新位置的映射及原始哈希
│     └─ Blender迁移验证.json  重新保存后的文件哈希、贴图及数据数量验证
├─ 剧情设计\                 剧情设计文档
└─ 图标设计\                 现有图标制作区
```

“迁移原件”和快照是本次整理的回退材料，有意保留原内容，不作为日常编辑入口，也没有为了回收空间而删除。优先打开“制作”目录下的当前文件。归档原件保留旧路径，回退时结合映射恢复原目录；日常查看历史制作版本优先使用已修正贴图路径的“制作备份”。快照不包含 .git、Library、Temp、.tmp-*，不能作为包含全部 Git 历史的整机备份。

## 二、Unity 内部放置规则

### Art：游戏使用的模型、贴图和材质

```text
Assets/Art/
├─ Props/
│  ├─ Desk/                   桌、椅、纸张作为一套资源
│  │  ├─ Models/
│  │  ├─ Materials/
│  │  ├─ Textures/
│  │  └─ Prefabs/
│  ├─ Bed/                    保留已有模型、材质、Prefab 分类
│  ├─ Wardrobe/               保留已有模型、材质、贴图及使用说明
│  ├─ TableLamp/              模型、材质、贴图、Prefab、Attribution.txt
│  └─ Telephone/              当前直接放电话 FBX 与号码盘贴图
└─ Blockout/Materials/        原 StudyBlockout 的白盒材质
```

按一套可共同制作和使用的资源建目录，不为每个文件建目录。电话当前只有两个文件，直接并排放置；Desk 已有多种模型和十张贴图，按类型分子目录有实际用途。不要为了目录对称建立空文件夹。正式交付以 Assets 中版本为准，不再长期维护第二套 `Desk_Unity`、`TableLamp_Unity` 等交付库。

### 其他自有资源

- `Assets/Characters/Player`：玩家专用 Avatar/身体网格配置、镜头混合配置。
- `Assets/Scenes`：项目场景。`Scenes/Tutorial` 的 Lightmap、反射探针和 LightingData 是场景依赖，留在此处。
- `Assets/Scripts`：运行时 C#，沿用 Dialogue、Evidence、Input、Interaction、Investigation、Player、Recall、Tutorial 功能分组。
- `Assets/Editor`：编辑器工具和现有烟雾测试，保留 Editor 特殊目录含义。
- `Assets/GameData/Investigation`：调查数据库；`GameData/Evidence`：证据定义。原 Evidence/Data 已归并至此。
- `Assets/Dialogue`：供游戏导入的 Ink 剧情源文件；剧情策划草稿留在工程外“剧情设计”。
- `Assets/Recall`：成套回溯动画、数据、材质、网格和 Prefab，不按扩展名拆散。
- `Assets/Tutorial`：关卡专用动画、Timeline、电话配置、音效、灯光及材质；按功能保留成套资源。
- `Assets/UI`：Fonts、Tutorial、Test 的界面 Prefab/模板。先保留场景差异，确有复用需要时再提取共享模板。
- `Assets/Settings`：渲染管线、Volume 和光照等项目配置。
- `Assets/Shaders`：自有 Shader；`Assets/Resources`：确实通过 Resources.Load 按名称加载的资源。不要把 Resources 当作通用素材仓库。

`Test` 是目前 Tutorial 出门后的实际目标场景，不是可随意移除的测试文件。场景名称、构建顺序和玩法本次均未改变。

### 第三方资源

`Assets/StarterAssets`、`Assets/SkySeries Freebie`、`Assets/Ink` 以及项目中其他第三方资源保持原位置和内容，不清理其中的示例或天空变体，不为了统一名称搬到 ThirdParty。`Packages/com.cqf.urpvolumetricfog` 是嵌入式包，连同源码纳入版本管理。

BlockadeLabs SDK 在本次整理开始时已不在 Assets 中；本次没有删除、恢复或重新安装它。以后重新导入第三方包时，优先保留供应方目录，并记录来源及版本。

## 三、资源索引

下面的源路径均相对于 `E:\Detective Game\美术资产`：

- **桌子、椅子、纸张**：编辑 `制作/Tutorial/desk.blend`；游戏资源放 `Assets/Art/Props/Desk`。Desk 作为一组维护，不为桌、椅、两张纸分别套多层目录。
- **台灯**：当前也在 `制作/Tutorial/desk.blend` 中制作；下载原包位于 `来源包/table-lamp`，其他灯具候选也留在来源包。游戏资源放 `Assets/Art/Props/TableLamp`，保留其 Attribution.txt；不要把候选模型当成当前正式版本。
- **床**：编辑 `制作/Tutorial/bed.blend`；交付到 `Assets/Art/Props/Bed`。
- **衣柜**：编辑 `制作/Tutorial/wardrobe.blend`；原包 `来源包/wardrobe-antique`；交付到 `Assets/Art/Props/Wardrobe`。
- **电话**：游戏交付文件为 `Assets/Art/Props/Telephone/telephone.fbx`。多个下载候选在来源包；此前 `Telephone_Unity` 已放进 `归档/2026-10/旧导出与记录/外部导出`。未找到已明确标记的正式电话 .blend，不能把候选文件当作已验证母版；后续编辑时选定源文件并在此补记。
- **起床动画**：编辑 `制作/Tutorial/TutorialWakeUp.blend`；交付 `Assets/Tutorial/Animations/WakeUp.fbx`，与 `Assets/Tutorial/Timeline/TutorialWakeUp.playable` 配合。
- **玩家与天空**：继续引用 StarterAssets 与 SkySeries Freebie 原资源，不复制到自有 Art 目录制造第二套版本。

## 四、新增资源工作流

1. 下载素材先放“来源包”，保留包内相对目录、授权/署名和来源链接。现有包以供应方包名区分，不按每张纹理重新建目录。
2. 需要加工时，把当前工作文件保存到“制作/关卡或资源组”，以固定名字表示有效版本，如 `desk.blend`。纹理使用相对路径，确保搬动整个美术资产目录后仍可解析。
3. 制作母版、高分辨率贴图与草稿放在工程外。导出给游戏的 FBX、PNG 和动画放入 Assets 的对应组；Prefab、Unity Material 和 .meta 始终留在工程内。
4. 旧版本与过时导出放入归档的年月目录，不让工作目录充满 `final2`、`latest_new`、`before` 等候选。归档文件不作为场景引用来源。
5. 每次正式替换交付资源后，在 Unity 检查尺寸、材质、动画、Prefab 和场景效果；需要保存的少量验收截图放 Documentation/Images。

保留现有 `SM_` 模型、`M_` 材质、`T_` 贴图命名习惯；新 Prefab 可用 `PF_`，不为了命名统一批量改旧资源。共用资源实际出现后再提取，不提前堆叠 Common/Shared/Misc。

## 五、工具、Git 和移动规则

- `Tools/Art/BuildWakeUp.py` 是原型动作生成器，会重建关键帧，不是日常保存按钮。默认从脚本位置定位项目，源目录为相邻“美术资产/制作/Tutorial”；支持 `--project-root` 和 `--source-dir`。已有 .blend 或 FBX 时默认拒绝覆盖；只有明确要重新生成时才传 `--overwrite`。手工精修后不要重跑。
- 临时自动化输出保留在根目录 `.tmp-*`，现在已由 .gitignore 排除。旧任务脚本保留便于追溯，但其中可能写死整理前路径，不应直接重跑；正式可复用工具放 Tools。
- 旧 `/SourceArt/` 已外置并加入忽略规则，防止历史工具再次把源素材提交进游戏仓库。不要往这个旧目录继续制作。
- 提交 Assets（包含 .meta）、Packages、ProjectSettings、Documentation、Tools、.gitignore、.gitattributes。不要提交 Library、Temp、构建输出、临时日志、外部来源包和历史备份。
- 本次没有配置 Git LFS，也没有提交或推送 Git。需要在工程内版本管理的大型交付资源，可另行按实际需要配置 LFS。
- Unity 内移动资源用 Project 窗口或 AssetDatabase.MoveAsset，保留 GUID 和文件夹 .meta。不要只用文件管理器搬模型而漏掉 .meta。
- 移动后同时检查生成工具中的路径字符串、Resources.Load 的名称、场景跳转和 Build Profile；GUID 保持不变不等于字符串路径自动更新。

## 六、本次迁移范围与回退

Unity 内完成 TableLamp、Telephone、Blockout、Evidence 四类归位；SourceArt 的 105 个文件按用途外置；外部 13 个原入口收拢为来源包、制作和归档三类入口。恢复场景完整保存在“迁移原件/恢复场景”，原内容未丢弃。第三方资源、游戏脚本、场景内容与贴图分辨率未作批量改写。

回退前先保留整理后新增的工作，不要直接解压覆盖整个项目。Unity 内按移动清单反向移动并保留 .meta；外部原件可按 `原件位置映射.json` 恢复到原位置；必要时从整理前快照提取指定文件。迁移回退不使用 git reset/discard，以免丢失整理前就存在的未提交改动。

检查与验证结果见 [资源迁移验证记录](ResourceMigrationValidation.md)。整理前分析保留在 [AssetOrganizationReview.md](AssetOrganizationReview.md)，其中候选方案不覆盖本文件的实际规则。
