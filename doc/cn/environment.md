---
section: basics
order: 6
---

# 环境与背景

背景只由一个属性决定：`Scene.Background`。这一篇讲纯色、图片、HDR / 六面天空盒，以及贴图「看起来不对」时的采样参数。

> [!TIP]
> 背景完全不吃光照。没加光源时，模型一片黑，但背景照常显示——反过来说，「加了背景却看不到东西」多半是灯的问题，见[光照与阴影](./lighting.md)。

## 最短：换掉默认背景

```csharp
view.Scene.Background = Texture.CreateFromColor(Color.Gray);
```

`Scene` 构造时已经给了一张纯色背景（AliceBlue），所以新场景从来不是透明的。`Background` 的类型是 `OneOf<CubeTexture, Texture>`，两个分支走两条完全不同的画法：

| 你给的 | 引擎怎么画 |
|---|---|
| `CubeTexture`（立方体图） | 天空盒：按视线方向采样六个面，转镜头能看到不同朝向 |
| `Texture`（普通 2D 图） | 铺满整个窗口并拉伸，**不保持宽高比**，与镜头朝向无关 |

（颜色值 `Texture.CreateFromColor(...)` 收的是 `System.Drawing.Color`，0..255 通道。）

## 纯色与单张图片背景

```csharp
// 纯色
view.Scene.Background = Texture.CreateFromColor(Color.Gray);

// 一张图片：拉伸铺满窗口，适合品牌底图、渐变底
using (var stream = File.OpenRead("background.jpg"))
{
    view.Scene.Background = TextureLoader.LoadTexture(stream);
}
```

想要真正的「环绕天空」，必须给立方体图，见下面两节。

## HDR 全景图 → 天空盒

等距柱状全景 `.hdr` 不能直接当背景，要先烘成立方体图；第二个参数是**每个面的边长**：

```csharp
using (var stream = File.OpenRead("environment.hdr"))
{
    var hdri = TextureLoader.LoadHdrTexture(stream);

    // HDR 数据是线性值，标成 gamma 空间会被再解码一次，天空会明显发白
    hdri.SetIsGammaSpace(false);

    view.Scene.Background = HDRIToCubeTextureConverter.ConvertFromTexture(hdri, 1024);
}
```

转换有实际开销，放在初始化阶段做一次即可，别每帧重烘。面边长越大天空越清晰，也越费显存。

## 六面立方体天空盒

最省事的重载直接给六个文件名：

```csharp
var cube = TextureLoader.LoadCubeTexture(new List<string>
{
    "px.png", "nx.png", "py.png", "ny.png", "pz.png", "nz.png",   // +X -X +Y -Y +Z -Z
});

view.Scene.Background = cube;
```

从内存/资源包读就自己组 `List<Stream>`——这种情况下 **stream 由你负责关闭**：

```csharp
var faces = new[] { "px.png", "nx.png", "py.png", "ny.png", "pz.png", "nz.png" };
var streams = new List<Stream>();

foreach (var face in faces)
{
    streams.Add(File.OpenRead(face));
}

try
{
    view.Scene.Background = TextureLoader.LoadCubeTexture(streams);
}
finally
{
    foreach (var s in streams) s.Dispose();
}
```

硬性要求：**正好 6 张**、六张**尺寸一致**、**通道格式一致**（都是 RGB 或都是 RGBA），任何一条不满足直接抛异常。从文件加载的立方体图会被标成 gamma 空间（`IsGammaSpace = true`），这是 LDR 图片的默认处理。

## 让某个视角不画背景

背景是**逐相机**开关的：

```csharp
camera.IsRenderBackground = false;   // 默认 true
```

多摄像机/小地图场景里，副视角（`scene.AddNode(secondCamera)` 那种）通常关掉它，避免重复绘制天空盒。透视和正交投影都能画天空盒（正交下有专门分支）。相机本身的参数见[相机与视角控制](./camera.md)。

## 贴图看起来不对：调采样

加载器给的默认值已经能用，但两类症状需要你手动调：**边缘拉丝/接缝**和**颜色发白或过艳**。可调项在 `Texture` / `CubeTexture` 上，用链式方法或属性都行：

```csharp
var texture = TextureLoader.LoadTexture(stream)
    .SetWrapS(TextureWrapMode.Repeat)        // 横轴（U）越界行为，默认 ClampToEdge
    .SetWrapT(TextureWrapMode.MirroredRepeat) // 纵轴（V），默认 ClampToEdge
    .SetMinFilter(TextureFilterMode.Linear)   // 缩小时，默认 Linear
    .SetMagFilter(TextureFilterMode.Nearest)  // 放大时，默认 Linear
    .SetColorFormat(ColorFormat.RGBA)         // 通道数：RGB 或 RGBA
    .SetIsGammaSpace(true);                   // 色彩空间，默认 false
```

