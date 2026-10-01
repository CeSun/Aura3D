---
section: notes
order: 3
---

# 节点与场景速查

**纯查表**：节点类型、`Node` / `Scene` / `PickResult` 成员、调试辅助层与枚举。用法见 [场景图与节点](./scene-and-nodes.md)。

> [!NOTE]
> 所有可渲染节点类型都在命名空间 `Aura3D.Core.Nodes`；`Scene`、`Grid`、`AxisGizmo`、`PickResult` 在 `Aura3D.Core.Scenes`。

## 节点类型总表

| 类型 | 作用 | 关键成员（仅示例，非全部） | 命名空间 | 教程 |
|---|---|---|---|---|
| `Camera` | 决定观察位置与投影 | `ProjectionType`、`FieldOfView`、`NearPlane`/`FarPlane`、`OrthographicSize`、`IsRenderBackground`、`View`/`Projection`/`ViewProjection`、`LookAt(Vector3)`、`FitToBoundingBox(bbox, padding)`、`SetClippingPlanes(near, far)`、`WorldToScreen(Vector3)` | `Aura3D.Core.Nodes` | [相机与视角控制](./camera.md) |
| `Light`（抽象基类） | 三种灯的公共部分 | `LightColor`、`CastShadow` | `Aura3D.Core.Nodes` | [光照与阴影](./lighting.md) |
| `DirectionalLight` | 平行光 / 太阳，方向由 `RotationDegrees` 决定 | `Irradiance`(lux，默认 80000)、`Intensity`(只读)、`ShadowConfig`(`Width`/`Height`/`NearPlane`/`FarPlane`) | `Aura3D.Core.Nodes` | [光照与阴影](./lighting.md) |
| `PointLight` | 点光源，向四周发光 | `AttenuationRadius`(默认 10f)、`LuminousIntensity`(cd，默认 1000)、`Intensity`(只读)、`SoftRatio`(默认 0.9)、`ShadowConfig` | `Aura3D.Core.Nodes` | [光照与阴影](./lighting.md) |
| `SpotLight` | 聚光，锥形光束 | `InnerConeAngleDegree`(默认 10)、`OuterConeAngleDegree`(默认 15)、`AttenuationRadius`、`LuminousIntensity`、`SoftRatio`、`ShadowConfig` | `Aura3D.Core.Nodes` | [光照与阴影](./lighting.md) |
| `Mesh` | 单个可渲染形状（几何 + 材质） | `Geometry`、`Material`、`BoundingBox`、`LocalBoundingBox`、`Model`、`IsSkinnedMesh`/`IsStaticMesh` | `Aura3D.Core.Nodes` | [加载与放置模型](./models.md) |
| `Model` | 导入模型的整棵子树 | `Meshes`、`Skeleton`、`AnimationSampler`、`BoundingBox`、`BoundingBoxPadding`、`CustomBoundingBox`、`Clone(CopyType)`、`IsSkinnedModel` | `Aura3D.Core.Nodes` | [加载与放置模型](./models.md) |
| `InstancedMesh` | GPU 实例化，一份几何画多次 | `FromMesh(mesh)`(静态)、`AddInstance(Matrix4x4)`、`UpdateInstance(i, transform)`、`RemoveInstance(i)`、`SetInstances(list)`、`InstanceCount`、`Material`、`SetAttributeEnabled(name, bool)`、`SetInstanceAttribute<T>(attr, count, data)`、`EnableFrustumCulling` | `Aura3D.Core.Nodes` | [实例化渲染](./instanced-rendering.md) |
| `InstancedMeshGroup` | HISM：八叉树分组 + 视锥剔除 | 构造 `InstancedMeshGroup(sourceMesh)`、`SourceMesh`、`MaxInstancesPerGroup`(默认 1024)、`MaxDepth`(默认 6)、`SetInstances(list)`、`AddInstance`/`AddInstances`、`UpdateInstance(i, transform)`、`Build()`、`InstanceCount`/`GroupCount`/`InPlaceUpdateCount`/`RebuildCount`、`IsBuilding` | `Aura3D.Core.Nodes` | [实例化渲染](./instanced-rendering.md) |
| `ParticleSystem` | CPU 模拟的粒子发射器集合 | `Emitters`、`Play()`、`Stop()`、`Pause()`、`IsPlaying`、`ActiveCount`、`MaxParticles`、`CustomBoundingBox`、`EnableVisibilityCulling` | `Aura3D.Core.Nodes` | [粒子系统](./particle-system.md) |
| `BoneAttachment` | 把节点钉到某根骨骼上 | `Mesh`(须蒙皮网格)、`BoneName`、`LocalOffset`、`NormalizeScale` | `Aura3D.Core.Nodes` | [动画系统](./animation.md) |

## Node 基类常用成员

