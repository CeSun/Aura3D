---
section: basics
order: 2
---

# 加载与放置模型

这一篇解决一件事：把「东西」放进你的 3D 场景。可以是从磁盘加载的 glTF/GLB 模型、Assimp 支持的 50 多种格式（FBX、OBJ、3DS、DAE 等），也可以不借助任何模型文件、用内置几何体或手写顶点数据现造一个。加载完成后，你会摆放它、找到它的部件、复制它。

## 最短可跑：加载一个 GLB 并摆进场景

先安装 glTF 加载包：

```shell
dotnet add package Aura3D.Model.GltfLoader
```

然后在 `SceneInitialized` 里加载、摆放、入场景：

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;
    var scene = args.Scene;

    // 默认管线（Blinn-Phong）下没有光源就看不到模型
    var light = new DirectionalLight { LightColor = Color.White };
    light.RotationDegrees = new Vector3(-45f, -25f, 0);
    scene.AddNode(light);

    var model = ModelLoader.LoadGlbModel("assets/stool.glb");
    model.Position = new Vector3(0, 0, 0);
    model.Scale = new Vector3(2f);
    view.AddNode(model);

    // 让相机自动框住模型（详见 ./camera.md）
    view.MainCamera.FitToBoundingBox(model.BoundingBox, padding: 0.5f);
}
```

## ModelLoader：加载 glTF / GLB

`Aura3D.Model.GltfLoader` 包提供以下入口，按需选用：

```csharp
// 从文件路径加载（仅静态模型）
var model = ModelLoader.LoadGlbModel("model.glb");

// 从 Stream 加载（资源内嵌、网络下载等非文件来源）
using (var stream = File.OpenRead("model.glb"))
{
    var model = ModelLoader.LoadGlbModel(stream);
}

// 从文件路径加载，同时取出动画
var (model, animations) = ModelLoader.LoadGlbModelAndAnimations("model.glb");

// .gltf 文本格式（几何/贴图通常在旁边的 .bin 与图片文件里）
var (model, animations) = ModelLoader.LoadGltfModelAndAnimations("model.gltf");
```

拿到的 `animations` 是动画剪辑列表，播放方式见 [动画系统](./animation.md)。

## AssimpLoader：FBX、OBJ 等 50+ 格式

安装 `Aura3D.Model.AssimpLoader` 包后，可以走 Assimp 这条通道加载 glTF 之外的格式：

```csharp
// 从文件加载（自动识别格式）
var (model, animations) = AssimpLoader.LoadModelAndAnimations("model.fbx");

// 从 Stream 加载（需指定格式后缀）
using (var stream = File.OpenRead("model.obj"))
{
    var model = AssimpLoader.Load(stream, "obj");
}

// 仅加载动画，挂到已有模型的骨架上（动作库工作流）
using (var stream = File.OpenRead("walk.fbx"))
{
    var animations = AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx");
}
```

Assimp 支持的格式包括 FBX、OBJ、3DS、DAE、PLY、STL、DXF、MD5、LWO、MS3D 等 50 多种。注意 Assimp 依赖原生库，主要在桌面端使用。

## 摆放：位置、旋转、缩放

模型是一个 [场景节点](./scene-and-nodes.md)，和其他节点一样设置变换：

```csharp
model.Position = view.MainCamera.Forward * 3;     // 放到相机正前方 3 米
model.RotationDegrees = new Vector3(0, 180, 0);   // 绕 Y 轴转 180 度
model.Scale = new Vector3(2f);                    // 整体放大两倍
view.AddNode(model);
```

节点上还有六个只读的方向向量：`Forward` / `Backward` / `Left` / `Right` / `Up` / `Down`。它们随旋转自动更新，但**不能赋值**——想改变朝向，改 `RotationDegrees` 或 `RotationQuaternion`。

### 包围盒

每个模型有 `BoundingBox`（含 `Min`、`Max` 两个角点），用于相机取景、拾取粗筛和调试显示：

```csharp
var bbox = model.BoundingBox;
view.MainCamera.FitToBoundingBox(bbox, padding: 0.5f);

