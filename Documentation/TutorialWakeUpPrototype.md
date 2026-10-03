# Tutorial 起床开场 Prototype

已接入 `Assets/Scenes/Tutorial.unity`，继续使用已有 WakeUp FBX、Timeline、角色根节点、镜头和输入恢复逻辑。

最新流程：**黑屏 + 日期 → 第一次鼠标左键点击 → 黑屏渐开、日期渐隐、故事字幕渐显，同时坐起 → 坐姿停留等待 → 第二次鼠标左键点击 → 字幕渐隐，同时下床站起 → 站稳后交还第一人称控制**。

## 两次点击与两段动画（当前版本）

选择场景 `TutorialWakeUp`，编辑 `TutorialWakeUpController`：

- **Date Subtitle**：`1958年9月9日`。
- **Story Subtitle**：`没有意外的话，` 与 `今天将是我入职阿什福德侦探事务所的日子。`，已保留显式换行。两个独立文本字段直接在 Inspector 修改。
- **Sitting End Time = 1.8**：源 Timeline 中完成上半身仰起、尚未转身的时间，单位秒。第一段是源 0–1.8 秒（仅仰起）；第二段是源 1.8–5.6 秒（转身、下床、站起）。未改 FBX，也未切断原 Timeline 的镜头/根位移/Idle 混合轨道。
- **Sitting Up Duration = 3.2**：第一段实际播放时间，单位秒。
- **Standing Up Duration = 4.0**：第二段实际播放时间，单位秒。
- **Playback Speed = 1**：同时乘到两段播放速率；2 倍会使上述两个阶段实际时长各减半。
- **Black Fade Duration = 2.25**：第一次点击后的黑屏渐开时长。
- **Date Fade Out Duration = 1.0**：第一次点击后的日期渐隐时长。
- **Story Fade In Duration = 1.4**：第一次点击后的故事字幕渐显时长。
- **Story Fade Out Duration = 0.8**：第二次点击后的字幕框整体渐隐时长。
- **Minimum Sit Hold = 0.25**：动画到坐姿、第一轮渐变全部结束后，至少停留多久才接受新的第二次点击。之后会无限等待，不会自动站起。可调大用于增加阅读停顿。

以上参数都在同一个控制器 Inspector 中。改变 Sitting End Time 只改变源动画的暂停/续播位置；改变两段 Duration 只改变播放速率。修改分段点时建议围绕 1.8 秒微调，确认臀部、脚、SitAnchor 和镜头一致。坐起完成后若渐变时间更长，会保持坐姿直到渐变结束，再进入 WaitingToStand；站起结束后若字幕淡出更长，会保持最终站姿直到字幕消失才恢复控制。

状态由 `TutorialWakeUpController.cs` 管理：`Date → SittingUp → WaitingToStand → StandingUp → Finished`。`AdvanceIntroduction()` 是两个点击共用入口；仅 Date 和已满足最短停留的 WaitingToStand 接受点击。SittingUp/StandingUp 中的连续点击直接忽略，不缓存；同一帧的重复调用也忽略。此次节奏不使用 Esc/手柄 Start 跳过快捷键。保留统一 CompleteOpening 恢复路径与可供后续系统调用的 StandingUp SkipOpening API。

Timeline 没有 Stop/Play 重启：图持续处于 Playing、根速度为 0，由控制器逐帧设置源时间并 Evaluate；到 1.8 秒精确夹住时间，骨骼/镜头/根节点继续保持求值，因此不会被正常 Idle 覆盖，也不会暂停后重新从躺姿开始。第二次点击从同一源时间向后推进。全部阶段沿用一个 Cinematic 输入所有者，直到最后统一恢复移动、Look、Interaction、Animator、碰撞与正常第一人称镜头。

## 当前黑屏和字幕 UI

