# 开始上手

## 安装

Aura3D 提供多个 NuGet 包，按需安装即可。最常用的入口是 `Aura3D.Avalonia`。

### 基础安装

```shell
dotnet add package Aura3D.Avalonia
```

此包会自动引入 `Aura3D.Core`，包含默认渲染管线（BlinnPhong）和基础功能。

### 按需安装扩展包

```shell
# glTF/GLB 模型加载
dotnet add package Aura3D.Model.GltfLoader

# Assimp 多格式模型加载（FBX、OBJ、3DS 等 50+ 格式）
dotnet add package Aura3D.Model.AssimpLoader

# PBR 延迟渲染管线
dotnet add package Aura3D.Pipeline.PBR

# 卡通渲染管线
dotnet add package Aura3D.Pipeline.CelShading
```

## 基本用法

### 在 XAML 中声明控件

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    ...>
    <a:Aura3DView
        x:Name="aura3Dview"
        SceneInitialized="OnSceneInitialized"
        SceneUpdated="OnSceneUpdated"/>
</Window>
```

- `SceneInitialized` — OpenGL 初始化完成后触发，在此处构建场景
- `SceneUpdated` — 每帧渲染前触发，参数中包含 `DeltaTime`（秒），在此处更新逻辑

### iOS / macOS 平台配置

在 iOS 和 macOS 上，需要在 `AppBuilder` 中指定 OpenGL 渲染模式：

```csharp
// Program.cs 或 App.axaml.cs
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions()
        {
            RenderingMode = new[] { AvaloniaNativeRenderingMode.OpenGl }
        });
```

### 初始化场景

```csharp
public void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;
    var scene = args.Scene;

    // 设置背景色
    scene.Background = Texture.CreateFromColor(Color.Gray);

    // 构建你的场景...
}
```

## 摄像机

场景中必须存在摄像机才能显示画面。`Aura3DView.MainCamera` 是默认摄像机。

```csharp
var camera = view.MainCamera;

// 设置投影类型
camera.ProjectionType = ProjectionType.Perspective;   // 透视（默认）
// camera.ProjectionType = ProjectionType.Orthographic; // 正交

// 移动摄像机
camera.Position = new Vector3(0, 5, 10);
camera.RotationDegrees = new Vector3(-20, 0, 0);

// 是否渲染背景天空盒
camera.IsRenderBackground = true;  // 默认开启

// 根据包围盒自动适配视角
camera.FitToBoundingBox(model.BoundingBox, padding: 0.5f);
```

### 摄像机控制器

`CameraController` 提供了开箱即用的摄像机操作（WASD 移动、鼠标右键旋转、滚轮缩放、中键平移）：

```csharp
private CameraController _cameraController;

public void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;

    _cameraController = new CameraController(view)
    {
        MoveSpeed = 30f,          // 移动速度
        MouseSensitivity = 20f,   // 鼠标灵敏度
        ZoomSpeed = 5f,           // 缩放速度
    };
}
```

可配置属性：

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `MoveSpeed` | `float` | `10f` | 键盘移动速度 |
| `MouseSensitivity` | `float` | `20f` | 鼠标旋转灵敏度 |
| `PanSpeed` | `float` | `10f` | 平移速度 |
| `ZoomSpeed` | `float` | `5f` | 缩放速度 |
| `Enabled` | `bool` | `true` | 是否启用 |
| `EnableLook` | `bool` | `true` | 启用旋转（右键拖拽） |
| `EnableMovement` | `bool` | `true` | 启用 WASD/QE 移动 |
| `EnableZoom` | `bool` | `true` | 启用滚轮缩放 |
| `EnablePan` | `bool` | `true` | 启用中键平移 |

> **注意**：`CameraController` 实现了 `IDisposable`，如不再使用请调用 `Dispose()`。

### 摄像机进阶参数

```csharp
var camera = view.MainCamera;

// 透视投影参数
camera.ProjectionType = ProjectionType.Perspective;
camera.FieldOfView = 60f;         // FOV（视场角，度），默认 75
camera.NearPlane = 0.1f;          // 近裁剪面，默认 1
camera.FarPlane = 1000f;          // 远裁剪面，默认 100

// 正交投影参数
camera.ProjectionType = ProjectionType.Orthographic;
camera.OrthographicSize = 10f;    // 正交视图大小，默认 5

// 让摄像机看向某个目标点
camera.LookAt(new Vector3(0, 0, 0));