model.BoundingBoxPadding = 0.12f;   // 给自动计算的包围盒加一圈余量

// 自动包围盒不合适时，手动指定一个
model.CustomBoundingBox = new BoundingBox(
    new Vector3(-2f, -0.2f, -2f),
    new Vector3(2f, 3.4f, 2f));
```

## 访问模型部件

一个模型由若干 `Mesh` 节点组成（`model.Meshes`），按 `Name` 找到部件后可以单独控制它：

```csharp
var wheel = model.Meshes.First(mesh => mesh.Name == "wheel_front_left");
wheel.RotationDegrees = wheel.RotationDegrees with { X = 45f };  // 单独转动这个轮子
```

`Name` 来自建模软件里的节点/网格名，加载后可用 `model.Meshes.Select(m => m.Name)` 先打印一遍确认。需要整棵子树查找时，用 `model.GetNodesInChildren<Mesh>()`。

## 克隆与共享

同一份模型摆多个时，用克隆而不是重复加载文件：

```csharp
// 共享底层资源数据（几何体、纹理等不复制），适合大量重复摆放
var clone = model.Clone(CopyType.SharedResourceData);
clone.Name = "stool-clone1";
clone.Position = new Vector3(2.6f, 0, 0);
scene.AddNode(clone);
```

`Clone` 的 `CopyType` 有三档：`SharedResource`、`SharedResourceData`、`FullCopy`，共享程度依次递减、独立性依次递增。日常摆放重复物体用 `SharedResourceData` 即可：变换各自独立，几何与纹理不复制。

> [!WARNING]
> 共享是双向的：克隆体可能与原模型引用同一份材质。给克隆体换贴图（例如 `mesh.Material.SetTexture("BaseColor", ...)`）会连带改变原模型的外观。改之前先在 Gallery 的 ModelViewer 示例里验证共享边界，或者改用 `FullCopy`。

## 不用模型文件：内置几何体

盒子、球、圆柱、平面开箱即用，适合原型、地面和调试物体：

```csharp
var mesh = new Mesh();

// 内置几何体（Geometry 子类，都有无参默认尺寸与带参自定义两种用法）
mesh.Geometry = new BoxGeometry();                          // 盒子
mesh.Geometry = new BoxGeometry(2f, 1f, 3f);                // 指定长宽高
mesh.Geometry = new SphereGeometry();                       // 球体
mesh.Geometry = new CylinderGeometry();                     // 圆柱体
mesh.Geometry = new PlaneGeometry();                        // 平面（1x1）
mesh.Geometry = new PlaneGeometry(40f, 40f);                // 自定义尺寸的平面

mesh.Material = new Material();
mesh.Material.BaseColor = Texture.CreateFromColor(Color.White);
mesh.Material.DoubleSided = true;   // 单片平面从背面也能看到

mesh.Position = view.MainCamera.Forward * 3;
view.AddNode(mesh);
```

需要控制细分精度时用带段数的构造，例如球体 `new SphereGeometry(1.4f, 48, 24, phiLength: MathF.PI * 2)`、平面 `new PlaneGeometry(3f, 3f, 8, 8)`（宽高段数影响顶点数，不一定都要细分）。

> [!IMPORTANT]
> `BaseColor` 是扩展属性，**不能**写在对象初始化器里（`new Material { BaseColor = ... }` 编译不过）。先 `new Material()`，再单独赋值；或者用通道写法 `material.SetTexture("BaseColor", texture)`。

## 自定义几何体与图元类型

内置形状不够用时，手填顶点缓冲区就能构造任意形状。核心是 `Geometry` 加顶点属性、索引和图元类型三样东西：

```csharp
var geometry = new Geometry();

// 图元类型：决定这批顶点/索引怎么连线（默认 Triangles）
geometry.PrimitiveType = PrimitiveType.Triangles;

// 顶点属性：位置是 0 号槽位、3 分量
geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, new List<float>
{
    -0.5f, -0.5f, 0,   // 左下
     0.5f, -0.5f, 0,   // 右下
     0.0f,  0.5f, 0,   // 上
});

