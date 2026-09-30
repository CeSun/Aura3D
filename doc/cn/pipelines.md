---
section: advanced
order: 3
---

# 选择与配置管线

渲染管线决定了场景的视觉风格：光照模型、阴影、色调映射、抗锯齿，以及画面里每一张贴图怎么被读。这一篇面向**使用管线**的人——怎么在几条内置管线里选一条、怎么通过 `PipelineSettings` 把它的画面调到想要的效果。若你要自己写管线或写 `RenderPass`，去 [自定义渲染管线](./custom-pipeline.md)。

## 能做什么

Aura3D 附带一组内置管线，覆盖从写实到风格化的常见需求。选管线本质是两件事：

1. **选一条**——在 XAML 里用 `x:TypeArguments` 指定，或在代码里给 `CreateRenderPipeline` 赋一个工厂委托。
2. **配它**——通过 `PipelineSettings` 调整深度精度、光源数量、曝光、阴影级联、抗锯齿、调试可视化。

## 内置管线一览与选型

| 管线 | 风格 / 用途 | 装哪个包 | 在 Core 里？ |
|---|---|---|---|
| `BlinnPhongPipeline` | 写实前向渲染，Blinn-Phong 光照模型（**默认**） | `Aura3D.Avalonia` | 是，无需额外装 |
| `NoLightPipeline` | 无光照，直出材质颜色，调试或风格化 | `Aura3D.Avalonia` | 是 |
| `PointCloudPipeline` | 点云场景，内置点大小与颜色属性 | `Aura3D.Avalonia` | 是 |
| `PBRDeferredPipeline` | 基于物理的 Metallic-Roughness 工作流，延迟渲染架构 | `Aura3D.Pipeline.PBR` | 需额外装 |
| `PBRForwardPipeline` | 同样的 PBR 工作流，前向渲染架构 | `Aura3D.Pipeline.PBRForward` | 需额外装 |
| `CelShadingPipeline` | 卡通 / Toon 非写实着色 | `Aura3D.Pipeline.CelShading` | 需额外装 |

BlinnPhong 是默认管线，支持方向光 / 点光 / 聚光（每类最多 4 盏）、阴影、骨骼动画、透明与半透明材质——直接用 `Aura3DView` 就是它，无需任何配置。

安装扩展管线（按需）：

```shell
# PBR 延迟渲染管线
dotnet add package Aura3D.Pipeline.PBR

# PBR 前向渲染管线
dotnet add package Aura3D.Pipeline.PBRForward

# 卡通渲染管线
dotnet add package Aura3D.Pipeline.CelShading
```

> [!NOTE]
> `BlinnPhong`、`NoLight`、`PointCloud` 都在 `Aura3D.Core` 里，随 `Aura3D.Avalonia` 一起装好，**不需要**额外的安装命令。

### 怎么指定管线

两种方式任选其一，效果相同。

**方式一 — XAML `x:TypeArguments`：**

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:acr="clr-namespace:Aura3D.Core.Renderers;assembly=Aura3D.Core"
    ...>
    <a:Aura3DView x:TypeArguments="acr:NoLightPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

Core 内置管线用 `acr:`（`Aura3D.Core.Renderers`）；扩展管线换成各自命名空间：

```xaml
<!-- PBR：xmlns:pbr="clr-namespace:Aura3D.Pipeline.PBR;assembly=Aura3D.Pipeline.PBR" -->
<a:Aura3DView x:TypeArguments="pbr:PBRDeferredPipeline" ... />

<!-- PBR 前向：xmlns:pbrf="clr-namespace:Aura3D.Pipeline.PBRForward;assembly=Aura3D.Pipeline.PBRForward" -->
<a:Aura3DView x:TypeArguments="pbrf:PBRForwardPipeline" ... />

<!-- 卡通：xmlns:cel="clr-namespace:Aura3D.Pipeline.CelShading;assembly=Aura3D.Pipeline.CelShading" -->
<a:Aura3DView x:TypeArguments="cel:CelShadingPipeline" ... />

<!-- 点云（Core 内置）：xmlns:core="clr-namespace:Aura3D.Core.Renderers;assembly=Aura3D.Core" -->
<a:Aura3DView x:TypeArguments="core:PointCloudPipeline" ... />
```

**方式二 — 代码 `CreateRenderPipeline`：**

```csharp
view.CreateRenderPipeline = scene => new NoLightPipeline(scene);
// 或
view.CreateRenderPipeline = scene => new PointCloudPipeline(scene);
```

> [!WARNING]
> `CreateRenderPipeline` **必须在 GL 初始化之前赋值**（即控件加载之前）。等 `SceneInitialized` 触发时管线已经建好，此时再设就晚了。

