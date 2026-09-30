---
section: basics
order: 1
---

# 场景图与节点

在 Aura3D 里，你能放进画面的一切——相机、灯、模型、粒子——都是挂在同一棵树上的**节点（Node）**。这棵树就是**场景图（Scene Graph）**。这一页先用一屏讲清它怎么搭、怎么改、怎么找，再逐个告诉你每种节点是干什么的、最小怎么写、深入该看哪篇。

> [!TIP]
> 阅读顺序建议：先看完本页的「场景图」和「Scene」两节，把「往场景里放东西」这件事跑通，再按需跳到某一种节点的深入教程（每小节末尾都有链接）。

## 场景图：一棵 Node 树

每个节点都有自己的**本地变换**（位置 `Position`、旋转 `Rotation`/`RotationDegrees`/`RotationQuaternion`、缩放 `Scale`），以及一个由整棵树算出来的**世界变换**。规则只有一条：

> 子节点的世界变换 = 自己的本地变换 × 父节点的世界变换。

也就是说，**移动或旋转一个父节点，它下面所有子节点会一起跟着动**。这就是搭「会动的机器人」「绕圈转的行星系」这类结构的基础——你可以用一个空的 `Node` 当骨架，把东西挂上去，再只转这个骨架。

```csharp
// 一个空节点当"旋转中枢"，把两盏灯挂上去，只转中枢，灯就绕着它转
var orbit = new Node { Name = "LightOrbit" };
view.AddNode(orbit);

var point = new PointLight { LightColor = Color.Red, AttenuationRadius = 9f };
var spot  = new SpotLight  { LightColor = Color.Blue, AttenuationRadius = 12f };

// KeepLocal：保持它们相对中枢的位置不变（挂上去时用的就是各自的本地坐标）
orbit.AddChild(point, AttachToParentRule.KeepLocal);
orbit.AddChild(spot,  AttachToParentRule.KeepLocal);

// 之后每帧只要转 orbit 一个节点，两盏灯自然跟着绕圈（示例中每帧 +1 度）
orbit.RotationDegrees = new Vector3(0, orbit.RotationDegrees.Y + 1f, 0);
```

### 放进场景与移出场景

顶层节点用 `AddNode` 进场景、`Remove` 出场景。加了父节点之后，它的整棵子树会一起登记进场景，不用再逐个 `AddNode`。

```csharp
view.AddNode(mesh);      // 放进场景（等价于 view.Scene.AddNode(mesh)）
view.Remove(mesh);       // 从场景移除（会连带其子树）
```

### 挂子节点：KeepWorld 还是 KeepLocal

`AddChild(child, rule)` / `RemoveChild(child, rule)` 的第二个参数决定**挂/摘的瞬间，子节点看起来在哪**：

- `AttachToParentRule.KeepWorld`：子节点的**世界位置保持不变**——引擎反算它的本地变换去迎合父节点。适合「先把东西摆到世界某处，再随手挂到某个父节点下，不希望它跳位」。
- `AttachToParentRule.KeepLocal`：子节点的**本地变换保持不变**——它会立刻跟着父节点跑。适合「本来就打算让它按相对父节点的偏移摆放」。

```csharp
parent.AddChild(child, AttachToParentRule.KeepWorld);   // 看着没动，本地坐标被改写
parent.AddChild(child, AttachToParentRule.KeepLocal);   // 本地坐标不动，世界位置跟着父节点变

// 摘下来时同理：KeepWorld 让子节点停在原地，KeepLocal 直接改父引用
parent.RemoveChild(child, AttachToParentRule.KeepWorld);
```

> [!NOTE]
> `AddChild` 会做校验：不能把自己挂成自己的子节点、不能成环、一个节点不能同时有两个父节点、不能跨场景挂父子。命中任意一条都会抛异常。

### 一次改多个变换：BeginTransformUpdate

