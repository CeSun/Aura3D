# 平台与渲染后端

`Aura3DView` 在各平台上拿 GL 上下文的来源不同。本文说明每个平台走哪条路、iOS 与浏览器需要额外准备什么，以及在 GLES 子集上写自定义 Pass 时要避的坑。

## 各平台的渲染后端

| 平台 | 宿主渲染器 | Aura3D 取 GL 上下文的方式 | 应用侧配置 |
|---|---|---|---|
| Windows / Linux | 桌面 OpenGL | Avalonia `OpenGlControlBase` | 无 |
| Android | OpenGL ES | Avalonia `OpenGlControlBase` | 无 |
| macOS | OpenGL | Avalonia `OpenGlControlBase` | 无需平台特判；若视口不出图，在 `AppBuilder` 里显式钉 `AvaloniaNativeRenderingMode.OpenGl` |
| iOS | Metal（Avalonia 默认） | **自持 ANGLE(Metal) 上下文 + Skia lease 合成**；宿主显式改为 OpenGl 时回退到 `OpenGlControlBase` | 无平台特判，但需链接 ANGLE 的 iOS framework |
| Browser (wasm) | WebGL2（Avalonia.Browser 的 Skia 合成器） | **借合成器的 WebGL2 上下文 + Skia lease 合成**，GLES 3.0 调用经 emscripten shim 落到 WebGL2 | wasm 链接开关由包自动注入；.NET 10 Release 发布必须显式关闭 trimming、AOT 与 icall linking，见下文 |

代码里三条路径分别位于 `src/Aura3D.Avalonia/Aura3DViewBase.OpenGl.cs`、`Aura3DViewBase.Angle.cs`（iOS）与 `Aura3DViewBase.WebGl.cs`（浏览器），共享的主体流程在 `Aura3DViewBase.cs`。应用侧引用 `Aura3DView` 的签名在所有平台一致。

## iOS：为什么自持 ANGLE

Avalonia 的 iOS 宿主默认使用 Metal 合成器，而该模式下 Avalonia 不提供 GL 互操作，`OpenGlControlBase` 会静默初始化失败（画面全白、无异常）。在此之前的做法是把整个宿主 App 强制成 `iOSRenderingMode.OpenGl`，代价是绑到已废弃的 EAGL 后端。

现在 `Aura3D.Avalonia` 的 iOS 目标内置了另一条路：自己创建 ANGLE(Metal) 的 EGL 上下文完成渲染，再把输出 `MTLTexture` 经 Skia lease 零拷贝合成上屏。宿主 App 不必再做任何渲染模式上的取舍。

两条路径编译进同一个二进制，首帧判定一次归属：

- **权威信号** — `OpenGlControlBase.OnOpenGlInit` 被回调，说明宿主开了 OpenGL 模式，ANGLE 分支整体退出，由 Avalonia 驱动。
- **次级信号** — 读一次 Skia lease 的 `GRContext.Backend`：`Metal` 表示走 ANGLE，其他表示交回 `OpenGlControlBase`。

判定结果会打一行日志 `[aura3d-angle] compositor backend=..., path=...`，排查时先看这行。

## iOS：ANGLE framework 从哪来

`Aura3D.Avalonia` 用 `DllImport("__Internal")` 解析 ANGLE 的 EGL/GLES2 符号（为的是避开 Apple 自带的 OpenGLES），所以两个 framework 必须由**应用**链接进主可执行文件，不能只留在类库里。

**这件事现在是自动的。** `Aura3D.Avalonia` 的 iOS 目标依赖 `Aura3D.Angle.iOS`，包里的 `buildTransitive` targets 会在应用工程里按 `$(RuntimeIdentifier)` 选择切片并注入 `NativeReference`——应用工程不需要写任何 ANGLE 配置。区间是精确锁定的（`[0.1.0]`）：切片与 P/Invoke 签名是 ABI 配对，不能让用户被动升到一个没配套测过的切片上，所以换切片必须连同版本号一起升，并重新发一次 `Aura3D.Avalonia`。

```shell
dotnet add package Aura3D.Avalonia   # iOS 目标会自动带进 Aura3D.Angle.iOS
```