> [!TIP]
> 想在运行时来回切换管线，把工厂委托集中成一张表最省事。Gallery 的管线对比 demo 就是这么做的——见 [PipelinesDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Pipelines/PipelinesDemo.axaml.cs) 与 [PipelineCatalog.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/PipelineCatalog.cs)，可在 BlinnPhong / NoLight / PBR 延迟 / PBR 前向 / 卡通五条管线间切换，直观看出「谁读了哪张贴图、谁压根不读」。

## 配置管线：PipelineSettings

`PipelineSettings` 控制管线的行为与画面效果。它的关键点在于**分两类**：一类（深度格式、光源上限、CSM 级联数与分辨率）必须在管线创建前设好，之后改了不生效；另一类（曝光、环境光、抗锯齿开关、调试可视化等）随时可改，改完下一帧立刻见效。

### 配置方式

**XAML**（在控件加载前随控件一起声明，满足"创建前"要求）：

```xml
<Window xmlns:core="clr-namespace:Aura3D.Core.Renderers;assembly=Aura3D.Core" ...>
    <a:Aura3DView x:TypeArguments="cel:CelShadingPipeline">
        <a:Aura3DView.PipelineSettings>
            <core:PipelineSettings DepthFormat="DepthComponent32f"
                                   DirectionalLightLimit="2"
                                   ToneMappingExposure="1.2f" />
        </a:Aura3DView.PipelineSettings>
    </a:Aura3DView>
</Window>
```

**代码**——创建前设一次性参数：

```csharp
var view = new Aura3DView<CelShadingPipeline>
{
    PipelineSettings = new PipelineSettings
    {
        DepthFormat = TextureFormat.DepthComponent32f,
        DirectionalLightLimit = 2,
    }
};
```

**运行时随时改**（改完下一帧生效）：

```csharp
view.Scene.RenderPipeline.Settings.ToneMappingExposure = 1.3f;
view.Scene.RenderPipeline.Settings.EnableFxaa = false;
```

### 深度格式 DepthFormat

控制前后遮挡判断的精度——可以理解为"判断谁在前面谁在后面的标尺刻度有多密"。

| 取值 | 精度 | 适用场景 |
|---|---|---|
| `DepthComponent16` | 16 位 | 普通场景 |
| `DepthComponent24` | 24 位 | 较大场景，或需要更精细的深度判断 |
| `DepthComponent32f` | 32 位浮点（默认） | 超大规模场景（城市、地形），16 位不够用时 |

> [!TIP]
> 场景里出现远处物体闪烁、两个面叠在一起分不清谁在前（俗称 Z-Fighting），就是精度不够，换 `DepthComponent32f` 即可。

### 光源数量上限

限制同时生效的光源个数，超出上限的光不产生光照也不投影。三者默认都是 `4`，有效范围 `1..10`：调小省性能，调大支持更多灯。

| 参数 | 对应光源 |
|---|---|
| `DirectionalLightLimit` | 方向光——太阳、全局平行光 |
| `PointLightLimit` | 点光——灯泡、蜡烛，向四周发光 |
| `SpotLightLimit` | 聚光灯——手电筒、舞台追光，锥形光源 |

> 光源本身怎么用、阴影怎么配，见 [光照与阴影](./lighting.md)。

### 色调映射与亮度

色调映射把 HDR 颜色压缩到屏幕能显示的范围，这两个参数决定画面整体明暗。

| 参数 | 作用 | 默认值 |
|---|---|---|
| `ToneMappingExposure` | 整体亮度，类似相机曝光补偿，越大越亮 | `0.7` |
| `BrightnessClamp` | 亮度上限，超过就截断，防局部过曝 | `4.0` |

> [!TIP]
> 画面偏暗 → 加大 `ToneMappingExposure`；高亮区白成一片 → 加大 `BrightnessClamp`。

### 环境光强度 AmbientIntensity

没有光直射的地方也不是全黑——环境光模拟场景中散射的微弱光线。`0` 暗部全黑，`0.1`（默认）轻微提亮，`0.5` 以上暗部明显偏亮、呈风格化效果。

> [!NOTE]
> PBR 管线用基于物理的 IBL 环境光，**不吃** `AmbientIntensity`（它有独立的 IBL 环境强度）。

### 级联阴影贴图 CSM

方向光阴影在远距离容易出现锯齿，CSM 把视锥体分成多个级联、每级独立阴影贴图来解决。仅 `SupportsCSM = true` 的管线（如 BlinnPhong）生效。用 `Scene.MainDirectionalLight` 指定哪盏方向光走 CSM，其余退化为单张阴影贴图：

```csharp
view.Scene.MainDirectionalLight = dl;  // 该方向光使用 CSM
```