单独改 `Position`、`Rotation`、`Scale` 中的每一个都会立刻重算世界矩阵，并向下递归刷新整棵子树。如果你要同时改好几个属性，用 `BeginTransformUpdate()` 把它们包起来，只在 `using` 结束时重算一次：

```csharp
using (node.BeginTransformUpdate(UpdateTransformMode.All))
{
    node.Position = new Vector3(10, 0, 5);
    node.RotationDegrees = new Vector3(0, 90, 0);
    node.Scale = new Vector3(2f);
}   // 离开 using 时一次性重算世界矩阵
```

`UpdateTransformMode` 是位标志枚举，取值为 `Local` / `World` / `ChildrenWorld`（以及三者的组合 `All`，即默认）；只想刷新其中一部分时可传对应标志。**一定要用 `using`（或手动 `Dispose`）**，否则重算不会触发。

### 在子树里找节点：GetNodesInChildren

`GetNodesInChildren<T>()` 递归收集以自己为根、类型为 `T` 的所有节点（自身匹配也会包含）。模型的部件、子灯、子网格都靠它找：

```csharp
var allMeshes = model.GetNodesInChildren<Mesh>();     // 模型里所有网格
var allLights = scene.MainCamera.GetNodesInChildren<Light>();
```

### 用 Tags 打标记再过滤

`Tags` 是每个节点上的一个字符串集合，专门用来给节点分组打标签，配合 LINQ 过滤：

```csharp
mesh.Tags.Add("pickable");

var pickables = model.GetNodesInChildren<Node>()
    .Where(n => n.Tags.Contains("pickable"));
```

### 临时藏起来：Enable

`node.Enable = false` 让该节点**连同整棵子树**都不渲染、不参与更新；改回 `true` 恢复。做「显示/隐藏」开关用它，不用增删节点。

### 复制一份：克隆

节点树本身没有通用克隆接口；能整棵树复制的是**模型**。`Model.Clone(CopyType)` 复制层级结构，`CopyType.SharedResourceData` 只共享底层几何与贴图，最省内存，适合「把同一个模型摆很多份」：

```csharp
var another = model.Clone(CopyType.SharedResourceData);
view.AddNode(another);
```

克隆的共享边界、`FullCopy` 的区别见 [加载与放置模型](./models.md)。

## Scene：一切的容器