- `TutorialWakeUp/WakeUpEyelids`：原 Canvas 与 CanvasScaler，父 CanvasGroup 恒为 1；字体/布局样式沿用。
- `.../Black`：黑色 Image + 独立 CanvasGroup，控制黑屏透明度。
- `.../IntroTextPanel`：底部字幕背景 + 独立 CanvasGroup，控制第二次点击后的整框淡出。
- `.../IntroTextPanel/DateText`：沿用原 IntroText 对象并改名，日期颜色 alpha 控制渐隐。
- `.../IntroTextPanel/StoryText`：复制原文本样式的第二个 Text，故事颜色 alpha 控制渐显。编辑状态默认隐藏，运行时由控制器启用。

文字内容在控制器的 Date Subtitle / Story Subtitle 修改；UI 对象的 Text 内容仅是编辑预览，会被控制器覆盖。字号、字体、颜色、对齐、内边距在 DateText/StoryText 修改；背景、大小与位置在 IntroTextPanel 修改。默认字体在 Windows 运行时采用 Microsoft YaHei UI / Microsoft YaHei，以支持中文；设计师分配的自定义 Font 资源优先。黑屏与字幕已分开透明度，黑屏淡完字幕仍可保留。

实际 Play Mode 验证：日期阶段时间为 0、躺姿稳定；第一次点击四项并行变化；早期连续点击没有跳过坐起；仰起姿态源时间精确保持 1.8 秒，额外观察一秒根节点/臀部漂移为 0；第二次点击从该点继续起身并淡出字幕；全程控制锁定，结束后移动/Look/Recall/碰撞恢复，镜头位置/旋转/FOV 误差为 0。实际注入 Move/Look 均有效，Unity Console 无错误。截图和记录在 `.tmp-wakeup/TwoStage-Date.png`、`TwoStage-Sitting.png`、`TwoStageSmokeTest.txt`。WakeUp.fbx 内容哈希与修改前一致。

## 1. 实际资源与层级检查

原始角色资源是 `Assets/StarterAssets/ThirdPersonController/Character/Models/Armature.fbx`，Avatar `ArmatureAvatar` 为有效标准 Humanoid。

`Tutorial_PlayerRig` 是组织容器；`PlayerArmature` 持有 Animator、CharacterController、ThirdPersonController、StarterAssetsInputs、PlayerInput、FullBodyViewController 与 RecallInteractor。MainCamera、PlayerFirstPersonCamera 和原第三人称相机仍在原容器下，PlayerCameraRoot 仍在 PlayerArmature 下。没有重新 Parent，也没有添加 WakeupMotionRoot。

原场景把整个容器旋转成躺姿，会同时旋转碰撞与主相机。本实现把该容器恢复为正常直立组织坐标，仅在开场期间移动 PlayerArmature 根节点，躺下/坐起/转身由 Humanoid 骨骼承担；模型与碰撞始终共用玩家根节点。原始 FBX、Prefab、Avatar、Animator Controller 均未修改。

## 2. Blender 修改和文件

- 可编辑文件：`E:/Detective Game/美术资产/制作/Tutorial/TutorialWakeUp.blend`。
- 独立工作 Scene：`TutorialWakeUp_Work`，骨架对象 `Skeleton`，Action `WakeUp`。
- 30 fps，帧 1–169，时长 5.6 秒，保留阶段 Timeline Markers。
- 基于原始 FBX 导入，在原骨架的 Pose Bones 上制作 Hips/Spine/Chest/Head/手臂/腿部关键帧；没有改骨骼结构、蒙皮或 Rest Pose。
- 简单两节腿解算生成烘焙关键帧；脚到地面后保持基本脚掌朝向。手撑床只做粗略姿态。
- `WakeUp_ReferenceMattress` 仅用于制作时参考，不导出。
- 为匹配源 Humanoid Avatar 的 FBX 层级，独立导出副本使用 `Mannequin(Clone)` / `Geometry` / `Mannequin_Mesh` 名称与组织层级。原始项目资源保持不变。
- 导出文件：`Assets/Tutorial/Animations/WakeUp.fbx`。
- `Tools/Art/BuildWakeUp.py` 是原型生成脚本。它重新生成/覆盖独立工作文件中的 WakeUp 关键帧；手工精修后不要再次运行它。现已使用外部制作目录，并要求显式传入 `--overwrite` 才能覆盖已有动画；可用 `--project-root`、`--source-dir` 指定目录。日常修改应打开外部 `.blend` 精修并导出，不要重跑生成器。
- 用户先前打开的 `E:/Detective Game/character.blend` 未覆盖；原有 Blender Scene 保留在新工作文件中。