包内含 `iossimulator-arm64` 与 `ios-arm64` 两份 ANGLE 切片。这个包有独立的发布通道：手动触发 `build-ios-lib.yml`，在表单里输入要发的版本号（作业用 `-p:Version=<输入>` 覆盖工程里写死的 `<Version>`，不改仓库文件也能发），同一次作业里 pack、核对版本与两份切片都在、再用 GitHub OIDC 推到 nuget.org；`pack.yml` 不再随列车推送它，产物集合里也没有它（浏览器侧的 `Aura3D.Avalonia.Browser` 同理，走 `build-browser-lib.yml`）。切片缺失时构建直接报错，不会静默不出图；输入的版本号已在 nuget.org 上时推送作业会红。版本号住在两个互不引用的地方：`Aura3D.Angle.iOS.csproj` 的 `<Version>` 是仓库内构建的默认值，`Directory.Packages.props` 的 `Aura3DAngleIosVersion` 只喂主包的精确区间——所以热修顺序是：跑切片作业发新版本 → 把这两处都补成刚发的号（两处不一致时，仓库里的这份切片和消费方按区间从 nuget.org 拿到的不是同一份，之后任何一次不带 `-p` 的 pack 都会产出错的号，作业在这一步就会红；只漏 props 那处则主包继续钉旧切片，作业只 warning）→ 再跑 `pack.yml` 重发主包。倒过来先跑 `pack.yml` 也不行：它按这个区间去 nuget.org restore，钉一个还没发布的版本这一步直接红，不会发出消费方拿不到依赖的包。

**手工方式**（需要自己出 ANGLE 产物时）：

1. 用 standalone ANGLE checkout（非 Chromium checkout）为 iOS 构建含 Metal 后端的产物，gn 参数只需 `enable_rust=false`，得到 `libEGL.framework` 与 `libGLESv2.framework`，放到 `src/Aura3D.Angle.iOS/native/iossimulator-arm64/` 与 `native/ios-arm64/`（这两份切片随包一起入库）。`src/Aura3D.Angle.iOS/build-angle-ios.sh --device` 把这条流程固化了下来；真机切片必须额外传 `ios_enable_code_signing = false`，否则 gn 阶段会因为找不到 "Apple Development" 身份而失败（CI 等无证书环境同理）。
2. 在自己的应用工程里直接 `PackageReference` 这个包也行（`dotnet add package Aura3D.Angle.iOS`），效果与经 `Aura3D.Avalonia` 传递一致。
3. 注意不要在 `ItemGroup` 上写 `Exists(...)` 条件：framework 不在时这段会静默跳过，编译照样通过，运行时 ANGLE 会话失败、视口不出图。找不到出图原因时先确认这两个 framework 真的被链接了（看 `<app>.app/Frameworks/` 里有没有它们）。
4. 设备要求：ANGLE 的 Metal 后端需要 Metal GPU family 4（A11 及以后）；tvOS 不支持。入库切片按 ANGLE 默认的 `ios_deployment_target` 构建，`minos` 为 18.0，宿主 App 的最低系统版本低于它时链接器会提示版本不匹配。

## 浏览器：为什么借合成器的 WebGL2 上下文

Avalonia.Browser 的 `WebGlContext` 是单例式的：`CanCreateSharedContext` 为 `false`，也不提供 `ISkiaSharpApiLeaseFeature` 之外的任何 GPU 互操作。因此 `OpenGlControlBase` 在 wasm 上永远初始化失败（与 iOS 的 Metal 合成器同构的静默失败），自建第二个 WebGL 上下文又无法与 Skia 的合成上下文共享资源。

浏览器分支的做法是**不建自己的上下文**：自定义绘制操作（`ICustomDrawOperation`）本来就运行在合成器的渲染线程上、且 Skia 的 WebGL2 上下文是 current 的，于是直接在这个上下文上分配输出纹理 + FBO 供管线写入，再经 lease 的 `GRContext` 以 `GRGlTextureInfo` 零拷贝导入合成。引擎侧的 GLES 3.0 调用不经过 Avalonia 的 GL 绑定，而是经 `DllImport("libSkiaSharp")` 拿到 emscripten 的 `eglGetProcAddress`，取回主 wasm 模块内的真实函数指针（`delegate* unmanaged`，Silk.NET 的 `GL.GetApi` 走这条路），由 emscripten 的 GLES→WebGL2 shim 翻译。

