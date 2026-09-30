---
section: basics
order: 4
---

# 光照与阴影

上一篇把模型放进了场景，但默认管线下**没有光源就什么都看不见**——这一篇负责把场景照亮，并让它投下影子。你会加三种灯、调它们的亮度与范围、开阴影，并处理新手最常撞的两个坑：灯多了不生效、影子缺一大块。

## 最短可跑：一盏太阳 + 一盏灯

在 `SceneInitialized` 里加两盏灯，一个受光物体：

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;
    var scene = args.Scene;

    scene.Background = Texture.CreateFromColor(Color.Gray);

    // 方向光 = 太阳。它没有位置概念，朝向决定光从哪来
    var sun = new DirectionalLight
    {
        LightColor = Color.White,
        Irradiance = 80000,                       // lux，默认值
        RotationDegrees = new Vector3(-35, -20, 0),
    };
    scene.AddNode(sun);

    // 点光 = 灯泡。位置 + 衰减半径决定它照到哪
    var lamp = new PointLight
    {
        LightColor = Color.White,
        LuminousIntensity = 1500,                 // 坎德拉 cd，默认 1000
        AttenuationRadius = 8f,
        Position = new Vector3(2f, 2.5f, 1f),
    };
    scene.AddNode(lamp);

    var box = new Mesh { Geometry = new BoxGeometry(), Material = new Material() };
    box.Material.BaseColor = Texture.CreateFromColor(Color.White);
    box.Position = new Vector3(0, 0.5f, 0);
    scene.AddNode(box);

    var ground = new Mesh { Geometry = new PlaneGeometry(60f, 60f), Material = new Material() };
    ground.Material.BaseColor = Texture.CreateFromColor(Color.Gray);
    scene.AddNode(ground);
}
```

## 三种灯怎么选

| 灯 | 用来模拟 | 位置怎么定 | 独有参数 |
|---|---|---|---|
| `DirectionalLight` | 太阳、月光、平行光 | 只有朝向：光沿节点的 `Forward`（由 `RotationDegrees` 决定）射出 | `Irradiance`（lux） |
| `PointLight` | 灯泡、火把、爆炸光 | `Position`，向四面八方 | `AttenuationRadius`、`LuminousIntensity`（cd）、`SoftRatio` |
| `SpotLight` | 手电、舞台追光、车灯 | `Position` + `RotationDegrees` 定锥轴 | `InnerConeAngleDegree`、`OuterConeAngleDegree`，另有上排三个 |

三种灯都有的：`LightColor`、`CastShadow`、以及 `Node` 的全部变换属性（可以挂在父节点上跟着动，见[场景图与节点](./scene-and-nodes.md)）。

> [!NOTE]
> `LightColor` 与 `Texture.CreateFromColor(...)` 收的是 `System.Drawing.Color`（`R/G/B/A` 为 0..255）。如果你的文件里同时 `using Avalonia.Media;` 和 `using System.Drawing;`，`Color` 会二义，Gallery 示例里那种 `System.Drawing.Color.White` 的完全限定写法就是为此准备的。

### 方向光：靠旋转定方向

```csharp
var dl = new DirectionalLight();
dl.LightColor = Color.White;
dl.RotationDegrees = new Vector3(-30, -15, 0);   // 光沿 Forward 射出去
```

俯角（第一个分量）越负，光越接近从头顶直射。想让太阳转起来，逐帧改 `RotationDegrees` 即可：

```csharp
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    sun.RotationDegrees = sun.RotationDegrees with { Y = sun.RotationDegrees.Y + 30f * (float)e.DeltaTime };
}
```

### 点光：靠衰减半径定范围

```csharp
var pl = new PointLight();
pl.LightColor = Color.Red;
pl.AttenuationRadius = 5f;      // 默认 10：到这个距离光衰减为 0
pl.Position = new Vector3(2, 3, 0);
```

`AttenuationRadius` 是点光唯一的「大小」旋钮：太小就会出现一盏灯只管住脚下一小块。想临时关掉某盏灯，把它 `Enable = false` 即可（灯仍在场景里，但不参与计算）。

### 聚光灯：内外锥角就是半影

```csharp
var sp = new SpotLight();
sp.LightColor = Color.Blue;
sp.AttenuationRadius = 10f;
sp.InnerConeAngleDegree = 10f;   // 默认 10：内锥里是全亮
sp.OuterConeAngleDegree = 25f;   // 默认 15：外锥之外完全无光
sp.Position = new Vector3(0, 5, 4);
sp.RotationDegrees = new Vector3(-40, 0, 0);
```

内锥到外锥之间亮度从 1.0 渐变到 0.0，这段差值就是柔和的边缘（半影）。两者相等 = 边缘硬切。外锥角同时决定这盏灯的阴影锥有多宽。

## 亮度：用物理量，不用裸系数

引擎按物理光照单位接收参数，内部换算成着色器用的 `Intensity`（只读，不用手动设）：

| 属性 | 单位 | 默认 | 换算 |
|---|---|---|---|
| `DirectionalLight.Irradiance` | lux（辐照度） | 80000 | `Intensity = Irradiance * 0.00001` |
| `PointLight.LuminousIntensity`<br>`SpotLight.LuminousIntensity` | cd（坎德拉） | 1000 | `Intensity = LuminousIntensity * 0.001` |

`LightColor` 只提供色相（RGB 三个 0..255 通道），与上面的强度相乘。

```csharp
sun.Irradiance = 120000f;      // 比默认更「晒」
lamp.LuminousIntensity = 2000f; // 更亮的灯泡
```

画面整体偏暗或过曝，先别动灯——那是色调映射的事，调 `PipelineSettings.ToneMappingExposure`（默认 0.7）与 `BrightnessClamp`（默认 4.0），见[选择与配置管线](./pipelines.md)。

`SoftRatio`（点光/聚光，默认 0.9）控制**阴影的软硬**：值越小边缘越糊，越大越锐。

```csharp
lamp.SoftRatio = 0.7f;   // 阴影边缘更柔和
```

## 默认每类只有 4 盏：新手第一坑

> [!WARNING]
> 默认管线下，方向光、点光、聚光**各自最多 4 盏生效**。第 5 盏灯既不提供光照、也不投射阴影——它就像没被加进场景一样安静。

生效的取法是「按加入场景的顺序取前 N 盏」。要支持更多，在**管线创建之前**改上限（有效范围 `1..10`）：

```csharp
var view = new Aura3DView
{
    PipelineSettings = new PipelineSettings
    {
        DirectionalLightLimit = 2,   // 只要两盏平行光，省下的额度留给点光
        PointLightLimit = 8,
        SpotLightLimit = 4,
    },
};
```

> [!TIP]
> 三个上限在管线创建后改了不生效，因为它们决定着色器里的循环长度。灯多到几十盏时，考虑只留少数投影灯，或换管线策略，见[选择与配置管线](./pipelines.md)。

## 阴影

每种灯都有 `CastShadow`（默认 `false`）。开了之后，这盏灯每帧会多渲染一遍「从灯看到的深度图」，主渲染时用它判断每个点是否在影子里。

```csharp
sun.CastShadow = true;
lamp.CastShadow = true;
```

### 谁投影、谁被投影

场景里**所有**网格都会进阴影图——不透明（`Opaque`）和 Alpha 裁剪（`Masked`）的静态网格、骨骼网格、实例化网格都算。

> [!NOTE]
> `BlendMode.Translucent`（半透明）材质不投射阴影。想让一片树叶投影，用 `Masked` + `AlphaCutoff`，见[材质与贴图](./material.md)。

### 裁剪面与覆盖范围

方向光用正交相机画阴影，`ShadowConfig` 就是这个正交盒子的尺寸——**注意 `Width`/`Height` 是「世界单位的覆盖范围」，不是贴图分辨率**：

```csharp
sun.ShadowConfig.Width = 50;      // 默认 50：横向覆盖多大一片场景
sun.ShadowConfig.Height = 50;     // 默认 50
sun.ShadowConfig.NearPlane = 0.1f; // 默认 0.1
sun.ShadowConfig.FarPlane = 50f;   // 默认 50
```

覆盖范围小于场景，超出部分的物体就不投影（表现为影子缺一大块）。点光和聚光的 `ShadowConfig` 只有 `NearPlane`（默认 1）与 `FarPlane`（默认 100）。

### 分辨率

- 走 CSM 的主方向光：分辨率 = `PipelineSettings.CsmShadowMapResolution`（默认 1024）。
- 其余投影灯（点光、聚光、非主光的方向光）：阴影图固定 1024×1024，当前不可配。

> [!WARNING]
> 阴影有实打实的开销：一盏投影**点光**要渲染立方体图六个面，一盏投影**聚光灯**要一次，方向光一次。灯多了画面会掉帧——优先关掉远处或看不见物体的灯的 `CastShadow`，而不是把分辨率往上调。

<a id="csm"></a>

## 级联阴影 CSM：远处阴影不再锯齿

方向光的单张阴影图摊到很大范围时，近处物体会出现明显锯齿。CSM（Cascaded Shadow Maps）把相机视锥切成几段，每段一张独立阴影图，近处精度高、远处精度低。

```csharp
scene.AddNode(sun);