## 3. Unity 新增/修改

新增脚本：

- `Assets/Scripts/Tutorial/TutorialWakeUpController.cs`：黑屏介绍/翻页/淡出/锁定/播放/跳过/统一恢复。
- `Assets/Scripts/Tutorial/TutorialWakeUpMotionTrack.cs`：Timeline 自定义轨道与 Mixer。
- `Assets/Scripts/Tutorial/TutorialWakeUpMotionAsset.cs`：可序列化的根节点、镜头与眼睑曲线。
- `Assets/Editor/Tutorial/TutorialWakeUpBuilder.cs`：导入与初次创建工具；已经创建的开场请直接编辑，Build Opening 会拒绝覆盖现有开场。

现有脚本的小范围扩展：

- ThirdPersonController 新增 `ResetMotionAfterCinematic()`，清速度/转向惯性/下落与跳跃状态，复位 Idle 参数。
- FullBodyViewController 新增相机/眼高访问接口和 `SnapFirstPersonLook()`；保持原第一人称身体显示与移动机制。
- GameInputModeController 的 enum 增加 `Cinematic`；使用现有输入所有权锁定流程。

场景新增 `TutorialWakeUp`：PlayableDirector、TutorialWakeUpController，以及子对象 Anchors、WakeUpOpeningCamera、WakeUpEyelids/Black。没有删除原场景对象。

两个点击及动画阶段期间均禁用正常移动与 CharacterController，停用 PlayerInput，并停用 RecallInteractor/BasicRigidBodyPush；FullBodyViewController 保持启用，但通过已有视角锁接口停止 Look。Timeline 初始姿态在第一次渲染前求值，眼睑在 Awake 置黑。结束时统一停止 Timeline、对齐 StandAnchor、恢复原 Animator/Idle、重置移动状态、恢复碰撞/输入/交互与正常镜头。

## 4. Timeline 和动画位置

Timeline：`Assets/Tutorial/Timeline/TutorialWakeUp.playable`，绑定场景 `TutorialWakeUp` 的 PlayableDirector。

轨道：

- `WakeUp — Humanoid / Idle handoff`：WakeUp 动画与原项目 Idle 的最后 0.35 秒混合。
- `Anchors + independent first-person camera`：根节点位移、独立第一人称镜头轨迹；黑屏淡出由控制器管理。
- `Future: breathing / bed / footsteps (empty)`：保留空音轨，便于扩展。

动画 Clip 是 `Assets/Tutorial/Animations/WakeUp.fbx` 内的 `WakeUp`，已验证 `humanMotion = true`；Rig 为 Humanoid，Copy From Other Avatar 指向原角色有效 Avatar。这个 Clip 可以驱动其他有效 Humanoid Avatar。

## 5. 三个参考点

场景路径：`TutorialWakeUp/Anchors/LieAnchor`、`SitAnchor`、`StandAnchor`。

依据实际床垫与枕头边界放置：床垫中心约 (-1.209, 0.46, -0.840)，床垫顶面 0.57，枕头中心约 (-1.209, 0.63, -1.83)。

- LieAnchor：(-1.209, 0, -0.90)，Yaw 0。
- SitAnchor：(-0.754, 0, -0.90)，Yaw 0。
- StandAnchor：(-0.134, 0, -0.90)，Yaw 90。

它们表示玩家根节点的地面参考坐标，不是头/臀的世界位置。臀部和头部高度由骨骼动画给出；不要为了把 LieAnchor 放在床垫表面而再加一次床高。

## 6. 修改躺姿、坐起、下床、站起

Blender 打开工作文件，切换 `TutorialWakeUp_Work`，选择 Skeleton → Pose Mode → Dope Sheet/Action Editor → WakeUp。调整姿态并插入关键帧。阶段参考：