归属判定与 iOS 一样只做一次，日志前缀是 `[aura3d-webgl]`：

- **权威信号** — `OpenGlControlBase.OnOpenGlInit` 被回调（今天不会发生，留作 Avalonia 支持共享上下文后的回退位）。
- **次级信号** — 读一次 Skia lease 的 `GRContext.Backend`：`OpenGL` 表示走 WebGL2 分支，其他表示交回 `OpenGlControlBase`。

## 浏览器：wasm 链接开关从哪来

GLES 入口点必须由**应用**自己的 wasm 模块带出来：不链原生库时模块里没有 `libSkiaSharp` 的符号，应用启动即 `System.DllNotFoundException: libSkiaSharp`（在 `SKImageInfo` 的静态构造里，没有任何上下文线索）。所以三件事要在应用工程里成立——`WasmBuildNative=true`、`-s FULL_ES3=1`、`-s MIN/MAX_WEBGL_VERSION=2`（GLES3 的 VAO / UBO / 3D 纹理 / blit 入口点只存在于 WebGL2 上下文）。

**这三个链接开关是自动的。** `Aura3D.Avalonia` 的 browser 目标以精确区间依赖 `Aura3D.Avalonia.Browser`，包里的 `buildTransitive` props 只对 `*-browser` 目标注入上述开关，应用不需要手写 emscripten 参数。与 iOS 的切片包同构，也同样是精确锁 `[0.1.0]`：开关与库的 GLES 调用面配对，升级要和库一起过一遍浏览器验证。注意，这不包含下面的 .NET 10 发布期运行时配置；那三项仍必须由应用工程显式声明。

但 `WasmBuildNative=true` 只是**必要条件**：本机 SDK 没有 wasm-tools/emsdk workload 时链接同样不会发生，而构建是成功的。SDK 自己那条 warning 的文案是固定的 "neither $(WasmBuildNative), nor $(RunAOTCompilation) are 'true'"，此时 `WasmBuildNative` 明明是 true，会把人支到错的方向（实测：`dotnet.native.wasm` 3.0 MB vs 链上后的 25.6 MB）。所以包内的 `buildTransitive` targets 会在 `RuntimeIdentifier=browser-wasm` 且 `WasmNativeWorkloadAvailable!=true` 时直接报 Error 并给出 `dotnet workload install wasm-tools`；不需要本后端的工程可以设 `Aura3DSkipWasmWorkloadCheck=true` 关掉。与 iOS 侧"缺切片就 Error、不静默不出图"同构。

构建产物里可以自查开关是否生效：`dotnet msbuild <App>.Browser.csproj -getProperty:EmccExtraLDFlags -getProperty:WasmBuildNative -getProperty:WasmNativeWorkloadAvailable`（最后一项是 workload 在不在的直接判据）；再确认链接出的 `dotnet.native.wasm` 里有 `glGenVertexArrays` 等 GLES3 符号，就说明 shim 真的链进来了。

<a id="browser-net10-release-config"></a>

### .NET 10 Release / 静态发布的必填配置

当前 .NET 10 WebAssembly 工具链下，使用 Aura3D 的 `net10.0-browser` 应用必须在应用 `.csproj`
中显式加入以下配置。三项是一个整体，不能只选其中一部分：

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishTrimmed>false</PublishTrimmed>
  <RunAOTCompilation>false</RunAOTCompilation>
  <WasmLinkIcalls>false</WasmLinkIcalls>