// 只读矩阵（自定义着色器时使用）
Matrix4x4 viewMatrix = camera.View;
Matrix4x4 projMatrix = camera.Projection;
Matrix4x4 vpMatrix = camera.ViewProjection;
```

投影参数会在赋值时校验：`NearPlane > 0`、`FarPlane > NearPlane`、`FieldOfView` 位于 `(0, 180)` 度、`OrthographicSize > 0`，且数值必须有限。需要同时大幅调整远近裁剪面时，使用 `camera.SetClippingPlanes(near, far)` 原子更新，避免中间状态无效。

## 模型

### 加载 glTF/GLB 模型

```csharp
// 从文件路径加载（仅静态模型）
var model = ModelLoader.LoadGlbModel("model.glb");

// 从文件路径加载（含动画）
var (model, animations) = ModelLoader.LoadGlbModelAndAnimations("model.glb");

// 从 Stream 加载
using (var stream = File.OpenRead("model.glb"))
{
    var model = ModelLoader.LoadGlbModel(stream);
}

// 加载 .gltf 文本格式
var (model, animations) = ModelLoader.LoadGltfModelAndAnimations("model.gltf");
```

### 通过 Assimp 加载更多格式

```csharp
// 从文件加载（自动识别格式）
var (model, animations) = AssimpLoader.LoadModelAndAnimations("model.fbx");

// 从 Stream 加载（需指定格式）
using (var stream = File.OpenRead("model.obj"))
{
    var model = AssimpLoader.Load(stream, "obj");
}

// 仅加载动画（应用到已有骨架）
using (var stream = File.OpenRead("walk.fbx"))
{
    var animations = AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx");
}
```

Assimp 支持的格式包括：FBX、OBJ、3DS、DAE、PLY、STL、DXF、MD5、LWO、MS3D 等 50+ 格式。

### 放置模型到场景

```csharp
var model = ModelLoader.LoadGlbModel("model.glb");

// 设置位置、旋转、缩放
model.Position = camera.Forward * 3;
model.RotationDegrees = new Vector3(0, 180, 0);
model.Scale = new Vector3(2f);

// 方向向量（只读）
// model.Forward / model.Right / model.Up / model.Backward / model.Left / model.Down

view.AddNode(model);
```

### 克隆模型

```csharp
// 共享底层资源数据（几何体、纹理等不复制），适合大量实例化
var cloned = original.Clone(CopyType.SharedResourceData);
```

### 访问模型的网格部件

```csharp
// 模型由多个 Mesh 组成，可通过名称查找
var specificPart = model.Meshes.First(mesh => mesh.Name == "item1");
specificPart.RotationDegrees = specificPart.RotationDegrees with { Y = 45f };
```

## 基础几何体

无需外部模型文件，直接创建基础形状：

```csharp
var mesh = new Mesh();

// 内置几何体类型
mesh.Geometry = new BoxGeometry();       // 盒子
// mesh.Geometry = new SphereGeometry(); // 球体
// mesh.Geometry = new CylinderGeometry(); // 圆柱体
// mesh.Geometry = new PlaneGeometry();  // 平面 (1x1)
// mesh.Geometry = new PlaneGeometry(2f, 3f); // 自定义尺寸的平面

mesh.Material = new Material();
mesh.Material.BaseColor = Texture.CreateFromColor(Color.White);
mesh.Material.BlendMode = BlendMode.Opaque;
mesh.Material.DoubleSided = true;   // 双面渲染

mesh.Position = view.MainCamera.Forward * 3;
view.AddNode(mesh);
```

### 自定义几何体

除了内置几何体，你也可以手动构建任意形状：

```csharp
var geometry = new Geometry();

// 设置图元类型
geometry.PrimitiveType = PrimitiveType.Triangles;

// 设置顶点属性（Position = 0 号槽位，3 分量）
geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, new List<float>
{
    // 第一个三角形
    -0.5f, -0.5f, 0,  // 左下
     0.5f, -0.5f, 0,  // 右下
     0.0f,  0.5f, 0,  // 上
});

// 可选：设置索引（不设索引则按顶点顺序每 3 个一组）
geometry.SetIndices(new List<uint> { 0, 1, 2 });

// 启用/禁用顶点属性（法线、切线等默认开启，不需要的可关闭）
geometry.SetAttributeEnabled(BuildInVertexAttribute.TexCoord_1, false);
```

## 光源

Aura3D 默认使用 Blinn-Phong 光照模型。**场景中必须有光源才能看到模型**。

> [!WARNING]
> 默认前向管线下，每种光源类型最多支持 4 盏。

### 方向光

模拟远距离光源，光线平行（如太阳光）。

```csharp
var dl = new DirectionalLight();
dl.LightColor = Color.White;
dl.RotationDegrees = new Vector3(-30, -15, 0);  // 光照方向
dl.CastShadow = true;                            // 投射阴影

// 阴影配置
dl.ShadowConfig.FarPlane = 1000;
dl.ShadowConfig.NearPlane = 10;
dl.ShadowConfig.Width = 1024;
dl.ShadowConfig.Height = 1024;