`Mesh`、`Model`、`Camera`、`Light`、`InstancedMesh`、`InstancedMeshGroup`、`ParticleSystem`、`BoneAttachment` 都继承自 `Node`，共享下列成员。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Name` | `string` | 节点名，默认 `"Node"` |
| `Tags` | `HashSet<string>` | 标签集合，用于分组过滤 |
| `Position` | `Vector3` | 本地位置 |
| `Rotation` | `Vector3` | 本地旋转（弧度） |
| `RotationDegrees` | `Vector3` | 本地旋转（角度），与 `Rotation` 同步 |
| `RotationQuaternion` | `Quaternion` | 本地旋转（四元数），与上两者同步 |
| `Scale` | `Vector3` | 本地缩放，默认 `(1,1,1)` |
| `LocalTransform` | `Matrix4x4` | 本地变换矩阵，可读写 |
| `WorldTransform` | `Matrix4x4` | 世界变换矩阵，可读写（赋值会反算本地） |
| `Forward`/`Backward`/`Up`/`Down`/`Right`/`Left` | `Vector3` | 只读方向向量 |
| `Parent` | `Node?` | 父节点，只读 |
| `Children` | `IReadOnlySet<Node>` | 子节点集合，只读 |
| `CurrentScene` | `Scene?` | 所属场景，只读 |
| `Enable` | `bool` | 开关；置 `false` 连带子树都不渲染 |
| `AddChild(Node, AttachToParentRule)` | 方法 | 挂为子节点 |
| `RemoveChild(Node, AttachToParentRule)` | 方法 | 摘除子节点 |
| `GetNodesInChildren<T>()` | `List<T>` | 递归收集子树中 `T` 类型节点（含自身） |
| `BeginTransformUpdate(UpdateTransformMode = All)` | `IDisposable` | 批量改变换，`using` 结束时一次性重算 |
| `Update(double delta)` | 虚方法 | 每帧回调，`Model`/`ParticleSystem`/`InstancedMeshGroup`/`BoneAttachment` 等重写 |

## Scene 常用成员

| 成员 | 类型 | 说明 |
|---|---|---|
| `MainCamera` | `Camera` | 默认相机，建 `Scene` 时自动创建 |
| `MainDirectionalLight` | `DirectionalLight?` | 设为此光才启用 CSM 级联阴影 |
| `Background` | `OneOf<CubeTexture, Texture>` | 背景，纯色贴图或立方体贴图天空盒 |
| `Nodes` | `IReadOnlySet<Node>` | 顶层节点集合 |
| `RenderPipeline` | `RenderPipeline` | 当前渲染管线（可读其 `Settings`） |
| `PipelineSettings` | `PipelineSettings` | 管线配置对象 |
| `AddNode(Node)` | 方法 | 加入顶层节点（连带其子树） |
| `RemoveNode(Node)` | 方法 | 移除顶层节点（仅根节点） |
| `Pick(float x, float y, Camera?)` | `List<PickResult>` | 屏幕坐标拾取，按距离排序；`camera` 省略则用主相机 |
| `PickClosest(float x, float y, Camera?)` | `PickResult?` | 最近一次命中 |
| `ShowGrid` | `bool` | 显示/隐藏地面网格 |
| `ShowAxisGizmo` | `bool` | 显示/隐藏坐标轴 |
| `Grid` | `Grid` | 地面网格配置对象 |
| `AxisGizmo` | `AxisGizmo` | 坐标轴配置对象 |
| `MeshOctree` | `Octree<Mesh>` | 网格空间索引，拾取与剔除的粗筛 |

## PickResult 成员

`Scene.Pick` / `Scene.PickClosest` 返回。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Node` | `Node` | 命中的节点（模型部件命中时返回其所属 `Model`） |
| `InstanceIndex` | `int?` | `InstancedMesh` 实例索引；普通 `Mesh` 为 `null` |
| `Distance` | `float` | 命中点到相机的距离 |
| `WorldPosition` | `Vector3` | 命中点的世界坐标 |

## 调试辅助：Grid 与 AxisGizmo

`Scene.Grid` 与 `Scene.AxisGizmo` 是配置对象（**不是 `Node`**），用 `ShowGrid` / `ShowAxisGizmo` 开关。

| 对象 | 成员 | 类型 | 说明 |
|---|---|---|---|
| `Grid` | `Enable` | `bool` | 是否显示（由 `Scene.ShowGrid` 读写） |
| `Grid` | `Size` | `float`（默认 10.0f） | 半宽尺寸 |
| `Grid` | `Divisions` | `int`（默认 10） | 等分数 |
| `Grid` | `LineColor` | `Color` | 普通格线颜色 |
| `Grid` | `CenterLineColor` | `Color` | 中心轴线颜色 |
| `AxisGizmo` | `Enable` | `bool` | 是否显示（由 `Scene.ShowAxisGizmo` 读写） |
| `AxisGizmo` | `AxisLength` | `float`（默认 1.0f） | 轴长 |
| `AxisGizmo` | `ArrowheadSize` | `float`（默认 0.15f） | 箭头大小 |

> [!TIP]
> 要看灯的范围球、相机视锥、包围盒、骨骼线这类可视化，用 `PipelineSettings.Debug.*`，见 [选择与配置管线](./pipelines.md)。

## 相关枚举

| 枚举 | 成员 | 用途 |
|---|---|---|
| `AttachToParentRule` | `KeepWorld`、`KeepLocal` | `AddChild`/`RemoveChild` 时决定保持世界还是本地 |
| `UpdateTransformMode` | `Local`、`World`、`ChildrenWorld`、`All` | `BeginTransformUpdate` 指定重算范围（位标志） |
| `ProjectionType` | `Perspective`、`Orthographic` | `Camera.ProjectionType` |

## 交叉链接

- 用法与逐个节点讲解：[场景图与节点](./scene-and-nodes.md)
- 几何体 / 自定义几何 / 材质通道：[加载与放置模型](./models.md)、[材质与贴图](./material.md)
- 排障（黑屏、看不见、变换不刷新）：[常见坑与排障](./troubleshooting.md)
- 源码：[Node.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/Node.cs)、[Scene.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Scenes/Scene.cs)
