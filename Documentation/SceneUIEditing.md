# Test / Tutorial UI 编辑位置

退出 Play Mode 后打开对应场景，在 Hierarchy 展开 `UI`。

- `Dialogue and Case Notebook`：对话框、对话选项容器、证言板、对话记录、菜单、长按 Esc 进度和提示。Test 的 `Notebook Navigation` 是证言 / 物证页签；`Investigation Wheel` 是暂停菜单。
- `Evidence Board`（Test）：物证列表、描述、推断区、回溯预览和时间轴。
- `Recall`：世界空间提示、回溯时间选择和播放控件。
- `Interaction Prompts`：互动提示容器。

这些对象是场景中的 Prefab 实例；可直接改 RectTransform、Text、Image、CanvasScaler、LayoutGroup 等组件。修改场景实例后保存场景；要改资源默认值则 Apply 到 Prefab，或双击 Project 中对应 Prefab。

页面默认关闭，便于正常游玩。编辑时可勾选目标页面的 Active，在 Scene 视图预览；进入游戏时交互脚本会设置页面初始显隐。不要同时开启多个遮挡页面进行排版。

动态内容使用 `Assets/UI/Test/Templates`、`Assets/UI/Tutorial/Templates` 中的 Prefab：

- `DialogueChoice`：对话选项。
- `EvidenceCard`、`EvidenceMoment`、`EvidenceInferenceOption`、`EvidenceTimeMarker`：物证卡片、事实及推断、拖拽选项、时间点（Test）。
- `RecallPeriod`：回溯时间选项。
- 三种提示卡模板：普通、回溯、互动提示。

改动态项的字体、背景、内边距、尺寸，应改对应模板。列表间距、排列和自动尺寸由容器的 LayoutGroup / ContentSizeFitter 与模板的 LayoutElement 控制。世界物体旁的提示位置仍随目标更新；时间轴、打开动画和选中反馈也仍由脚本控制。

Tutorial 原有的电话拨号、开场字幕及淡入淡出已经使用场景 UI，保留原有引用。`ScreenFade` 因跨场景常驻需要仍位于场景根层级。电话等功能组件上可直接点选引用定位其页面。

控制脚本仍位于原对象（例如 Test 的 `RecallSystem` 和玩家对象）。其中 `Use Scene UI` 已启用，引用接到上述场景对象和模板。不要清空这些引用或关闭该选项。旧场景兼容构建代码仍保留，但这两个场景不会执行运行时排版。

字体为项目内的 Noto Sans CJK SC；许可证见 `Assets/UI/Fonts/LICENSE.txt`。可在各 Text 上替换字体，脚本不会重新指定字号或字体。

编辑器菜单 `Tools > Detective Game > UI > Validate Authored Scenes` 可检查这两个场景的 UI 引用与丢失脚本。转换菜单是一次性迁移工具，遇到已有 UI 根对象会拒绝覆盖，请勿用它重新生成已编辑页面。