</PropertyGroup>
```

- 默认 Release full trimming 会移除用于生成部分 WASM interpreter-to-native trampoline 的声明。
  Silk.NET 首次调用 `gl.ClearColor(float, float, float, float)` 时缺少 `VFFFF` 签名，表现为已经打印
  `[aura3d-webgl] output ...`，随后出现 `aot-runtime-wasm.c:188 <disabled>` 和
  `Program terminated with exit(1)`。文件名里的 `aot-runtime-wasm.c` 不代表应用已经启用了 AOT。
- 只关闭 trimming 会恢复 trampoline，但 .NET 10 的 linked-icall 表仍可能与实际加载的
  `System.Private.CoreLib` metadata token 不一致，启动阶段报
  `Your mono runtime and class libraries are out of sync` / `function signature mismatch`。
  因此必须同时设置 `WasmLinkIcalls=false`。
- 单独开启 AOT、设置 `TrimMode=partial` 或只添加
  `TrimmerRootAssembly Include="Silk.NET.OpenGLES"` 都不能修复这两个故障，不能作为替代方案。

切换这些属性后必须使用全新的 `bin`、`obj` 和发布目录。推荐让本次发布使用独立 artifacts 根目录：

```powershell
dotnet publish .\gallery\Aura3D.Gallery.Browser\Aura3D.Gallery.Browser.csproj `
  -c Release `
  --no-incremental `
  -p:UseArtifactsOutput=true `
  -p:ArtifactsPath="$PWD\artifacts\browser-release-clean"
```

产物在 `artifacts/browser-release-clean/publish/Aura3D.Gallery.Browser/release/wwwroot`。部署时必须整体替换
远端静态站目录，不要把新文件覆盖追加到旧 `_framework`；同时刷新浏览器、Service Worker 和 CDN
中的 `dotnet.js` 缓存。否则旧的 native wasm/CoreLib 与新清单混用，也会得到相同的 CoreLib/icall 错误。

`WEBGL_debug_renderer_info not enabled` / `INVALID_ENUM` 只是 emscripten 查询匿名 renderer 时产生的
无害警告，与上述崩溃无关。

## GLES 3.0 子集：写自定义 Pass 要知道的限制

框架的着色器与 GL 调用统一按 OpenGL ES 3.0 子集编写，以便同一条管线在桌面 GL、Android GLES 与 iOS 的 ANGLE 上都成立。其中几条在 ANGLE 上会真实咬人：

- ANGLE 的 Metal 后端只暴露 ES 3.0，申请 ES 3.1/3.2 上下文会以 `EGL_BAD_MATCH` 失败，且不提供 `GL_EXT_float_blend`。**混合开启时绘制到 32 位浮点颜色附件，整次 draw 会被当作 `GL_INVALID_OPERATION` 静默丢弃**——表现是黑屏而不是报错。框架的 HDR 渲染目标因此统一用 `Rgba16f`；自定义 Pass 不要申请 `Rgba32f` 颜色附件后又开混合。
- 深度附件 `DEPTH_COMPONENT16/24/32F` 与 `DEPTH24_STENCIL8`/`DEPTH32F_STENCIL8` 都是 ES 3.0 core，在 ANGLE 下可正常使用；上面那条只针对颜色附件。
- 没有 compute；`TexImage3D` 一类的 3D 纹理不能作为外部纹理导入。
- 着色器由 ANGLE 在运行时把 GLSL 翻成 MSL 并编译，没有磁盘二进制缓存。iOS 首帧开销实测（iPhone 17 模拟器 / iOS 27.0）：PBR 加级联阴影的场景约 5.6–5.9 s，简单场景 0.4–0.7 s。

浏览器上的 WebGL2 校验比桌面 GL 与 ANGLE 严格，下面几条在别的平台上是"能跑"，在 WebGL2 上会被判 `GL_INVALID_OPERATION` 并**静默丢弃整条 draw**：

- **启用的 draw buffer 必须对应一个被写入的片元输出。** 两种踩法：(1) 只挂深度附件的 FBO，其 `GL_DRAW_BUFFER0` 默认值仍是 `COLOR_ATTACHMENT0`，而阴影片元着色器没有颜色输出——depth-only 的 FBO 必须显式 `glDrawBuffers([GL_NONE])`（`RenderTarget`、`CubeRenderTarget` 与 CSM 路径已这么做）；(2) 片元着色器声明了 `out` 却在所有 `#ifdef` 分支里都不写它，编译后程序的片元输出数为 0。
- **变体宏要在绑定程序之前定好。** `UseShader(defines…)` 只是记录宏，真正编译并 `glUseProgram` 的是 `UseShader_Internal()`。两者顺序写反，本帧绑定的就是上一帧（首帧是空宏）的变体——引擎里曾靠"错一帧但稳定"掩盖了这个问题，代价是首帧画空 + WebGL2 报错。新 Pass 一律按 `UseShader` → `UseShader_Internal` → 设 uniform → draw 写。
- **RGB 族内部格式不可作为颜色附件。** WebGL2 不把 `RGB8` / `RGB16F` / `RGB32F` 列为 color-renderable，挂上就是 `FRAMEBUFFER_INCOMPLETE_ATTACHMENT`。渲染目标一律用 RGBA 族（辐照度图与预滤波环境图已从 `Rgb16f` 改为 `Rgba16f`）。
- **没有 `glGetTexLevelParameteriv`。** 把 GL 纹理导入 Skia 时必须在 `GRGlTextureInfo` 里显式给出内部格式，否则 `SKImage.FromTexture` 返回 `null`（Skia 在桌面 GL 上会反查格式，WebGL2 没有这个入口点）。另外 `WEBGL_debug_renderer_info` 未开启时查询 unmasked renderer 会打一条 `INVALID_ENUM` 警告，来自 emscripten 自身，可忽略。