- 0–0.7 秒（帧 1–22）：躺姿、呼吸、轻微抬头。
- 0.7–1.8 秒（帧 22–55）：上半身撑起。
- 1.8–2.8 秒（帧 55–85）：转向床沿坐起。
- 2.8–3.5 秒（帧 85–106）：双腿下床、脚掌落地。
- 3.5–4.0 秒（帧 106–121）：前倾移重心。
- 4.0–4.85 秒（帧 121–147）：站起。
- 4.85–5.25 秒（帧 147–159）：站稳。
- 5.25–5.6 秒（帧 159–169）：与原 Idle 混合。

导出时仅选工作角色的 Skeleton、Mannequin(Clone)、Geometry 和 Mannequin_Mesh；FBX Bake Animation 开启，NLA Strips / All Actions 关闭，Add Leaf Bones 关闭，保持 -Z Forward / Y Up。输出到独立 WakeUp.fbx，不覆盖源 Armature.fbx。Unity 会自动重新导入；保留现有 .meta 的 Humanoid 设置。

## 7. 整体速度和阶段停顿

整体速度：场景 TutorialWakeUp → TutorialWakeUpController → Playback Speed。默认 1 使用 Sitting Up Duration 3.2 秒与 Standing Up Duration 4 秒，0.8 会使两段播放各延长为原来的 1.25 倍；WakeUp 动画、镜头与位移同步；介绍黑屏淡出由独立 Black Fade Duration 参数决定。

阶段停顿：Blender Action 中移动/复制各阶段关键帧，脚落地/站稳处保留相同姿态形成停顿。同时在 Timeline 自定义 Motion Clip Inspector 中同步 lieToSit、sitToStand、cameraKeys 的时间。这里 time 为 0–1 的 Clip 归一化时间，而非秒。

若改变总长度，应一起修改 Timeline Fixed Duration、WakeUp/Motion Clip 的 Duration、最后 Idle 的起点与混合时长。末尾相机收敛逻辑目前从归一化时间 0.938 开始；大幅改动末尾阶段时，同步调整控制器 SampleMotion 中的该参数。

## 8. 开场相机运动

选择 TutorialWakeUp → 打开 Timeline → 选择自定义轨道上的 `WakeUp motion / eye trajectory` Clip。Inspector 的 Camera Keys 决定镜头轨迹：time 为归一化时间；anchor 0/1/2 对应 Lie/Sit/Stand；offset 为相对对应 Anchor 的世界坐标偏移；rotation 为世界 Euler。

镜头使用独立 `WakeUpOpeningCamera`，不绑定 Head Bone。起始看天花板，坐起时升高，床沿阶段看房间/略看地面，最后收敛到实际正常第一人称相机及其碰撞修正位置、Rotation 与 Lens。坐姿/前倾阶段已经给镜头增加前向和高度间距，避免占位身体遮挡。

第一次点击后的黑屏渐变在控制器 Black Fade Duration 中调整，独立于两段字幕渐变参数。未来音效可放到空 AudioTrack，后续剧情用控制器 On Completed 事件接入。

## 9. 最终站立位置

修改 `TutorialWakeUp/Anchors/StandAnchor` 的位置与 Yaw。自然播放和跳过都会准确落到该参考点，然后交还正常控制；CharacterController 开启后会按正常重力贴地，短暂厘米级地面沉降属于原移动系统的落地调整。

## 10. 正常第一人称眼高

`Tutorial_PlayerRig/PlayerArmature` → FullBodyViewController → First Person Lens → Eye Height，目前场景配置 1.5 米；Forward Offset 为 0.22 米，FOV 为 75。末尾镜头自动使用这些正常相机参数。中间阶段镜头 key 的高度仍需按正式角色单独调整。

## 11. 当前 Prototype 建议微调

优先手工改善手撑床的接触、手臂姿态、膝盖转向、脚掌接触与少量脚滑；目前是基础烘焙姿态，没有运行时 IK。床沿到站立的根位移曲线与脚部曲线可以进一步配合，增加重心/重量感。正式比例未定前无需精修。

