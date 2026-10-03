# Tutorial 电话玩家拨号

场景：`Assets/Scenes/Tutorial.unity`。没有改变电话原模型、回溯预览 Prefab 或玩家父子层级。

## 已配置的对象与组件

- `telephone`：保留 `RecallableObject`、`TelephoneDialRecall`；新增 `TelephoneDialController`、`RotaryDialController`、`AudioSource`。
- `telephone/TEL_DialWheel`：仍是原转盘；通过其平面投射鼠标，无需新增碰撞体。
- `telephone/DialViewAnchor`：固定拨号镜头的位置和旋转。默认世界位置约 `(0.31, 1.38, 1.08)`。
- `PlayerArmature`：补上现有 `PlayerInteractor`，复用普通交互提示和 E 输入。
- `TelephoneDialUI/DialPanel`：六个 `DialDigitSlot_0…5`，每个都有 `Digit` Text 与 `Underline` Image；还有 `Instructions`。

## Inspector 调参

1. `Assets/Tutorial/Telephone/TutorialTelephoneDialProfile.asset`：十个条目的 `Digit`、`Target Angle`、`Angle Tolerance`。已从原回溯配置迁移：0=300°、1=30°、2=60°……9=270°；容差暂为12°。
2. `telephone > TelephoneDialRecall > Phone Number`：正确号码，目前501107；长度必须六位。这也是回溯自动拨号的号码。
3. `telephone > RotaryDialController`：Drag Sensitivity、Maximum Angle、Pointer Radius/Inner Radius（转盘本地单位）、Return Speed（度/秒）、Return Curve（0→1表示回弹完成进度）、After Digit Pause。
4. `telephone > TelephoneDialController`：Camera Entry/Exit Seconds、Dial Field Of View、Camera Progress、Connected Sound、On Dial Success。
5. `TelephoneDialUI > TelephoneDialUI`：Normal/Error/Focus Color、Error Shake Strength/Time、Focus Pulse Speed/Strength；Slots里可以重新引用六个槽位。
6. UI字体、字号、面板位置、槽位间距和下划线尺寸：直接编辑上述UI子对象的RectTransform、Text和Image。

角度目前延续回溯的粗略30°阶梯，请按模型上的数字与挡片人工微调。若目标角度超过310°，同步提高Maximum Angle。拖动时沿轮面顺时针画弧，反向拖动可以减少当前角度；不能低于0°。

## 状态与输入

`TelephoneDialController` 管理 Idle、EnteringDialView、Ready、DraggingDial、DialReturning、Validating、CorrectionMode、Success、Exiting。

靠近并看向电话时显示【E 拨号】，E后接管主相机并锁定普通玩家输入。角色Root/CharacterController留在原地；冻结Starter Assets移动与FullBodyViewController的Look，暂停普通互动与回溯输入，暂时隐藏身体。

`RotaryDialController`负责鼠标投射、拖动角度与回弹。松开时保存角度对应数字；仅在回弹归零且After Digit Pause结束后通过ReturnCompleted交付一次。角度不在任何容差范围内时归零后忽略本次拨号，不占槽位。回弹、验证和成功期间不能再次拖盘。

`TelephoneDialController.Backspace()`：Ready时删除最后一位；CorrectionMode时只清空当前错误槽，不移动其他数字或焦点。拖动、回弹和验证阶段不处理Backspace。

普通输入没有逐位正确性提示。六位填满后整体验证。错误槽的数字与下划线变红，并仅对错误槽轻微左右摇动。反馈结束后，焦点移到第一处错误；重新拨入数字后移到本轮下一处错误。替换单个数字时不判断它是否正确，本轮所有错误位置改完后才再次检查完整号码。如果仍然错误，继续新一轮修正。Backspace清空当前槽后，必须重新拨入才能继续。

所有阶段（包括进入镜头、回弹、验证、成功）可按Esc。先取消未提交转盘输入、隐藏UI、停止音效，平滑返回原相机，随后同一RestoreGameplay流程恢复相机FOV/裁剪面/CinemachineBrain、身体显示、玩家组件、输入与光标。禁用组件或切换场景时也走恢复清理。

## 回溯复用

`TelephoneDialRecall.AngleProfile`与玩家RotaryDialController引用同一资源，不复制角度。回溯仍使用原`Assets/Recall/Animations/Telephone_501107.anim`，原生成器按共享配置烘焙。请在非播放模式且打开Tutorial时修改共享角度资源，其自定义Inspector会自动重烘焙已加载电话的回溯AnimationClip；也提供手动更新按钮。只改容差不会改变回溯动作的目标角度。