你在 `SceneInitialized` 事件里拿到的 `Scene`，就是那棵场景图的持有者。它管着顶层节点集合，并暴露几个你几乎每次都要用的成员：

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view  = (Aura3DView)sender;
    var scene = args.Scene;

    // 背景：纯色 / HDR 立方体贴图都赋给同一个属性
    scene.Background = Texture.CreateFromColor(Color.Gray);

    // 主相机：每个 Scene 自带一个，无需自己 new
    scene.MainCamera.Position = new Vector3(0, 4.2f, 12f);
    scene.MainCamera.LookAt(new Vector3(0, 1.6f, 0));

    // 主方向光：指定后，这盏方向光才启用级联阴影（CSM）
    var sun = new DirectionalLight { LightColor = Color.White, CastShadow = true };
    sun.RotationDegrees = new Vector3(-52, -34, 0);
    scene.AddNode(sun);
    scene.MainDirectionalLight = sun;

    // 拾取：把屏幕坐标打到场景里的物体上
    var hits = scene.Pick(screenX, screenY, scene.MainCamera);
}
```

| Scene 成员 | 作用 |
|---|---|
| `MainCamera` | 默认相机，建 Scene 时自动创建，可直接用 |
| `MainDirectionalLight` | 指定为 CSM 主光源的方向光；不指定则没有级联阴影 |
| `Background` | 场景背景，纯色 `Texture` 或 `CubeTexture`（天空盒） |
| `Nodes` | 场景当前登记的顶层节点集合 |
| `AddNode` / `RemoveNode` | 加入 / 移除顶层节点 |
| `Pick` / `PickClosest` | 屏幕坐标拾取，返回 `PickResult`（含命中节点、世界坐标、距离、实例索引） |
| `ShowGrid` / `ShowAxisGizmo` | 打开调试用的网格地面与坐标轴（见下） |

背景与天空盒的完整配法见 [环境与背景](./environment.md)；相机、光、拾取的深入分别见下面各自的节点小节。

## 我要做这件事 → 该用哪个节点

| 你想做的事 | 用哪个节点 | 深入看 |
|---|---|---|
| 决定画面从哪看、用透视还是正交投影 | `Camera` | [相机与视角控制](./camera.md) |
| 加太阳 / 平行光把场景整体照亮 | `DirectionalLight` | [光照与阴影](./lighting.md) |
| 加灯泡 / 火把这种四面八方发光 | `PointLight` | [光照与阴影](./lighting.md) |
| 加手电 / 舞台灯这种锥形光束 | `SpotLight` | [光照与阴影](./lighting.md) |
| 摆一个基础形状（立方 / 球 / 柱 / 面） | `Mesh` | [加载与放置模型](./models.md) |
| 摆一个导入的模型文件（glb / fbx / obj…） | `Model` | [加载与放置模型](./models.md) |
| 同一种物体成百上千份 | `InstancedMesh` | [实例化渲染](./instanced-rendering.md) |
| 海量实例还要按视区做空间剔除 | `InstancedMeshGroup`（HISM） | [实例化渲染](./instanced-rendering.md) |
| 火焰、烟雾、雨雪这类粒子特效 | `ParticleSystem` | [粒子系统](./particle-system.md) |
| 让一把剑跟着动画里的手骨一起动 | `BoneAttachment` | [动画系统](./animation.md) |

下面每种节点各给 2–4 句「它是干什么的」加一段最小代码。

## Camera：决定画面怎么看

相机决定从哪个位置、用什么投影把场景拍到屏幕上；一个场景至少得有一台相机才有画面。`Scene.MainCamera` 已经帮你建好，直接改它的变换就能用。

```csharp
var camera = scene.MainCamera;
camera.ProjectionType = ProjectionType.Perspective; // 或 Orthographic
camera.Position = new Vector3(0, 5, 10);
camera.LookAt(new Vector3(0, 0, 0));
camera.FitToBoundingBox(model.BoundingBox, padding: 0.5f); // 自动凑近把模型框满
```

想加鼠标键盘漫游、正交切换、多相机、把画面渲到纹理，见 [相机与视角控制](./camera.md)。

## DirectionalLight / PointLight / SpotLight：照亮场景

三种灯都来自抽象基类 `Light`，共有 `LightColor`（颜色）和 `CastShadow`（是否投影，默认关）。默认前向管线下**每种灯最多 4 盏**，且「有相机但没灯」时模型通常看不见——先摆一盏方向光再往下做。

**方向光 `DirectionalLight`**——光线平行，用来模拟太阳。它没有位置概念，方向由 `RotationDegrees` 决定；设为 `Scene.MainDirectionalLight` 后才启用级联阴影（CSM）。

```csharp
var sun = new DirectionalLight { LightColor = Color.White, CastShadow = true };
sun.RotationDegrees = new Vector3(-52, -34, 0);
sun.Irradiance = 80000;                     // lux，物理辐照度
view.AddNode(sun);
```

**点光 `PointLight`**——从一个点向四周发光，像灯泡。用 `Position` 摆位置，`AttenuationRadius` 控制照多远。

```csharp
var bulb = new PointLight { LightColor = Color.Red, AttenuationRadius = 5f };
bulb.Position = new Vector3(2, 3, 0);
view.AddNode(bulb);
```

**聚光 `SpotLight`**——锥形光束，像手电。`InnerConeAngleDegree`/`OuterConeAngleDegree` 定锥角，内外之间亮度渐变形成柔和边。

```csharp
var torch = new SpotLight { LightColor = Color.Blue, AttenuationRadius = 10f };
torch.InnerConeAngleDegree = 15f;
torch.OuterConeAngleDegree = 30f;
view.AddNode(torch);
```

阴影配置、CSM 参数、物理光照单位换算，全部在 [光照与阴影](./lighting.md)。

## Mesh：一个能直接渲染的形状

`Mesh` 是「几何体 + 材质」再挂到世界坐标上的最小可渲染单元。内置几何体有立方、球、圆柱、平面，改 `Geometry` 就换形状，改 `Material` 就换外观。

```csharp
var mesh = new Mesh { Geometry = new BoxGeometry() };
mesh.Material = new Material();
mesh.Material.BaseColor = Texture.CreateFromColor(Color.White);
mesh.Position = scene.MainCamera.Forward * 3;
view.AddNode(mesh);
```

内置几何体的构造参数、手写自定义几何、七种图元类型，见 [加载与放置模型](./models.md)；材质通道与贴图见 [材质与贴图](./material.md)。

## Model：导入进来的整棵模型树

`Model` 本身也是节点，它是一棵由多个 `Mesh` 组成的子树（对应一个模型文件）。加载器把它整个建出来，`Meshes` 就是它下面的网格集合，可按 `Name` 查部件、单独驱动某个部件。

```csharp
var model = ModelLoader.LoadGlbModel("robot.glb");
model.Position = new Vector3(0, 0, 0);
model.Scale = new Vector3(2f);
view.AddNode(model);