## 浏览器的性能与已知缺口

- Debug 和当前已验证的 Release 发布配置都走 Mono 解释器，重场景的**首帧**代价可能以分钟计（级联阴影加 HDR 转立方体贴图的场景，在无头 Chromium 里建场景后超过 10 分钟才出第一帧）。这不是渲染路径的问题，是宿主 CPU 侧执行速度。当前不要为了性能单独打开 `RunAOTCompilation`：AOT 不能补回被漏掉的 trampoline，仍会在首帧崩溃。发布性能优化需等上游修复或由 Browser 包显式保留 Silk.NET 所需的全部调用签名。
- 自动化环境里页面处于 `hidden` 状态时，`requestAnimationFrame` 与 `ResizeObserver` 完全不触发，Avalonia 的渲染循环与画布尺寸都不会动——本地用无头浏览器验证时需要临时注入 rAF/ResizeObserver 垫片，这类垫片**不能**进 `wwwroot`。
- 模型导入依赖 Assimp 原生库，浏览器 wasm 上的链接方式尚未验证；其他平台的模型导入行为不受影响。

## 帧调度与线程语义

- `RequestNextFrameRendering()` 在两个后端签名一致。`AutoRequestNextFrameRendering = false` 时由应用自己逐帧请求。
- iOS 的帧回调运行在合成器渲染线程，与桌面端（回调即 UI 线程）不同：`SceneInitialized` / `SceneUpdated` / `ContextLost` / `ContextRestored` 会切回 UI 线程触发，事件处理器可以安全读写控件。浏览器分支同构，且上下文线程亲和于渲染线程——控件分离时不能直接发 GL 调用，释放要投递成 `Compositor.RequestCompositionUpdate` 任务在渲染线程执行。
- 控件从视觉树分离即销毁上下文，并真正删除 GL 对象归还显存；场景、节点与 CPU 侧资源全部保留，重新挂载时走 `ContextRestored` 而不是再次 `SceneInitialized`。语义与桌面端 `OnOpenGlDeinit` 一致，细节见 [GPU 资源生命周期](./gpu-resource-lifecycle.md)。

## GL(EAGL) 回退路径的已知问题

只有宿主显式设置 `iOSRenderingMode.OpenGl` 时才会走到这条路径（Avalonia `OpenGlControlBase` + Apple EAGL）。iOS 默认配置不需要关心本节。目前掌握的情况来自模拟器实测、未在真机验证：

- 走 PBR Deferred 的场景帧时是分钟级——模拟器的 EAGL 是转译/软件路径，扛不住多个高分辨率浮点渲染目标加级联阴影。真机 EAGL 由硬件驱动，表现可能不同。
- 级联阴影下的地面网格自第 3 帧起不再产生片元，而同帧一个只吃 `gl_VertexID` 的最小 program 能稳定落地，疑似模拟器 GLES-on-Metal 层静默丢弃该 draw。

结论：iOS 上保持默认（ANGLE）路径即可，没有理由再强制 OpenGL 模式。
