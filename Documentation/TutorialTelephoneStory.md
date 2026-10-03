# Tutorial 电话：实验拨号与 Ink 剧情

## Inspector 操作

选中 Tutorial 场景中的 `telephone`，展开 `Rotary Dial Controller`：

- `Use Experimental Finger Stop`：开启孔位选择模式；取消勾选立即恢复原来的释放角度识别模式。切换从下一次按下开始生效。
- `Finger Stop Angle`：固定挡杆在转盘初始平面中的极角，当前 220°。孔位中心极角 = 此角度 − 对应数字 Target Angle；使用捕获的初始旋转构建坐标，不读取旋转后的欧拉角。
- `Digit Hole Radius`：孔位环半径，当前 0.045（模型局部单位）。
- `Digit Hole Hit Radius`：每个孔的点击容差半径，当前 0.010。不是旧模式的 Angle Tolerance。
- `Finger Stop Enabled`：开关微小吸附与碰撞回弹反馈；关闭后仍保留安全硬限位，不能转穿挡杆。
- `Stop Snap Strength`：距离目标多少度以内吸附，当前 1.5°。
- `Stop Bounce Amount / Duration`：极小回弹幅度与时长，只向限位以内运动，不抖动屏幕。
- `Drag Sensitivity`：鼠标拖动灵敏度。
- `Return Speed / Return Curve / After Digit Pause`：自动回弹速度、曲线与登记前停顿。

数字在鼠标按下时锁定。拖动采用初始平面中连续 `DeltaAngle` 累积；逆向拖动不转动转盘，也不能靠来回晃动累加正向角度。到达目标并松开后，回到初始姿态才登记一次。未到挡杆松手不登记。

## 角度数据

Project：`Assets/Tutorial/Telephone/TutorialTelephoneDialProfile.asset`。
Inspector：`Digits → Digit / Target Angle / Angle Tolerance`。

本次没有更改任何数字角度：0=340°、1=70°、2=100°、3=130°、4=160°、5=190°、6=220°、7=250°、8=280°、9=310°。
实验模式每个数字的最大行程就是该 `Target Angle`。`Maximum Angle` 仅用于旧模式；不会让实验模式所有数字使用同一行程。
旧模式释放时使用 `Angle Tolerance`；实验模式使用“按下选孔 + 到达止挡”，不会在释放时重新猜数字。
自动回溯和手动拨号继续引用同一个 Profile。调整 Profile 后沿用原有 Inspector 烘焙流程更新回溯动画。
当前孔位依据共享角度与挡杆反算，因此修改 Target Angle 同时会移动该孔的检测中心；调整后应在 Scene 视图核对孔位。

## 修改剧情

文件：`Assets/Dialogue/TutorialTelephoneCall.ink`。
可以用 `E:\ink\Inky_windows_64\inky.exe` 打开这个文件，也可以用文本编辑器。
保存后 Unity 的现有 InkImporter 会自动编译，无需手动导出另一份 JSON。

每一行正文是一页，点击现有对话框（或空格/回车）进入下一页：

```ink
您好，请问是雷明顿小姐吗？ # speaker:未知人士 # type:Dialogue
这么快？ # speaker:克莱拉·雷明顿 # type:InnerThought
-> END
```

37 页已按原文 `/` 拆分；8 页为 InnerThought。
自我介绍本页 speaker 仍是“未知人士”，下次对方发言的 speaker 为“艾德蒙·阿什福德”。姓名切换由数据决定，不在脚本中按句子位置硬编码。

复用 `DialogueController`、`DialogueRuntimeUI` 和 InkFile。InnerThought 以姓名后缀“（心声）”、淡蓝灰色及斜体区别；选中 `TutorialDialogue → Dialogue Runtime UI → Inner Thought` 可调整。普通 Test 对话仍使用原有入口。

## 场景与流程

- `telephone → Tutorial Phone Call Controller`：`Phone Dialogue` 指向 Ink 文件，`Connection Pause Seconds` 调整接通停顿；`On Tutorial Phone Call Completed` 接 Tutorial 后续事件。
- `TutorialDialogue`：复用的 DialogueController / DialogueRuntimeUI / InvestigationKnowledgeService。
- `PhoneCallVisual → BlackOverlay_ReplaceWithSilhouette`：独立黑屏画面；未来替换此子物体为剪影。Canvas 排序 4600，现有 Dialogue UI 排序 4700。
- `PhoneCallVisual → Phone Call Visual → Fade Out Seconds`：剧情结束黑屏淡出时长。
- `telephone → Telephone Dial Controller → Camera Exit Seconds`：镜头返回时长。

正确号码仍读取 `telephone → Telephone Dial Recall → Phone Number`，没有新增另一份号码。
流程为 Success → Connecting → Dialogue → FadingOut → Returning → Completed。
电话从拨号开始到镜头返回始终持有同一份输入锁；对话不会提前释放它。Success 后禁止 Esc / Backspace / 再次转盘，托管对白不允许 Esc 取消或打开调查面板。结束事件只在镜头返回并恢复控制后触发。
完成标记属于本次场景实例，阻止本次 Tutorial 重复触发；目前未接存档，重新载入场景会重置。

没有制作或配置新音效，已取消原拨号成功分支的音效播放。

## 变更文件

修改：`RotaryDialController.cs`、`TelephoneDialController.cs`、`DialogueController.cs`、`DialogueRuntimeUI.cs`、`Tutorial.unity`。
新增：`TutorialPhoneCallController.cs`、`PhoneCallVisual.cs`、`TutorialPhoneCallBuilder.cs`、`TutorialTelephoneCall.ink`。
没有改动电话模型、原有角度 Profile、回溯动画、Outline 或交互提示系统。
配置工具：`Tools → Detective Game → Configure Tutorial Phone Story`，只挂接电话剧情，保留原拨号配置。
场景修改前副本：`.tmp-wakeup/Tutorial-before-phone-story-20260926.unity`（包含当时未保存的场景内容）。

## 验证结果（2026-09-26）

完整 Play Mode 测试通过：从电话前实际 E 输入进入，注入鼠标按住/顺时针拖动/释放事件完成拨号，逐页点击全部 37 页，确认黑屏淡出、镜头位置/旋转/FOV 对齐、移动/视角/交互恢复，以及事件单次触发和阻止重复交互。
额外覆盖：0–9 每个孔、未到底松手、逆向拖动、每数字硬限位、释放后回弹才登记、Backspace、旧模式回退、成功后的 Esc 屏蔽、逐页姓名/正文/心声标签匹配。Unity Console 无错误。
原 Test Ink 用普通 BeginDialogue 入口开始与取消、输入恢复也已回归通过。
测试记录：`.tmp-wakeup/PhoneStoryPlayModeTest.txt`、`.tmp-wakeup/PhoneNormalDialogueRegression.txt`。

选中电话并开启 Scene Gizmos，可看到青色数字孔命中圈与黄色挡杆参考线；此辅助只存在 Editor，不改变模型或游戏画面。
新增 Editor 辅助脚本：`Assets/Editor/Tutorial/RotaryDialControllerEditor.cs`。
建议手动微调：实际鼠标拖动灵敏度、孔位点击半径、止挡吸附与微小回弹、接通停顿、黑屏淡出。之后正式接存档时，将 Completed 状态纳入 Tutorial 进度数据。

镜头返回修复：在第一人称模式下，退出电话前重新读取 `FullBodyViewController.FirstPersonCamera` 的实际位置、旋转与 FOV，避免进入电话时恰逢 Cinemachine 过渡而保存到中间帧。返回动画沿用原有 FixedViewCameraTransition。