view.AddNode(dl);
```

### 点光

从一个点向所有方向发射的光（如灯泡、火把）。

```csharp
var pl = new PointLight();
pl.LightColor = Color.Red;
pl.AttenuationRadius = 5f;   // 衰减半径
pl.CastShadow = true;        // 投射阴影
pl.Position = new Vector3(2, 3, 0);

view.AddNode(pl);
```

### 聚光灯

锥形光束（如手电筒、舞台灯）。

```csharp
var sp = new SpotLight();
sp.LightColor = Color.Blue;
sp.AttenuationRadius = 10f;
sp.InnerConeAngleDegree = 15f;   // 内锥角
sp.OuterConeAngleDegree = 30f;   // 外锥角（边缘柔和过渡）
sp.CastShadow = true;

view.AddNode(sp);
```

### 光源高级属性

```csharp
// 方向光 — 辐照度（物理光照）
var dl = new DirectionalLight();
dl.Irradiance = 120000f;   // lux，默认 80000。Intensity = Irradiance * 0.00001

// 点光 — 光强和阴影柔和度
var pl = new PointLight();
pl.LuminousIntensity = 2000f;   // 坎德拉(cd)，默认 1000。Intensity = LuminousIntensity * 0.001
pl.SoftRatio = 0.7f;            // 阴影柔和比例，默认 0.9（越大越硬）

// 聚光灯 — 锥角半影
var sp = new SpotLight();
sp.InnerConeAngleDegree = 10f;   // 内锥角（完全亮度）
sp.OuterConeAngleDegree = 25f;   // 外锥角（边缘半影过渡）
// 内锥到外锥之间：亮度从 1.0 渐变到 0.0
sp.LuminousIntensity = 2000f;    // cd，同点光
sp.SoftRatio = 0.8f;             // 阴影柔和比例
```

> **说明**：`Irradiance` / `LuminousIntensity` 提供物理光照值，引擎内部转换为 `Intensity`（只读）供着色器使用。设置前者即可，后者自动同步。

## 场景背景

### 纯色背景

```csharp
view.Scene.Background = Texture.CreateFromColor(Color.Gray);
```

### HDR 环境贴图 / 天空盒

```csharp
using (var stream = File.OpenRead("environment.hdr"))
{
    var hdri = TextureLoader.LoadHdrTexture(stream);
    var cubemap = HDRIToCubeTextureConverter.ConvertFromTexture(hdri, 1024);
    view.Scene.Background = cubemap;
}
```

### 立方体贴图天空盒

```csharp
var streams = new List<Stream>();
foreach (var face in new[] { "px.png", "nx.png", "py.png", "ny.png", "pz.png", "nz.png" })
{
    streams.Add(File.OpenRead(face));
}
var cubeTexture = TextureLoader.LoadCubeTexture(streams);
view.Scene.Background = cubeTexture;

// 记得关闭 stream
foreach (var s in streams) s.Dispose();
```

### 纹理采样配置

通过 `Texture` 或 `CubeTexture` 上的 Fluent API 配置采样参数，这些设置直接影响渲染品质：

```csharp
var texture = TextureLoader.LoadTexture(stream)
    .SetWarpS(TextureWrapMode.Repeat)         // S 轴（U）包裹模式，默认 ClampToEdge
    .SetWarpT(TextureWrapMode.Repeat)         // T 轴（V）包裹模式
    .SetMinFilter(TextureFilterMode.LinearMipmapLinear)  // 缩小过滤，默认 Linear
    .SetMagFilter(TextureFilterMode.Linear)               // 放大过滤，默认 Linear
    .SetColorFormat(ColorFormat.Srgb)         // 色彩空间，默认无
    .SetIsGammaSpace(true);                   // Gamma 空间，默认 false
```

立方体贴图额外支持 WrapR（第三维包裹）：
```csharp
var cubemap = TextureLoader.LoadCubeTexture(streams)
    .SetWarpR(TextureWrapMode.ClampToEdge);
```

## 节点层次结构

Aura3D 使用场景图来组织节点，子节点继承父节点的变换。

```csharp
var parent = new Node();
parent.Position = new Vector3(0, 5, 0);
view.AddNode(parent);

var child = new Mesh();
child.Geometry = new BoxGeometry();

// 添加为子节点
parent.AddChild(child, AttachToParentRule.KeepWorld);  // 保持世界坐标不变
// parent.AddChild(child, AttachToParentRule.KeepLocal); // 保持本地坐标

// 移除子节点
parent.RemoveChild(child, AttachToParentRule.KeepWorld);