第一人称镜头已避免明显身体遮挡，但建议在实际 Game View 检查各分辨率下的视野、身体边缘、镜头前向距离与看地角度。音效尚为空；目前睁眼是黑色遮罩渐开，不是眼皮形状。

镜头尾段和 Idle 混合已经处理连接；起床动画是粗略 Prototype，不能等同于完成的表演动画。

## 12. 正式角色替换与 Retarget

1. 把正式模型作为新资源导入；Rig 设置 Humanoid / Create From This Model，Configure 并确认 Avatar 有效、T Pose/骨骼映射正确。
2. 保留现有 PlayerArmature 的玩家控制根节点、CharacterController、StarterAssets/输入组件与相机组织结构，替换其视觉模型/骨架。若必须换 Animator 所在对象，重新绑定 TutorialWakeUpController 的 Animator 和 Timeline Animation Track；不要只移动视觉模型而漏掉碰撞根节点。
3. Animator 使用正式角色 Avatar 和合适的正式角色 Idle Controller。WakeUp FBX 继续保留其源 Humanoid Avatar；Unity 会把该 Humanoid Clip Retarget 到目标 Avatar，不应把 WakeUp 源资源强制复制成不同骨骼命名的目标 Avatar。
4. FullBodyViewController 的 Full Body Renderer 与 First Person Body Mesh 更新为正式角色版本；头部隐藏/影子策略沿用项目当前逻辑，但需要为正式网格重新生成正确的第一人称身体网格。
5. Timeline 末尾 Idle 替换为正式角色 Idle，保持约 0.35 秒混合；确认 Timeline Animation Track 绑定新 Animator。
6. 按身高/四肢长度重新微调躺姿、撑床、落脚、根节点三参考点和 Motion 曲线，检查床接触。必要时在正式动画上增加 IK，而不是改原 Rig。
7. 调整 CharacterController 高度/中心、FullBodyViewController Eye Height，以及独立镜头中段 key。检查最后手动跳过与自然结束的镜头/位置一致性。
8. 再测首次进入 Tutorial、自然结束、Esc 跳过、WASD、鼠标 Look、碰撞、Recall 交互。

## 13. 原 WakeUp 资源及恢复路径验证记录

- 新 WakeUp Clip 导入为有效 Humanoid，长度 5.6 秒。
- Play Mode 实际经过躺/坐/脚落地/站起各阶段；开场中移动和 PlayerInput 均冻结。
- 中途跳过：StandAnchor 根节点误差 0；相机位置/旋转误差 0；Move/Look 均清零。
- 自然结束：CharacterController、移动、PlayerInput、RecallInteractor 恢复；主相机与正常第一人称相机位置、旋转、FOV 误差均为 0。
- 恢复后注入 Move 输入实际移动约 0.33 米；注入 Look 输入产生实际旋转。
- 最终镜头轨迹调整后重复自然结束/跳过/移动测试通过，Unity Console 未发现错误。
- 保存并重新打开 Tutorial：自定义资源、9 个镜头 Key、Animator/Motion Track 绑定、5.6 秒 Duration 均有效。
- 与任务开始时场景备份比较：原有序列化对象删除数为 0；原床/房间/已有交互对象未改动。原角色层级未重排。
- 本次没有运行独立 Windows Build 验证；验证范围是当前 Unity Editor 的 Play Mode。

## 14. 立即体验

打开 `Assets/Scenes/Tutorial.unity`，点击 Play。第一次鼠标左键点击同时坐起和渐变；坐姿等待后再次点击继续站起，完成后进入正常 Gameplay，再测试正常第一人称移动、鼠标视角与 Recall。当前 Unity 已退出 Play Mode，保留 Tutorial 编辑状态。

最新分段调整：前段只做上半身仰起，在源 1.8 秒等待，玩家根节点仍位于 LieAnchor，Yaw 为 0；床沿根位移曲线从 1.8 秒后开始，镜头等待位置保持朝前。第二次点击后才转身下床并起身，后段实际时长默认 4 秒。重复实际鼠标事件测试通过，暂停根节点/臀部漂移为 0，结束控制与相机恢复正确，Console 无错误。