// 指定这盏方向光走 CSM；其余方向光仍是单张阴影图
scene.MainDirectionalLight = sun;
```

不指定也可以：不显式设置时，引擎自动取**第一盏 `Enable && CastShadow` 的方向光**当主光。因此把 `MainDirectionalLight` 设回 `null` 并不会关掉级联——那只是把主光交还给自动选取。要真正回到单张阴影图，把 `CsmCascadeCount` 设为 1，或让这盏灯不投影（`CastShadow = false`）。

参数全在 `PipelineSettings`：

```csharp
var settings = new PipelineSettings
{
    CsmCascadeCount = 4,            // 默认 3，范围 1..4；1 = 回退单张阴影图
    CsmSplitLambda = 0.5f,          // 0 = 均匀分割，1 = 对数分割（近处给更多级联）
    CsmShadowMapResolution = 2048,  // 默认 1024，每个级联一张
};
```

| 参数 | 默认 | 何时设 |
|---|---|---|
| `CsmCascadeCount` | 3 | 必须在管线创建前 |
| `CsmShadowMapResolution` | 1024 | 必须在管线创建前 |
| `CsmSplitLambda` | 0.5 | 随时可改，即时生效 |

> [!IMPORTANT]
> CSM 只对声明了 `SupportsCSM = true` 的管线生效：默认 BlinnPhong 与 PBR（延迟、前向）都支持；卡通 CelShading、`NoLightPipeline`、点云管线没有覆写它（基类默认 `false`），因此始终走单张阴影图。Browser（WebGL）宿主上则一律强制退回单张阴影图。
>
> 另外：CSM 生效时，主方向光的覆盖范围来自**相机视锥的分割**，`ShadowConfig.Width/Height` 只在单张阴影图那条路径上起作用。

运行时可以直接改：

```csharp
view.Scene.RenderPipeline.Settings.CsmSplitLambda = 0.8f;
```

管线的选择、其余 `PipelineSettings` 参数与调试可视化，深入配置链在[选择与配置管线](./pipelines.md)。

## 看不清灯在哪时：调试可视化

```csharp
var debug = view.Scene.RenderPipeline.Settings.Debug;
debug.Enable = true;
debug.ShowDirectionalLight = true;   // 方向光方向线
debug.ShowPointLight = true;         // 点光范围球（能一眼看出 AttenuationRadius 设小了）
debug.ShowSpotLight = true;          // 聚光灯锥体（看内外锥）
debug.ShowBoundingBox = true;        // 网格包围盒
```

有额外性能开销，建议只在开发时开。

## 常见坑

| 症状 | 原因与处理 |
|---|---|
| 模型一片黑或只有背景 | 默认管线**没有光就不显示物体**。先加一盏 `DirectionalLight`。背景不受光照影响，见[环境与背景](./environment.md) |
| 第 5 盏同类灯完全没反应 | 每类默认 4 盏上限，超出无光无影；管线创建前调 `*LightLimit` |
| 影子缺一整块 / 远处没影 | 方向光 `ShadowConfig.Width/Height/FarPlane` 覆盖不到；或这盏灯根本没走 CSM（`MainDirectionalLight`、`CastShadow` 都要对） |
| 近处阴影边缘有阶梯状锯齿 | 提高 `CsmShadowMapResolution`、`CsmCascadeCount`，或加大 `CsmSplitLambda` 把精度往近处挪 |
| 开了 CSM 改 `ShadowConfig` 没效果 | CSM 的覆盖范围来自相机视锥，`Width/Height` 只服务单图路径 |
| 半透明物体不投影 | 预期行为：阴影图只收 `Opaque` / `Masked` 材质 |
| 一开阴影就掉帧 | 逐盏检查 `CastShadow`；投影点光最贵（六面） |
| 阴影图上限改了不生效 | `*LightLimit`、`CsmCascadeCount`、`CsmShadowMapResolution` 必须在管线创建前设 |

## 跑起来看

- **Shadows** 示例把两条阴影路径（CSM 主光 + 单图方向光）并排展示，所有参数都能实时调，并从真实生效的 GPU 状态读回分辨率与级联数：[ShadowsDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Shadows/ShadowsDemo.axaml.cs)
- 场景一片黑、物体看不见，还有另一批原因，见[常见坑与排障](./troubleshooting.md)。