// 从场景中移除
view.Remove(node);
```

## 场景更新与渲染循环

### 自动渲染

默认情况下，控件会自动请求每帧渲染。可在 `SceneUpdated` 事件中处理逐帧逻辑：

```csharp
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    // e.DeltaTime — 上一帧到当前帧的时间间隔（秒）
    // e.Scene — 当前场景

    // 旋转光源
    dl.RotationDegrees += new Vector3(0, 30, 0) * (float)e.DeltaTime;
}
```

### 手动控制渲染

如果希望按需渲染（例如场景静止时节省资源），可关闭自动渲染：

```csharp
view.AutoRequestNextFrameRendering = false;
```

然后在需要更新画面时手动请求：

```csharp
view.RequestNextFrameRendering();
```

> 在 `SceneUpdated` 事件中调用 `RequestNextFrameRendering()` 可以实现连续渲染（常用于需要实时更新的动画场景）。

## 点击拾取

Aura3D 支持通过屏幕坐标拾取场景中的物体，返回三角形级别的精确结果：

```csharp
// 获取屏幕坐标处的所有命中结果（按距离排序）
List<PickResult> results = view.Scene.Pick(screenX, screenY, view.MainCamera);

// 获取最近的命中结果
PickResult? closest = view.Scene.PickClosest(screenX, screenY, view.MainCamera);

if (closest != null)
{
    var node = closest.Value.Node;              // 命中的节点
    var worldPos = closest.Value.WorldPosition; // 命中点的世界坐标
    var distance = closest.Value.Distance;      // 到摄像机的距离
    var instanceIndex = closest.Value.InstanceIndex; // InstancedMesh 实例索引（普通 Mesh 为 null）
}
```

> 通常在鼠标点击事件中获取屏幕坐标后调用。支持普通 Mesh、InstancedMesh 和 CPU 蒙皮骨骼网格。

## 级联阴影贴图（CSM）

CSM（Cascaded Shadow Maps）用于解决方向光阴影在远距离下的锯齿问题。通过 `Scene.MainDirectionalLight` 指定主方向光：

```csharp
var dl = new DirectionalLight();
dl.LightColor = Color.White;
dl.RotationDegrees = new Vector3(-30, 0, 0);
dl.CastShadow = true;
view.AddNode(dl);

// 设为主方向光 → 使用 CSM；其余方向光退化为单张阴影贴图
view.Scene.MainDirectionalLight = dl;
```

CSM 参数通过 `PipelineSettings` 配置：

```csharp
var settings = new PipelineSettings
{
    CsmCascadeCount = 4,           // 级联数量（默认 3，设为 1 回退单阴影贴图）
    CsmSplitLambda = 0.5f,         // 分割参数（0=均匀，1=对数，默认 0.5）
    CsmShadowMapResolution = 2048, // 每级联的分辨率（默认 1024）
};
```

> 仅声明了 `SupportsCSM = true` 的管线（如 BlinnPhong）才会启用 CSM。

## 调试可视化

通过 `PipelineSettings.Debug` 开启内置的调试绘制：

```csharp
var debug = view.Scene.RenderPipeline.Settings.Debug;
debug.Enable = true;                // 总开关
debug.ShowBoundingBox = true;       // 显示所有网格的包围盒
debug.ShowDirectionalLight = true;  // 显示方向光方向线
debug.ShowPointLight = true;        // 显示点光范围球
debug.ShowSpotLight = true;         // 显示聚光灯锥体
debug.ShowCamera = true;            // 显示摄像机视锥体
debug.ShowBone = true;              // 显示骨骼层次
```

> 也可在 XAML 中通过 `PipelineSettings` 配置。调试绘制有额外性能开销，建议仅开发时开启。

## 使用指定渲染管线

在 XAML 中通过泛型参数指定管线：

```xaml
<!-- PBR 延迟管线 -->
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:pbr="clr-namespace:Aura3D.Pipeline.PBR;assembly=Aura3D.Pipeline.PBR"
    ...>
    <a:Aura3DView x:TypeArguments="pbr:PBRDeferredPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

```xaml
<!-- 卡通渲染管线 -->
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:cel="clr-namespace:Aura3D.Pipeline.CelShading;assembly=Aura3D.Pipeline.CelShading"
    ...>
    <a:Aura3DView x:TypeArguments="cel:CelShadingPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

也可以在代码中通过 `CreateRenderPipeline` 属性动态设置：

```csharp
view.CreateRenderPipeline = scene => new NoLightPipeline(scene);
```

> 注意：`CreateRenderPipeline` 必须在 OpenGL 初始化之前赋值（即在控件加载之前）。

## 下一步

- [渲染管线](./pipelines.md) — 了解和自定义渲染管线
- [动画系统](./animation.md) — 骨骼动画、混合空间、状态图
- [实例化渲染](./instanced-rendering.md) — GPU 实例化与 HISM
- [渲染专题](./rendering.md) — 阴影、点云、图元、材质、节点操作