回溯与拨号镜头现在共用`FixedViewCameraTransition.Apply`的位置、旋转、FOV插值；保留回溯的原进度曲线、重建效果与播放流程。`GameInputModeController`只增加枚举TelephoneDial，保留现有模式和独占所有者机制。`PlayerInteractor`、`RecallInteractor`补上统一模式检查，保证开场/拨号期间不能抢普通输入。

## 成功后的剧情接入

在`telephone > TelephoneDialController > On Dial Success`绑定UnityEvent，也可脚本注册`OnDialSuccess.AddListener(...)`。该事件只在完整号码正确后触发一次，成功后继续拨号被锁定。当前播放`Assets/Tutorial/Telephone/TelephoneConnectedPrototype.wav`占位接通音，可替换为正式音效。事件留给正式电话剧情；结束剧情后调用`ExitDialMode()`，或当前原型按Esc返回。

## 脚本与配置工具

新增运行时：`Assets/Scripts/Interaction/TelephoneDialController.cs`、`RotaryDialController.cs`、`TelephoneDialUI.cs`、`TelephoneDialProfile.cs`、`Assets/Scripts/Player/FixedViewCameraTransition.cs`。

新增编辑器：`Assets/Editor/Recall/TelephoneDialProfileEditor.cs`、`Assets/Editor/Tutorial/TutorialTelephoneBuilder.cs`。配置菜单`Tools > Detective Game > Configure Tutorial Telephone Dialing`只针对打开的Tutorial，复用已存在对象/资源，保留手动修改过的Anchor和UI样式。

修改：TelephoneDialRecall、TelephoneDialRecallEditor、RecallSessionController、RecallInteractor、PlayerInteractor、GameInputModeController；FullBodyViewController只增加只读InteractionLocked属性，不改变正常移动/镜头行为。

Play Mode证据记录在`.tmp-wakeup/TelephoneDialStrictTest.txt`以及TelephoneDial-Ready.png、TelephoneDial-Correction.png。需继续人工调整：精确数字角度/容差、回弹机械曲线、镜头构图、UI风格和正式电话音效/剧情。

附加测试：TelephoneDialCancelTest.txt记录六个阶段Esc和组件禁用清理；TelephoneSharedAngleTest.txt记录共享角度重烘焙与还原；TelephoneBoundaryRecallTest.txt记录起床开场互斥、Esc同帧优先及原回溯流程。所有这些验证均通过，Console零错误。


## 拨号方向、双提示与白边修正

电话的TelephoneDialRecall.ClockwiseAxis已统一为本地+Z。鼠标极角基准、玩家转盘旋转以及回溯烘焙都读取此轴；角度仍为正数，不需要反号或重新标定。回弹是从正角度退回0，因此视觉上逆时针回位。

普通游戏提示现在由PlayerArmature上的InteractionPromptUI统一管理。PlayerInteractor提供E动作，RecallInteractor提供R动作；同一物体的可用动作绘制在一个World Interaction Prompt面板内，R在上、E在下。两种焦点检测对带回溯的物体共用锚点、距离及焦点半径。RecallRuntimeUI保留回溯的时间选择与播放界面，不再叠加普通游戏R提示。按键只触发当前实际显示的物体动作。

运行时检查没有发现拨号再次创建Outline：telephone有四个原模型渲染器和四个对应Outline渲染器。原Shader在物体空间扩张，电话1.5倍缩放导致0.006的参数实际扩张到0.009米；透明壳相互重叠还会重复混色而增亮。修正为世界空间宽度，并使用URP预留的用户Stencil位阻止同一屏幕像素重复混色。保留原Outline Width和颜色/呼吸亮度参数。

InteractionPromptUI只维护一个高亮物体所有者；RecallableObject保存该物体唯一一组Outline，状态变化幂等。切换为仅R、仅E、或R+E时不会新增Outline。生成时跳过已有运行时Outline和属于其他交互根的渲染器，防止递归复制。

验证：`.tmp-wakeup/TelephoneThreeFixTest.txt`记录双提示、R/E各自触发、仅R/仅E保持高亮、0–9独立屏幕顺时针拖动、反向拖动拒绝、回弹及回溯方向测试。`.tmp-wakeup/TelephoneOutlineRenderingTest.txt`记录同一透明白边在重叠渲染前后的GPU比较。
