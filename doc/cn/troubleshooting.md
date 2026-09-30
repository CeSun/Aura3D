---
section: notes
order: 2
---

# 常见坑与排障

这篇按**症状**索引：先在下表找到你的现象，跳到对应小节；每条给「多半原因 → 怎么办 → 去哪读深」。这里不收整篇教程，只收踩过无数次的坑。

| 症状 | 跳转 |
|---|---|
| 画面全黑 / 什么都不显示 | [#black-screen](#black-screen) |
| 物体表面闪烁、叠面分不清前后（Z-Fighting） | [#z-fighting](#z-fighting) |
| 远处或动画中的模型被误剔除、凭空消失 | [#culling](#culling) |
| macOS 上视口不出图 | [#macos](#macos) |
| iOS 上开混合后黑屏（浮点渲染目标） | [#ios-blend](#ios-blend) |
| 浏览器里某些东西静默消失，桌面端正常 | [#browser-drops](#browser-drops) |
| 浏览器首帧崩溃 / 启动即中止 | [#browser-crash](#browser-crash) |
| iOS / 浏览器首帧特别慢 | [#slow-first-frame](#slow-first-frame) |
| 直接改骨骼矩阵不生效 | [#bone-matrix](#bone-matrix) |
| 粒子完全看不到 | [#particles](#particles) |
| 逐实例属性（颜色等）不生效或全错 | [#instance-location](#instance-location) |
| 自定义 Pass 首帧画空，第二帧起才正常 | [#shader-order](#shader-order) |
| Uniform 怎么设都没反应 | [#uniform-silent](#uniform-silent) |
| GLSL 看着没错却编译失败 | [#glsl-chars](#glsl-chars) |
| PipelineSettings 改了不生效 | [#settings-timing](#settings-timing) |
| 编译报「`Color` 是 `System.Drawing.Color` 和 `Avalonia.Media.Color` 的歧义引用」 | [#color-ambiguity](#color-ambiguity) |
| 第 5 盏同类型光没效果 / 环境光参数不动作 | [#limits-ambient](#limits-ambient) |
| 切后台、分离控件回来后画面要不要重建 | [#context-restore](#context-restore) |

## 画面不出东西

<a id="black-screen"></a>
### 画面全黑 / 什么都不显示

按命中率排查四个原因：

1. **没加光源**——默认 BlinnPhong 管线没光就是全黑。加一盏 `DirectionalLight` 并设好 `RotationDegrees`（方向光靠旋转定义照射方向，转背对模型同样是一片黑）。
2. **相机没对准**——场景必须有相机，`Aura3DView.MainCamera` 是默认相机；确认它没躲到物体背面或钻进球体内部，可用 `camera.LookAt(...)` 或 `FitToBoundingBox(model.BoundingBox, padding: 0.5f)` 对准目标。
3. **`CreateRenderPipeline` 赋值太晚**——它必须在控件 GL 初始化之前（即加载之前）赋值；在 `SceneInitialized` 之后才设就晚了，指定的管线不会生效。
4. **`BlendMode.Masked` 把半透明当裁剪丢弃**——Masked 下 alpha ≤ `AlphaCutoff`（默认 0.5）的像素直接 `discard`，半透明贴图会大面积破洞甚至整块消失。要真正的半透明请用 `BlendMode.Translucent`。

光与相机细节见 [光照与阴影](./lighting.md)、[相机与视角控制](./camera.md)；管线赋值时机见 [选择与配置管线](./pipelines.md)。

<a id="z-fighting"></a>
### 物体表面闪烁、叠面分不清前后（Z-Fighting）

- **多半原因**：深度缓冲精度不够，两个几乎重叠的面无法判别先后。
- **怎么办**：把 `PipelineSettings.DepthFormat` 设为 `DepthComponent32f`（默认即 32f；若曾为省内存降到 16/24，调回来）。注意它在管线创建后改无效，须在创建前设置。
- **深读**：[选择与配置管线](./pipelines.md)。

<a id="culling"></a>
### 远处或动画中的模型被误剔除、凭空消失

- **多半原因**：视锥剔除用的是按静态顶点算出的 **T-Pose 包围盒**；骨骼动画（行走、跳跃等）让模型明显超出包围盒时，还在视野里的网格会被剔掉。
- **怎么办**：给模型 `model.BoundingBoxPadding = new Vector3(2f)` 各方向扩包围盒，或 `model.CustomBoundingBox = new BoundingBox(min, max)` 完全覆盖动画范围；验证时可临时 `Settings.EnableFrustumCulling = false`，或用 `Settings.Debug.ShowBoundingBox = true` 直接看包围盒。静止模型无需调整。
- **深读**：[动画系统](./animation.md)。

<a id="macos"></a>
### macOS 上视口不出图

- **多半原因**：宿主走了非 OpenGL 的原生渲染模式。
- **怎么办**：在 `AppBuilder` 里显式钉 `AvaloniaNativeRenderingMode.OpenGl`。iOS 不需要这种特判（它有自己的 ANGLE 路径）。
- **深读**：[平台与渲染后端](./platform-render-backends.md)。

## 平台特有：静默失败

<a id="ios-blend"></a>
### iOS 上开混合后黑屏（浮点渲染目标）

- **多半原因**：iOS 的 ANGLE(Metal) 后端不提供 `GL_EXT_float_blend`；混合开启时绘制到 **32 位浮点颜色附件**（如 `Rgba32f`）会被当作 `GL_INVALID_OPERATION` 静默丢弃——症状是黑屏而不是报错。
- **怎么办**：HDR 渲染目标统一改用 `Rgba16f`（框架内置管线就是这么做的）；自定义 Pass 不要申请 `Rgba32f` 颜色附件后又开混合。
- **深读**：[平台与渲染后端 → GLES 3.0 子集](./platform-render-backends.md)。

<a id="browser-drops"></a>
### 浏览器里某些东西静默消失，桌面端正常

- **多半原因**：WebGL2 校验严格，违规的整条 draw 被**静默丢弃**。四大诱因：① 启用的 draw buffer 没有对应"被写入的片元输出"（depth-only FBO 忘 `glDrawBuffers([GL_NONE])`；片元声明了 `out` 但所有 `#ifdef` 分支都不写它）；② `UseShader`/`UseShader_Internal` 顺序写反（见 [#shader-order](#shader-order)）；③ 颜色附件用了 RGB 族内部格式（`RGB8`/`RGB16F`/`RGB32F` 不可渲染，必须 RGBA 族）；④ 用了 WebGL2 不存在的入口点（如 `glGetTexLevelParameteriv`）。
- **怎么办**：自定义 Pass 按 GLES 3.0 子集检查以上四条；内置管线本身不受影响。
- **深读**：[平台与渲染后端 → GLES 3.0 子集](./platform-render-backends.md)。

<a id="browser-crash"></a>
### 浏览器首帧崩溃 / 启动即中止

- **多半原因**：.NET 10 Release 裁剪删掉了 WASM interpreter-to-native trampoline（日志走到 `[aura3d-webgl] output ...` 后 `aot-runtime-wasm.c:188 <disabled>`、`exit(1)`），或 linked-icall 表与 `System.Private.CoreLib` 不同步（`Your mono runtime and class libraries are out of sync` / `function signature mismatch`）。启动即 `DllNotFoundException: libSkiaSharp` 则是 wasm 没链上原生库。
- **怎么办**：应用 `.csproj` 的 Release 下**同时**设三项：`PublishTrimmed=false` + `RunAOTCompilation=false` + `WasmLinkIcalls=false`（只设其一必挂其一）；改完用全新 `bin`/`obj`/发布目录，部署时整体替换远端静态目录并刷新 `dotnet.js` 缓存。
- **深读**：[平台与渲染后端 → .NET 10 Release 必填配置](./platform-render-backends.md#browser-net10-release-config)。

<a id="slow-first-frame"></a>
### iOS / 浏览器首帧特别慢——属正常

- **多半原因**：iOS 上 ANGLE 在运行时把 GLSL 翻成 MSL 逐变体编译、无磁盘缓存；浏览器上 Mono 解释器执行宿主代码慢。实测：iOS PBR+CSM 场景首帧约 5.6–5.9 s、简单场景 0.4–0.7 s；浏览器重场景首帧可能以分钟计。
- **怎么办**：等首帧出来即正常；不要为性能单独开 `RunAOTCompilation`（会退化成 [#browser-crash](#browser-crash) 的崩溃）。
- **深读**：[平台与渲染后端](./platform-render-backends.md)。

## 动画、实例与粒子

<a id="bone-matrix"></a>
### 直接改骨骼矩阵不生效

- **多半原因**：骨骼矩阵每帧在**动画采样阶段**由 `IAnimationSampler.Update()` 重算，你在 `SceneUpdated` 里的直接写值会被覆盖。
- **怎么办**：程序化控制骨骼需要自定义 `IAnimationSampler`，或确保在动画采样之后覆盖矩阵。
- **深读**：[动画系统 → 骨骼手动操作](./animation.md)。

<a id="instance-location"></a>
### 逐实例属性（颜色等）不生效或全错

- **多半原因**：顶点着色器里声明的 `layout(location = N)` 与 `BuildInVertexAttribute` 枚举值不一致（location 就是枚举的数字），或 `#ifdef INSTANCED_MESH` 分支忘了自己乘逐实例 `modelMatrix`——覆盖顶点着色器就等于接管了实例变换。
- **怎么办**：对照 [内置 Pass 与着色器宏速查](./reference-shaders.md) 的 location 约定表；`InstancedMesh.SetAttributeEnabled("InstanceNormalTransform", false)` 可关闭不需要的实例法线矩阵以省带宽。
- **深读**：[实例化渲染](./instanced-rendering.md)。

<a id="particles"></a>
### 粒子完全看不到

- **多半原因**：没调 `Play()`；或关着自动出帧（`AutoRequestNextFrameRendering = false` 却没逐帧 `RequestNextFrameRendering()`）。
- **怎么办**：配置完成后 `ps.Play()`；其余「粒子在原点 / 不移动 / 网格模式全黑 / Flipbook 不动」等按参数逐条排查。
- **深读**：[粒子系统 → 常见问题排查](./particle-system.md)。

## 着色器与自定义 Pass

<a id="shader-order"></a>
### 自定义 Pass 首帧画空，第二帧起才正常

- **多半原因**：`UseShader_Internal()` 写在了 `UseShader(...)`/`AddDefines(...)` 之前——本帧绑定的还是上一帧（首帧是空宏）的变体。
- **怎么办**：一律按 `UseShader` → `UseShader_Internal` → 设 uniform → draw 的顺序写；另外宏顺序影响缓存 key（`;` 拼接），建议 `UseShader` 一次性声明所有宏。
- **深读**：[自定义渲染管线](./custom-pipeline.md)。

<a id="uniform-silent"></a>
### Uniform 怎么设都没反应

- **多半原因**：`Uniform*` 系列方法在着色器未激活（`CurrentShader` 为空）或该 uniform 名在程序里不存在时**静默跳过、不报错**。不遍历网格的 Pass（纯 `RenderQuad`/`RenderCube`）必须手动调用 `UseShader_Internal()`。
- **怎么办**：先检查激活顺序（[#shader-order](#shader-order)），再核对 GLSL 里 uniform 名拼写；引擎固定提供 `modelMatrix`/`viewMatrix`/`projectionMatrix`/`cameraPosition`，材质参数按同名 uniform 绑定。
- **深读**：[自定义渲染管线](./custom-pipeline.md)、[自定义材质与着色器](./custom-material.md)。

<a id="glsl-chars"></a>
### GLSL 看着没错却编译失败

- **多半原因**：着色器源码里写了**中文注释或全角字符**。
- **怎么办**：GLSL 内注释只用 ASCII；解释性文字写在 C# 侧。
- **深读**：[自定义材质与着色器](./custom-material.md)。

## 配置与时机

<a id="settings-timing"></a>
### PipelineSettings 改了不生效

- **多半原因**：有些设置只能在**管线创建前**指定，创建后再改被忽略：`DepthFormat`、`DirectionalLightLimit`/`PointLightLimit`/`SpotLightLimit`、`CsmCascadeCount`、`CsmShadowMapResolution`。其余（`CsmSplitLambda`、`ToneMappingExposure`、`BrightnessClamp`、`AmbientIntensity`、`EnableFxaa`、`EnableFrustumCulling`、`Debug.*`）随时可改、下帧生效。
- **怎么办**：创建前的设置在 XAML `PipelineSettings` 或控件构造时指定；运行时调试用 `view.Scene.RenderPipeline.Settings.X = ...`。
- **深读**：[选择与配置管线](./pipelines.md)。

<a id="color-ambiguity"></a>
### 编译报「`Color` 是歧义的引用」

- **多半原因**：Aura3D 的颜色属性（`Material.BaseColor`、`Light.LightColor`、`Texture.CreateFromColor(...)` 等）收的是 **`System.Drawing.Color`**，而 Avalonia 工程里常用的是 `Avalonia.Media.Color`；两个命名空间同时 `using` 时裸写 `Color` 就冲突。
- **怎么办**：写全限定 `System.Drawing.Color.White`，或 `using DrawingColor = System.Drawing.Color;` 起别名。Avalonia 颜色转换：`System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)`。
- **深读**：[快速开始](./quickstart.md)。

<a id="limits-ambient"></a>
### 第 5 盏同类型光没效果 / 环境光参数不动作

- **多半原因**：默认每类光源上限 4 盏，超限的灯不产生光照和阴影；上限范围 1..10，且必须管线创建前调。另：`AmbientIntensity` 只对 BlinnPhong/CelShading 生效，**PBR 管线用 IBL 环境光、不吃这个参数**。
- **怎么办**：管线创建前提高对应 `*LightLimit`；PBR 场景想要环境亮度请配 HDR 环境贴图。
- **深读**：[光照与阴影](./lighting.md)、[选择与配置管线](./pipelines.md)。

## 上下文与生命周期

<a id="context-restore"></a>
### 切后台 / 控件分离再回来，画面要不要重建？

- **不用。** 上下文丢失后 `ContextLost` 触发、新上下文就绪时 `ContextRestored` 触发，全部 GPU 资源按需自动重建，画面自己回来；场景、节点、材质都在，**不需要重建场景**，也不会再次触发 `SceneInitialized`——场景只在首次初始化时构建一次。
- **注意**：不要订阅 `SceneInitialized` 去重复建场景来"应对丢失"；需要显式归还显存用 `Aura3DView.ReleaseGpuResources()`，彻底结束用 `DestroyScene()`。
- **深读**：[GPU 资源生命周期](./gpu-resource-lifecycle.md)。