var head = model.Meshes.First(m => m.Name == "Head");
head.RotationDegrees = new Vector3(0, 45, 0);   // 只转头部
```

加载 glb/gltf 与 Assimp 多格式、部件查找、包围盒、克隆，见 [加载与放置模型](./models.md)。

## InstancedMesh：同一份几何画很多次

`InstancedMesh` 用 GPU 实例化把「同一个网格、不同摆放」一次性画出来——几千棵树、几万颗星，只提交一次几何与材质。从一个 `Mesh` 派生，然后只加实例矩阵。

```csharp
var source = new Mesh { Geometry = new BoxGeometry() };
var instanced = InstancedMesh.FromMesh(source);

instanced.AddInstance(Matrix4x4.CreateTranslation(0, 0, 0));
instanced.AddInstance(Matrix4x4.CreateTranslation(3, 0, 0));
view.AddNode(instanced);
```

逐实例增删改用 `AddInstance`/`UpdateInstance`/`RemoveInstance`，逐帧大改整批位置用 `SetInstances(...)`，逐实例颜色等自定义属性见 [实例化渲染](./instanced-rendering.md)。

## InstancedMeshGroup：海量实例的空间剔除（HISM）

`InstancedMeshGroup`（层次实例化网格）在 `InstancedMesh` 之上再建一棵八叉树，把实例分到若干组、按视锥自动剔除，适合「几万到几十万、且只看得清一部分」的场景。你把所有实例矩阵交给它，它自己分组、异步重建。

```csharp
var source = new Mesh { Geometry = new BoxGeometry() };
var group = new InstancedMeshGroup(source)
{
    MaxInstancesPerGroup = 64,
    MaxDepth = 6,
};

