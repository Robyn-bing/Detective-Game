# Tutorial 电话转场与出口门

## 调节位置

- 通用 Fade 脚本：`Assets/Scripts/Player/ScreenFade.cs`。
- 编辑模式 Hierarchy：`ScreenFade / FadeImage`。Canvas 排序 10000；运行时根物体保留到 DontDestroyOnLoad，用于跨场景覆盖画面。
- 电话：选中 `telephone → Tutorial Phone Call Controller`，修改 `Phone Call Fade Out Duration`、`Phone Call Fade In Duration`（默认均 0.7 秒）。进入与离开对白均使用这些时间。
- `Connection Pause Seconds` 是完全黑屏期间准备对白后的短暂停顿。
- 出口：选中 `Tutorial_ExitDoor → Tutorial Exit Door`，修改 `Scene Fade Out Duration`（0.8 秒）、`Scene Fade In Duration`（0.7 秒，新场景显现）。
- 电话与场景切换使用同一个 ScreenFade。PhoneCallVisual 保持独立：电话对白阶段为不透明黑色背景，只在 ScreenFade 完全不透明时启用/关闭。不会同时用两套 Tween 修改同一个 Alpha。
- 原 `PhoneCallVisual.Fade Out Seconds` 是旧的局部淡出参数，本次电话流程不再调用；现在统一调整上述 Phone Call Fade 参数。

## 电话时序

拨号成功立即锁定输入，但拨号 UI 保持显示。ScreenFade 逐渐遮黑后，隐藏拨号 UI，进入 Story，启用 PhoneCallVisual 并准备第一行。ScreenFade 淡入期间禁用对白推进，淡入完成才允许点击。
最后一句点击完成时保留画面，ScreenFade 遮黑后才隐藏对白、关闭 PhoneCallVisual、恢复正常第一人称镜头和身体。淡入完成后再恢复移动、Look、普通交互，标记 Completed 并触发 OnTutorialPhoneCallCompleted。

## 门交互与 Choice

- `Tutorial_ExitDoor → TutorialExitDoor` 继承现有 WorldInteractable，由原 PlayerInteractor 接收 E，InteractionPromptUI 显示 `【E 开门】`。
- 使用已有 DialogueController 和 DialogueRuntimeUI 的 Choice 布局。新增通用 `DialogueChoiceOption`（Label、Interactable）及托管 Choice 入口，未复制 UI 系统。
- Tutorial 门没有 DoorInteractable，也不调用 PlayerDoorInteractionController；只打开 Choice，没有门或手部开门动画。Test 普通门代码保持原行为。
- 两个选项相关文本：`Tutorial Exit Door → Stay Label / Locked Destination Label / Unlocked Destination Label`。
- 禁用外观：`TutorialDialogue → Dialogue Runtime UI → Disabled Choices → Disabled Choice Text Color / Disabled Choice Background`。
- 禁用项保持可见；Button.interactable=false，Controller 的数字选择和点击回调也检查 Interactable，不能通过 Hover、Submit 或数字快捷键绕过。
- Esc 与选择“留在房间再看看”都关闭选项并恢复玩家控制。
- 完成状态保存在现有 `telephone → TutorialPhoneCallController.Completed`。门引用 `Phone Progress`，打开时读取该值，并监听 OnTutorialPhoneCallCompleted；同一组选项数据更新标签和可用状态，不需要重新加载。
- 本阶段状态仅在当前场景实例生效，未新增存档系统。

## 更换目的地

`Tutorial_ExitDoor → Tutorial Exit Door → Destination Scene` 当前为 `Test`。
正式场景完成后，修改该字段，并将对应场景加入当前 Build Profile 的 Scene List 且启用。
显示给玩家的文字独立配置于 `Unlocked Destination Label`。
现有 SampleScene 保留，已加入并启用 Tutorial 与 Test。实际加载 Test 已通过，目标无效时会保留 Choice 和当前场景，Console 提示修正配置。

## 文件

新增：ScreenFade.cs、TutorialExitDoor.cs、DialogueChoiceOption.cs、TutorialFadeDoorBuilder.cs。
修改：TutorialPhoneCallController.cs、TelephoneDialController.cs、DialogueController.cs、DialogueRuntimeUI.cs、Tutorial.unity、EditorBuildSettings.asset。
配置菜单：Tools / Detective Game / Configure Tutorial Fade and Exit Door。
修改前当前场景副本：`.tmp-wakeup/Tutorial-before-fade-door.unity`。
未制作或配置任何音效。

## 实测

Play Mode 使用 E 键、鼠标拖动拨号和 UI 点击完成：门前锁定选项 → 点击留在房间 → 拨出 501107 → 黑屏转场 → 点击完成 37 页剧情 → 淡入回房间 → 门前目的地解锁 → 点击出发 → 完全黑屏加载 Test → 淡入并恢复输入。
通过项包括禁用选项拦截鼠标与键盘、拨号 UI 保持到全黑、电话淡变期间持续锁定、门位姿未改变、目的地即时解锁、跨场景仅保留一个 Fade。Console 无错误。
记录：`.tmp-wakeup/TutorialFadeDoorPlayModeTest.txt`。

补充回归：Test 普通对白开始/取消正常，普通门实际打开且角色动画结束；房门 Choice 的 Esc 取消与 Dialogue 组件关闭均能释放输入锁。