// 顶点色写在 Color_0 槽位、4 分量
geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, new List<float>
{
    1, 0, 0, 1,
    0, 1, 0, 1,
    0, 0, 1, 1,
});

// 索引：Triangles 类型推荐使用索引；不设索引则按顶点顺序每 3 个一组
geometry.SetIndices(new List<uint> { 0, 1, 2 });

// 法线、切线等属性默认启用，用不到的可以关掉省带宽
geometry.SetAttributeEnabled(BuildInVertexAttribute.TexCoord_1, false);

var material = new Material { BlendMode = BlendMode.Opaque };
var mesh = new Mesh { Geometry = geometry, Material = material };
view.AddNode(mesh);
```

### 七种图元

同一批顶点换一种图元类型就得到点云、线框或实心面。以索引数 `n` 计：

| PrimitiveType | 怎么解释数据 | 图元数量 | 典型用途 |
|---|---|---|---|
| `Points` | 每个顶点一个点 | n | 点云 |
| `Lines` | 每 2 个索引一条独立线段 | n / 2 | 线框、坐标轴 |
| `LineStrip` | 索引依次连成折线 | n - 1 | 轨迹线、轮廓 |
| `LineLoop` | 折线首尾闭合 | n | 闭合轮廓 |
| `Triangles` | 每 3 个索引一个三角形（默认） | n / 3 | 普通网格 |
| `TriangleStrip` | 每新增 1 个顶点多出 1 个三角形 | n - 2 | 带状网格 |
| `TriangleFan` | 顶点绕首个顶点扇形展开 | n - 2 | 圆盘、扇面 |

> [!TIP]
> 点与线通常不吃光照：想画彩色点云/线框，常见做法是配 NoLight 管线加一小段自定义着色器（点渲染还要在顶点着色器里设 `gl_PointSize`）。自定义着色器见 [自定义材质与着色器](./custom-material.md)，完整可跑的七种图元对照见 Gallery 的 Primitives 示例。

## 常见坑

- **模型加载了但一片黑**：默认 Blinn-Phong 管线必须有光源。先加一盏 `DirectionalLight`，见 [光照与阴影](./lighting.md)。
- **忘了装加载包**：glTF 与 Assimp 是分开的 NuGet 包（`Aura3D.Model.GltfLoader` / `Aura3D.Model.AssimpLoader`），`Aura3D.Avalonia` 本体不包含加载器。
- **给 `Forward` 等方向向量赋值**：它们是只读的，赋值编译不过；改朝向请设置 `RotationDegrees`。
- **克隆体一改贴图，原模型跟着变**：`SharedResourceData` 克隆共享几何与材质数据，见上面警告块。
- **`new Material { BaseColor = ... }` 编译报错**：`BaseColor` 是扩展属性，不能进对象初始化器。
- **模型部件名对不上**：`Meshes` 里的 `Name` 由导出工具决定，先把名字列表打出来再按名查找。

## 可运行示例

- [ModelViewer 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/ModelViewer/ModelViewerDemo.axaml.cs) — 部件树、包围盒（含 `BoundingBoxPadding` / `CustomBoundingBox`）、三种 `CopyType` 克隆的共享边界对照。
- [Geometries 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Geometries/GeometriesDemo.axaml.cs) — 四种内置几何体的全部构造参数，实时看顶点/索引数量变化。
- [Primitives 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Primitives/PrimitivesDemo.axaml.cs) — 同一批手写顶点在七种图元类型下的连线规则。
- [AssimpFbx 示例](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/AssimpFbx/AssimpFbxDemo.axaml.cs) — Assimp 加载 FBX 与动作库（`LoadAnimations` + 共享骨架）。

## 下一步

- [相机与视角控制](./camera.md) — 让相机自动框住你刚放的模型。
- [材质与贴图](./material.md) — 给模型换外观。
- [动画系统](./animation.md) — 播放加载出来的动画剪辑。
