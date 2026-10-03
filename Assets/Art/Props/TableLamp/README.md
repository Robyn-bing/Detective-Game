# TableLamp

拖动 `Prefabs/TableLamp.prefab` 到场景即可摆放。底部中心为根枢轴，尺寸约 0.1246 × 0.3270 × 0.1246 米（Unity XYZ），保留当前 Blender 文件中用户调整后的大小。

- Models/SM_TableLamp.fbx：3,892 三角面，两套 UV，三个材质槽。
- Materials：金属和灯泡不透明；灯罩 Alpha=0.2、双面透明，关闭灯罩材质的 ShadowCaster Pass。
- Textures：2K BaseColor、Normal、MetallicSmoothness；后者 R=金属度、A=光滑度，按线性数据导入，开启 Mip Maps。
- BulbLight_Off：默认关闭的点光源。开启照明时再调整亮度、范围和阴影；灯泡材质 Emission 也默认关闭。
- 未添加碰撞或交互脚本，未将实例加入场景。

复查结果：无超过两个面共边、无零面积面；72 条开放边界保留。相距约 0.054 毫米的一对顶点位于不同结构，未强行焊接。Blender 辅助灯已重新对齐当前灯泡中心。

Unity 已验证面数、尺寸、UV2、三个 URP 材质映射及 BaseMap 引用。透明排序、光照烘焙及目标设备性能需结合实际场景检查。来源及署名见 Attribution.txt。
