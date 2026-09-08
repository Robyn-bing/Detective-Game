# 物体回溯系统

## 已配置示例

`Assets/Scenes/Test.unity` 中的 `Study_Blockout/03_Desk/Desk_Book` 已配置为可回溯物体。
靠近并看向书本会显示 `[R] 回溯`，按 R 直接进入唯一的 `18:55–19:05` 回溯片段。
预览会在约 1.35 秒内快速倒放到最早状态，然后可以按空格播放/暂停、拖动时间轴或按 Esc 退出。

示例资产位于：

- 数据：`Assets/Recall/Data/Desk_Book_RecallData.asset`
- 动画：`Assets/Recall/Animations/Desk_Book_1855_1905_LeftToRight.anim`
- 预览 Prefab：`Assets/Recall/Prefabs/Desk_Book_RecallPreview.prefab`
- 预览网格：`Assets/Recall/Meshes/Desk_Book_RecallPreviewMesh.asset`

场景中的原书不会被移动。回溯系统会在原物体位置实例化预览 Prefab、暂时隐藏原物体，并继续渲染真实场景背景。MainCamera 会从玩家当前视角平滑移动到固定回溯视角，退出时再平滑返回玩家视角。进入回溯后玩家的视觉 Renderer 会暂时隐藏，因此第三人称身体不会挡住回溯物体；返回玩家视角后会按原状态恢复。

## 为其他物体配置回溯

### 1. 创建纯视觉预览 Prefab

选中场景物体后执行：
`Tools > Detective Game > Recall > Create Preview Prefab From Selected`。
工具会在 `Assets/Recall/Generated/` 下创建纯视觉 Prefab，并把场景内嵌/ProBuilder Mesh 复制成独立资产。
它会移除 Collider、Rigidbody 和 gameplay MonoBehaviour，但保留 Transform、Renderer、MeshFilter、SkinnedMeshRenderer 与 Animator。

也可以手动建立独立 Prefab。推荐结构：

```text
Object_RecallPreview       <- 动画根节点；位置动画应录在这里
└─ Visual                  <- MeshRenderer / SkinnedMeshRenderer 和可动画子物体
```

预览 Prefab 不应包含任务逻辑、交互脚本或动态物理。Collider 会在运行时禁用，Animator 也会在采样前禁用。
如原物体是场景内 ProBuilder 网格，先复制其 Mesh 为独立 `.asset`，不要让 Prefab 引用场景内嵌 Mesh。

### 2. 制作 AnimationClip 关键帧

打开预览 Prefab，选中动画根节点，打开 `Window > Animation > Animation`。
创建一个 AnimationClip，并确保：

- 0 秒是故事中的最早状态。
- 动画末尾是故事中的最新状态。
- 最新状态最好与场景物体当前外观一致。
- 关闭 Loop Time。

点击红色 Record，在时间轴上移动播放头后修改 Transform 或子物体属性，Unity 会自动创建关键帧。
连续移动可用 Auto/Linear 切线；突然变化使用 Constant 切线。
故事时间和动画长度是分开的：例如 3 秒动画可以代表 `18:55–19:05` 十分钟。

适合直接 K 帧的内容包括位置、旋转、缩放、书封/抽屉/门的角度、灯光强度，以及可动画组件的数值。
不要用 AnimationEvent 表示必须可逆的状态；倒放和拖动可能跳过事件。需要“突然出现/消失、换材质、换模型”时，应扩展为按时间采样的离散状态轨道。

### 3. 创建 RecallObjectData

在 Project 窗口右键：`Create > Detective Game > Recall Object`。
配置：

- Display Name：提示和回溯页面显示的名称。
- Preview Prefab：上一步创建的纯视觉 Prefab。
- Preview Camera Euler：预览观察角度。
- Preview Field Of View：预览镜头视野。
- Framing Padding：自动构图留白；物体超出画面时增大。
- Camera Entry Progress Curve：统一进入阶段中的镜头进度曲线。
- Camera Return Transition Seconds：退出时固定镜头返回玩家视角的时间。
- Background Color：保留字段；当前场景回溯模式会继续使用真实场景背景。
- Periods：回溯时间段数组。

每个 Period 配置：Label、开始/结束时分、Animation Clip、Synchronized Entry Seconds、Rewind Progress Curve、Playback Speed 和 Available。
Available 关闭或 Animation Clip 为空的时间段不会显示。只有一个有效时间段时直接进入；两个及以上时自动出现选择页面。

`Synchronized Entry Seconds` 是镜头过渡和物体回到最早状态共用的总时长，两者会在同一帧开始并结束。`Camera Entry Progress Curve` 只控制镜头在这段时间内如何移动；`Rewind Progress Curve` 控制物体如何倒放。两条曲线的横轴使用相同的归一化时间，因此可以分别调整手感而不会失去同步。

`Rewind Progress Curve` 的纵轴是已经完成的倒放进度；曲线越陡，物体当时移动越快。默认三个关键点为 `(0,0)`、`(0.5,0.5)`、`(1,1)`，首尾切线较陡、中间切线较平，形成“快—慢—快”。系统会把曲线的第一个和最后一个关键点自动映射到统一进入阶段的起点与终点，因此移动曲线端点也不会破坏同步。在 Inspector 中点击曲线即可拖动关键点或修改切线；建议保持曲线单调上升，避免物体意外来回跳动。

### 4. 配置场景物体

在场景物体上添加：

- 一个 BoxCollider 或其他 Collider，保证玩家视线可以命中物体。
- `RecallableObject`，把 Data 指向对应 RecallObjectData。

Interaction Anchor 通常指向物体自身；大型物体可创建一个空子节点，放在希望玩家注视的位置。
Interaction Distance 控制提示出现距离；Prompt Screen Offset 控制悬浮提示相对物体的屏幕偏移；Outline Width 与 Outline Pulse Speed 控制白边粗细和呼吸速度。玩家还必须把物体放在画面中心附近，并且视线没有被墙或其他 Collider 阻挡。书本示例的距离为 1.75 米，玩家上的 Viewport Focus Radius 为 0.12，因此附近多个物体会优先选择视线最集中的一个。

不需要再创建 RecallSessionController 或 UI；Test 场景中的 `RecallSystem` 是全场景共享实例。玩家上的 `RecallInteractor` 会自动发现所有启用的 RecallableObject。

## 多时间段

在同一个 RecallObjectData 的 Periods 中增加元素即可。每段使用独立 AnimationClip，系统会自动显示时间选择页。
方向键选择、Enter 确认，也支持鼠标点击。不要为同一个物体重复添加 RecallableObject。

## 回归测试

在 Test 场景进入 Play Mode，执行：
`Tools > Detective Game > Run Recall Smoke Tests (Play Mode)`。
报告写入 `Temp/RecallTests/report.json`。测试会验证倒放、精确拖动采样、播放/暂停、唯一 MainCamera 和退出后的状态恢复。
