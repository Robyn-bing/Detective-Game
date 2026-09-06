# 全身第一人称原型

## 使用

打开 `Assets/Scenes/Test.unity`，选择 Hierarchy 中的 `PlayerArmature`。
在 `Full Body View Controller` 组件里切换 `Use First Person`：勾选为全身第一人称，取消为原第三人称。
Test 默认勾选。支持运行时切换；Play Mode 中修改此开关不会自动保存到场景。

点击 Game 窗口后，鼠标观察，WASD 移动，Shift 奔跑。
第一人称 A/D 横移、S 后退，身体朝向始终跟随水平视线。低头可见胸部、手臂、手和腿（可见范围随动画姿势变化）。
跳跃仍由 `Third Person Controller → Can Jump` 控制；Test 保持禁跳。

## 调整

`Full Body View Controller` 提供 Eye Height、Forward Offset、Field Of View、Near Clip Plane、Look Up/Down Limit、Mouse Sensitivity 和 Gamepad Look Speed。
当前模型使用眼高 1.66 m、前偏移 0.22 m、垂直 FOV 75°、近裁剪 0.04 m。前偏移使镜头位于胸部前方，避免低头看进领口。
镜头跟随角色根位置，不继承头骨动画；碰撞探测会在靠墙时缩短前偏移。大幅修改眼高、偏移、FOV 或碰撞层后请重新检查墙边和门框。

## 实现边界

- 保留原有 CharacterController、PlayerInput、StarterAssetsInputs 和 Animator。
- 原 `PlayerFollowCamera` 负责第三人称（原 FOV 30°）；新增 `PlayerFirstPersonCamera` 负责第一人称，两者通过 CinemachineBrain 共用 MainCamera。
- `Assets/Characters/Player/PlayerViewBlends.asset` 仅把这两台虚拟相机之间的切换设置为 Cut，避免镜头穿过身体。
- `Armature_FirstPersonBody.asset` 是编辑器生成的独立网格，剔除主要受颈部及其子骨骼影响的三角形；UV、材质槽、绑定姿势与其他顶点属性保留，未修改原始 FBX 或其 Read/Write 设置。
- Play Mode 中生成 `Geometry/Armature_Mesh/FirstPersonBody (Runtime)`，共用原骨骼和材质。第一人称中原模型只投射完整阴影，第三人称恢复原模型。
- 当前沿用 Starter Assets 的移动动画，横移和后退尚未配置专用动画，也未实现交互手部 IK、镜中完整角色或独立第一人称手臂动画。
- 新组件仅装配到 Test 场景的玩家实例，未改写其他场景或原始角色预制体。

## 回归验证

在 Test 的 Play Mode 下选择 `Tools → Detective Game → Run Full Body View Smoke Tests (Play Mode)`。
测试会临时停用设备输入，向现有 StarterAssetsInputs 注入移动和观察值；它验证控制器与相机集成，不代替真实键鼠/手柄的手感测试。
报告和实际 MainCamera 截图保存在项目 `Temp/FullBodyViewTests/`。
运行结束后建议退出 Play Mode 再重新进入，以恢复干净的初始状态。