| 参数 | 作用 | 默认值 |
|---|---|---|
| `CsmCascadeCount` | 级联数量，设为 1 回退单阴影贴图（`1..4`） | `3` |
| `CsmSplitLambda` | PSSM 分割参数，0=均匀、1=对数（`0..1`） | `0.5` |
| `CsmShadowMapResolution` | 每级联阴影贴图分辨率（须大于 0） | `1024` |

> `CsmCascadeCount` 与 `CsmShadowMapResolution` 必须在管线创建前设；`CsmSplitLambda` 可运行时调整。CSM 的原理与调参见 [光照与阴影](./lighting.md#csm)。

### 功能开关 EnableFxaa / EnableFrustumCulling

| 参数 | 作用 | 默认值 |
|---|---|---|
| `EnableFxaa` | FXAA 抗锯齿，让物体边缘更平滑 | `true` |
| `EnableFrustumCulling` | 只渲染相机视野内的物体，看不见的自动跳过 | `true` |

> [!TIP]
> 性能不足时关 `EnableFxaa` 省一点开销。`EnableFrustumCulling` 一般不用关，物体多时能显著提速——详见下文[视锥体剔除](#视锥体剔除)。

### 调试可视化 Debug

`PipelineSettings.Debug`（一个 `DebugSettings`）控制内置调试绘制，开发期帮助看清场景结构，全部属性都可运行时随时改：

```csharp
var debug = view.Scene.RenderPipeline.Settings.Debug;
debug.Enable = true;                // 总开关
debug.ShowBoundingBox = true;       // 所有网格的包围盒
debug.ShowDirectionalLight = true;  // 方向光方向线
debug.ShowPointLight = true;        // 点光范围球
debug.ShowSpotLight = true;         // 聚光灯锥体
debug.ShowCamera = true;            // 摄像机视锥体
debug.ShowBone = true;              // 骨骼层次
```

> [!WARNING]
> 调试绘制有额外性能开销，建议仅开发时开启。`Debug` 不能为 `null`；所有强度/色调映射浮点参数必须是有限非负数，否则赋值时抛 `ArgumentOutOfRangeException`。

### 哪些设置需要什么时候设

这是本篇最容易写错的地方——照着这张表放代码就对了。

| 设置 | 必须在管线创建前设？ | 适用管线 |
|---|---|---|
| `DepthFormat` | ✅ 是，之后改了不生效 | 全部 |
| `DirectionalLightLimit` | ✅ 是 | BlinnPhong / PBR / CelShading |
| `PointLightLimit` | ✅ 是 | BlinnPhong / PBR / CelShading |
| `SpotLightLimit` | ✅ 是 | BlinnPhong / PBR / CelShading |
| `CsmCascadeCount` | ✅ 是 | BlinnPhong |
| `CsmShadowMapResolution` | ✅ 是 | BlinnPhong |
| `CsmSplitLambda` | ❌ 随时可改 | BlinnPhong |
| `ToneMappingExposure` | ❌ 随时可改 | BlinnPhong / PBR / CelShading |
| `BrightnessClamp` | ❌ 随时可改 | BlinnPhong / PBR / CelShading |
| `AmbientIntensity` | ❌ 随时可改 | BlinnPhong / CelShading |
| `EnableFxaa` | ❌ 随时可改 | 全部 |
| `EnableFrustumCulling` | ❌ 随时可改 | 全部 |
| `Debug.*` | ❌ 随时可改 | 全部 |

> [!NOTE]
> NoLight 管线不涉及光照和色调映射，光源、曝光、环境光参数对它无效。

### 向后兼容

`RenderPipeline` 上原有的属性（如 `EnableFrustumCulling`、`DirectionalLightLimit`）仍可用，内部自动转发到 `Settings`：

```csharp
// 以下两种写法等价
pipeline.EnableFrustumCulling = false;
pipeline.Settings.EnableFrustumCulling = false;
```

## 视锥体剔除

视锥体剔除让渲染器只绘制相机视野内的物体，跳过视野外的一切，减少绘制开销。由 `PipelineSettings.EnableFrustumCulling` 控制，**默认开启**。开启时，管线每帧针对每个相机算出可见网格列表，`RenderVisibleMeshesInCamera` 这类渲染方法只遍历这份列表；关闭时会把场景里所有网格都画一遍（物体极少、或需要强制全遍历的场景才关）。对多相机，剔除按每个相机各自计算。

## 多摄像机渲染

一个场景可以同时渲染多个相机视角，例如分屏、小地图。场景里所有 `Camera` 节点会被管线自动发现并逐一渲染，每个注册为 `RenderPassGroup.EveryCamera` 的 Pass 会对每个相机各执行一次（Pass 分组机制见 [自定义渲染管线](./custom-pipeline.md)）。

```csharp
// 在 SceneInitialized 中创建第二个相机
var secondCamera = new Camera
{
    Position = new Vector3(10, 5, 0),
    IsRenderBackground = false  // 第二个视角不重复渲染天空盒
};
secondCamera.LookAt(Vector3.Zero);
scene.AddNode(secondCamera);
```

### 渲染到纹理

用 `ControlRenderTarget` 把某个相机的画面渲染到纹理，用于小地图、监控画面等：

```csharp
// 创建离屏渲染目标，挂到相机上
var renderTarget = new ControlRenderTarget(width, height);
secondCamera.RenderTarget = renderTarget;

// 渲染后，该目标里就是这个相机的画面
// 可在 SceneUpdated 中读取其纹理，作为其他材质的输入
```

相机的更多用法（投影类型、`FitToBoundingBox`、控制器）见 [相机与视角控制](./camera.md)。

## GPU 资源自动管理（简述）

当你用 `view.AddNode(...)` 把网格、材质、纹理、模型加入场景时，管线会自动接管这些资源的 GPU 侧状态：首次用到时按需上传，内容变化后重新同步，不再被引用时定期回收——日常使用不需要手动干预，也没有需要手动调用的注册接口。

上下文丢失与恢复、显存释放与重建、`IGpuState` 契约等深入内容属于 [GPU 资源生命周期](./gpu-resource-lifecycle.md)，出问题或要精细控显存时再读那篇即可。

## PBR 材质参数（用法示例）

选了 PBR 管线后，它按 Metallic-Roughness 工作流读材质的通道贴图。这里给一个"怎么喂参数"的例子；材质通道本身、以及自定义着色器机制留给 [自定义材质与着色器](./custom-material.md)。

```csharp
var mesh = new Mesh();
mesh.Geometry = new SphereGeometry();
mesh.Material = new Material();

// 基础色（BaseColor 是扩展属性，先 new 再赋值，别写进对象初始化器）
mesh.Material.BaseColor = Texture.CreateFromColor(Color.FromArgb(255, 200, 50, 50));

// 法线贴图
mesh.Material.SetTexture("Normal",
    Texture.CreateFromColor(Color.FromArgb(128, 128, 255)));

// 金属度/粗糙度贴图：R 通道 = 金属度，G 通道 = 粗糙度
mesh.Material.SetTexture("MetallicRoughness",
    Texture.CreateFromColor(Color.FromArgb(200, 100, 0)));

view.AddNode(mesh);
```

> [!NOTE]
> `MetallicRoughness` 里 R 存金属度、G 存粗糙度，是 PBR 约定的通道打包方式。卡通管线（CelShading）的用法与默认管线一致——加载模型、设置光源后渲染风格自动变为卡通着色。

## 常见坑

- **`CreateRenderPipeline` 设晚了**：必须在 GL 初始化前（控件加载前）赋值；放进 `SceneInitialized` 回调里已经太迟。
- **创建前的参数当运行时参数改**：`DepthFormat`、三个 `*LightLimit`、`CsmCascadeCount`、`CsmShadowMapResolution` 在管线建好之后再改不会生效，要重建管线（重开控件）才行。对照[上面那张表](#哪些设置需要什么时候设)放代码。
- **PBR 里调 `AmbientIntensity` 没反应**：PBR 用 IBL 环境光，不受此参数控制，属正常现象。
- **给 NoLight 管线配光/曝光**：NoLight 不走光照和色调映射，这些设置对它无效。
- **非负校验**：强度、色调映射类浮点参数传负数或 `NaN`/`Infinity`，`Debug` 传 `null`，都会在赋值时抛异常。
- **XAML `x:TypeArguments` 命名空间写错**：Core 内置管线在 `Aura3D.Core.Renderers`，PBR / 卡通要各自 `clr-namespace` 指到对应程序集，且先装好对应 NuGet 包。

## 可运行示例

- Gallery 管线对比 demo（同一幕场景在 BlinnPhong / NoLight / PBR 延迟 / PBR 前向 / 卡通间切换，看每条管线读哪张通道）：[PipelinesDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Pipelines/PipelinesDemo.axaml.cs)
- 管线到具体类型的映射表：[PipelineCatalog.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/PipelineCatalog.cs)

## 下一步

- 要自己写管线或 `RenderPass`：[自定义渲染管线](./custom-pipeline.md)
- 材质与贴图通道机制：[自定义材质与着色器](./custom-material.md)
- 显存与上下文恢复：[GPU 资源生命周期](./gpu-resource-lifecycle.md)