| 症状 | 怎么调 |
|---|---|
| 贴图平铺处边缘被拉成一条丝 | `SetWrapS/SetWrapT` = `Repeat`（平铺）或 `MirroredRepeat`（镜像接缝更隐蔽） |
| 像素风糊成一片 | `SetMagFilter(TextureFilterMode.Nearest)` |
| 颜色比原图发白/过艳 | `SetIsGammaSpace(true)`（LDR 图），或反过来对线性数据设 `false` |
| 上传时通道数报错/颜色错位 | `SetColorFormat(ColorFormat.RGB \| RGBA)`，必须与像素数据实际通道数一致 |

> [!NOTE]
> 三个容易误会的点：
> 1. 滤镜只有 `Nearest` / `Linear` 两档——引擎不为 `Texture`/`CubeTexture` 生成 mipmap，所以没有 mipmap 相关选项。
> 2. `ColorFormat` 描述的是**通道数**，不是色彩空间；色彩空间由 `IsGammaSpace` 决定（为 `true` 时按 sRGB 内部格式上传）。
> 3. 立方体图还有第三个维度的包裹，用属性直接设：`cube.WrapR = TextureWrapMode.ClampToEdge;`

## 背景能当光源吗

- **默认 BlinnPhong 管线**：不能。背景只是画面。暗部亮度由 `PipelineSettings.AmbientIntensity`（默认 0.1，设 0 则暗部全黑）决定。
- **PBR 管线**：能。`Scene.Background` 是唯一的环境光入口——引擎会把它烘成辐照度图与预滤波反射图（IBL），金属球上的反射就来自它。

> [!IMPORTANT]
> **没设立方图不等于不烘 IBL。** 这时两条 PBR 管线会退到 `PBRPipelineBase.DefaultIblAmbientCubeTexture`——一张由 `Texture.CreateFromColor(White)` 转出来的 16px 白立方图。
> 纯白意味着环境项没有方向、预滤波反射没有形状：金属会变成一坨均匀发亮的球，看着像「IBL 没生效」，其实是生效了但没有内容可反射。
> 所以只要想让金属度/粗糙度、法线一类通道**看得出效果**，场景就得挂一张有内容的立方图（HDR/六面天空盒），示例里的 PBR 页面正是这么做的。

> [!TIP]
> 换背景不用手动管缓存：给 `Scene.Background` 赋一个**新的**背景资源时，setter 会通知管线（`RenderPipeline.OnBackgroundChanged`），PBR 管线会把相机上烘好的 `IrradianceMap` 与 `PrefilteredEnvironmentMap` 作废，下一帧用新图重烘。需要手动干预的只有一种情况：背景资源引用没换、但内容被原地改过（例如同一张 `CubeTexture` 重新填充了数据）——这时调 `PBRPipelineBase.InvalidateIblBakeCaches()`（或参照 Environment 示例里的 `InvalidateIblCaches()`）。管线与 IBL 配置见[选择与配置管线](./pipelines.md)。

## 常见坑

| 症状 | 原因与处理 |
|---|---|
| 加了背景，物体还是黑的 | 背景不照明。默认管线要加光源，见[光照与阴影](./lighting.md) |
| 「天空盒」像一张壁纸、转头不动 | 你给的是普通 `Texture`（拉伸铺满分支），要 `CubeTexture` 才有环绕 |
| 背景随窗口比例被拉长 | 2D 图分支不保持宽高比；要贴合画面请用天空盒，或自己按宽高裁切源图 |
| 加载立方体图直接抛异常 | 张数不是 6、六张尺寸不同、或通道格式不统一 |
| 内存被图片占住不放 | `LoadCubeTexture(List<Stream>)` 不会替你关闭 stream，用完 `Dispose`（或改用文件名重载） |
| HDR 天空整体发白、金属反射刺眼 | 全景图必须 `SetIsGammaSpace(false)` |
| PBR 下金属球是一坨均匀发亮、看不出环境 | 场景没挂立方图，IBL 只能烘那张纯白立方图。挂上有内容的立方图，见下面「背景能当光源吗」 |
| 地面贴图边缘拉丝 | 默认 `ClampToEdge`，平铺材质要 `Repeat` |
| 第二个视角也画了一遍天空 | 那个相机 `IsRenderBackground = false` |

## 跑起来看

- **Environment**：五个环境来源，前两档是文件资产（1k HDRI 全景、六面天空盒），后三档是程序化全景图；同时演示 PBR 的辐照度/预滤波反射与重建缓存：[EnvironmentDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Environment/EnvironmentDemo.axaml.cs)
- **Background**：四种背景来源（引擎默认纯色、六面立方图、HDR 转换、平面拉伸图）逐一对比，并可在透视/正交之间切换观察天空盒：[SkyboxBackgroundDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Background/SkyboxBackgroundDemo.axaml.cs)
- **PBR 材质库**：三档典型实拍材质（锈蚀金属/砖墙/镀锌钢板）挂在 IBL 环境下渲染，金属球能直接看出环境反射的形状：[PbrMaterialsDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/PbrMaterials/PbrMaterialsDemo.axaml.cs)
- 贴图与材质通道的采样设置，见[材质与贴图](./material.md)。