group.SetInstances(transforms);   // List<Matrix4x4>，一次给全
group.Build();                    // 触发八叉树分组（后台异步，自动收尾）
view.AddNode(group);
```

分组参数、`Build` 时机、增量更新与统计字段，见 [实例化渲染](./instanced-rendering.md)。

## ParticleSystem：CPU 模拟的粒子特效

`ParticleSystem` 是节点，上面挂若干发射器（`Emitters`），负责火焰、烟雾、雨雪、魔法这类逐帧生成又消散的粒子。配好发射器后调 `Play()`，系统每帧自动推进模拟。

```csharp
var ps = new ParticleSystem();
ps.Emitters.Add(myEmitter);   // 见粒子教程构造 ParticleEmitter
ps.Position = new Vector3(0, 1, 0);
view.AddNode(ps);
ps.Play();
```

`ParticleEmitter` 的全部参数、贴图 billboard 模式与网格模式、性能与可见性剔除，见 [粒子系统](./particle-system.md)。

## BoneAttachment：让物体牢牢跟着某根骨骼

`BoneAttachment` 是个特殊节点：你告诉它跟哪台蒙皮网格（`Mesh`）的哪根骨骼（`BoneName`），它每帧把自己贴到那根骨骼的世界位置上——角色手里「长出」的一把剑就该这么做。

```csharp
var sword = new BoneAttachment
{
    Mesh = skinnedMesh,        // 必须是带骨架的蒙皮网格
    BoneName = "Hand_R",
    LocalOffset = Matrix4x4.CreateTranslation(0, 0.2f, 0),
};
view.AddNode(sword);
```

骨骼体系、`Skeleton` 与动画采样，见 [动画系统](./animation.md)。

## Grid 与 AxisGizmo：调试辅助，不是节点

`Scene` 自带 `Grid`（地面网格）和 `AxisGizmo`（原点坐标轴）两个**调试辅助层**——它们是场景配置对象，不是挂在树上的 `Node`，只在你需要看清坐标方向、对齐物体时打开。

```csharp
scene.ShowGrid = true;
scene.Grid.Size = 12f;
scene.Grid.Divisions = 12;

scene.ShowAxisGizmo = true;
scene.AxisGizmo.AxisLength = 2.5f;
```

真正要看灯的范围球、相机视锥、包围盒这类可视化，用 `PipelineSettings.Debug.*`（见 [选择与配置管线](./pipelines.md)）。

## 常见坑

> [!WARNING]
> - **忘了相机或灯**：没有相机就没画面；默认前向管线下有相机没灯，模型常常一片黑。先确认 `Scene.MainCamera` 摆好、至少加一盏光。
> - **KeepWorld / KeepLocal 选错**：挂上去时物体「跳位」，多半是规则选反了——想让它在原地别动用 `KeepWorld`，想按相对父节点摆用 `KeepLocal`。
> - **重复 AddNode / 跨场景挂父子**：一个节点只属于一个场景，已进场景的再 `AddNode`、或把两个不同场景的节点互相挂父子都会抛异常。
> - **BeginTransformUpdate 没用 using**：作用域没结束（没 `Dispose`）时世界矩阵不会重算，画面看着没变。
> - **对子节点用 view.Remove**：`Remove`/`RemoveNode` 只针对顶层节点；摘子节点用 `parent.RemoveChild(...)`。
> - **Enable 的连带效应**：`Enable = false` 会关掉整棵子树，排查「东西不见了」时记得看父节点是不是被关了。
> - **逐帧给 InstancedMesh 一个个增删**：大批量改位置用 `SetInstances(...)` 一次性替换，比循环 `AddInstance`/`RemoveInstance` 高效得多。

## 可运行示例

- [SceneGizmos 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/SceneGizmos/SceneGizmosDemo.axaml.cs) — `ShowGrid`/`ShowAxisGizmo`、`Grid`/`AxisGizmo` 参数、`Enable` 隐藏节点、灯挂中枢绕转。
- [ModelViewer 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/ModelViewer/ModelViewerDemo.axaml.cs) — 模型树、部件查找、`Clone(CopyType)`。
- [Geometries 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Geometries/GeometriesDemo.axaml.cs) — 内置几何体的 `Mesh`。
- [Instancing 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Instancing/InstancingDemo.axaml.cs) / [Hism 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Hism/HismDemo.axaml.cs) — 实例化与层次实例化节点。
- [Particles 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Particles/ParticlesDemo.axaml.cs) — 粒子系统节点。

节点与场景成员的完整速查表见 [节点与场景速查](./reference-nodes.md)。源码：[Node.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/Node.cs)、[Scene.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Scenes/Scene.cs)。
