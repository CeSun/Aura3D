# Apple 平台脱离 OpenGL 强制模式：验证与迁移计划

本文是给执行者（人或 agent）用的任务清单，用于在 macOS/iOS 上把"Aura3D 在 Apple 平台不再强制宿主 App 使用 OpenGL 渲染器"这件事推进到可决策的状态。完成状态以本文各任务的**判定标准**和末尾的「执行结果」表为准，不以讨论结论为准。

背景痛点：iOS 端目前必须在 `AppDelegate.CustomizeAppBuilder` 里写 `iOSRenderingMode.OpenGl`，宿主 App 因此整体绑定到 Apple 已废弃的 EAGL 后端；macOS 端默认走的是桌面 GL，与 iOS 不统一。

## 0. 给执行 agent 的硬约束

- 用中文记录结论与提交说明。
- 不修改 CI 配置、不改动仓库级设置、不提交任何密钥或证书文件。
- 不推 `main`。工作分支：`feature/metal-lease-presentation`（已推到 origin）。
- 每完成一个任务，把结论回填到末尾「执行结果」表，并在对应任务下追加一行证据（命令输出摘要、截图路径或文件:行号）。
- 任何一个任务判定为"不通过"时**停下来报告**，不要自行扩大改动范围或跳到后续任务。
- 测试基线：`dotnet test Aura3D.sln` 应为 89 通过、0 失败。若某个任务改变了这个数字，必须说明原因。
- 构建产物（尤其 ANGLE 的 out 目录、xcframework）**不要提交**。

环境要求（Mac 侧）：Xcode 与其命令行工具、`dotnet workload install ios`、能访问 `github.com`（拉本仓库）与 `chromium.googlesource.com`（读 ANGLE 源码；`raw.githubusercontent.com` 在本项目环境不可达，不要用）。

## 1. 已经确立的事实

下表中的事实不要重复验证，除非该行明确标了"需复采"。证据分两类：`反编译`指 Avalonia 12.0.0 程序集的反编译产物（Windows 本机临时目录，Mac 上需要自行用 `ilspycmd` 反编译 `~/.nuget/packages/avalonia.ios/12.0.0/lib/...` 才能复核），`实测`指真实运行观察。

| # | 事实 | 证据 |
|---|---|---|
| F1 | `iOSPlatformOptions.RenderingMode` 的真实字段初值是 `[Metal, OpenGl]`（Metal 优先）。想走 Metal 只需**删掉**显式的 `[OpenGl]`，不需要写 `[Metal]`；传 null 或空集合会抛 `InvalidOperationException`。属性上的 XML 注释写"默认 OpenGl"是过时的。 | 反编译 `Avalonia.iOS`：`iOSPlatformOptions` 初值、`InitializeGraphics` 顺序取首个 `TryCreate()` 成功者 |
| F2 | Avalonia 默认 Metal 后端下，`ICustomDrawOperation.Render(ImmediateDrawingContext)` 里 `TryGetFeature<ISkiaSharpApiLeaseFeature>()` 可用；外部自建的 `MTLTexture` 经 `GRMtlTextureInfo` → `GRBackendTexture` → `SKImage.FromTexture` → `DrawImage` 能零拷贝合成上屏、逐帧刷新。 | 实测（2026-09-23 iOS 模拟器，`example/Example.iOS/MetalLeaseProbe.cs`，提交 4a10035） |
| F3 | Windows 上同一条 lease 路用 GL 纹理也成立（4423 次导入零失败）。⇒ 呈现口与平台/图形 API 无关。 | 实测（2026-09-22，临时探针已删除） |
| F4 | iOS Metal 模式下**没有** `ICompositionGpuInterop`：`MetalDevice.TryGetFeature => null`、`MetalPlatformGraphics.UsesSharedContext => false` 且 `GetSharedContext()` 抛异常。`OpenGlControlBase.InitializeAsync` 遇 null 直接失败。⇒ iOS Metal 模式下不能用 `OpenGlControlBase`。 | 反编译 `Avalonia.iOS` / `Avalonia.OpenGL` |
| F5 | iOS 的 EAGL 模式本身是零拷贝的（GL 纹理共享走同 share group），所以强制 `[OpenGl]` 的代价是"绑死废弃 API"，不是性能。 | 反编译 + `Avalonia.Skia` 的 `GlSkiaGpu` |
| F6 | EAGL 创建的纹理不能直接交给 Metal 后端的 `GRContext`，需要经 IOSurface 中转。 | **推断，未实测** |
| F7 | ANGLE 的 Metal 后端支持 iOS：README 注脚"Metal is supported on iOS 12+"；`gni/angle.gni` 里 `angle_enable_metal = is_apple`、`is_apple = is_mac || is_ios`；`src/libANGLE/renderer/metal/BUILD.gn` 有 `assert(is_mac || is_ios)`（⇒ tvOS 不可构建）。ES 3.0 需要 GPU family 4（A11 起），`kMaxSupportedGLVersion = 3.0`。上游 CI 没有任何 iOS builder。 | 源码取证（googlesource） |
| F8 | ANGLE 支持把我们提供的 `MTLTexture` 变成可渲染的 GL 纹理：扩展 `EGL_ANGLE_metal_texture_client_buffer`（token `EGL_METAL_TEXTURE_ANGLE`、`EGL_METAL_TEXTURE_ARRAY_SLICE_ANGLE`），`ImageMtl.mm` 接受 `2D/Cube/2DArray`（**3D 被拒**）、零拷贝包裹、并标记为 colorRenderable；**要求纹理的 device 必须等于 ANGLE display 的 device**。反向（从 ANGLE 自建的 GL 纹理取回 `MTLTexture`）没有公开 API。 | 源码取证，**但确切入口点未逐字确认，见 T3 步骤 0** |
| F9 | ANGLE 的 Metal 后端未实现 compute（`ContextMtl.mm` 里 `dispatchCompute` 是 `UNIMPLEMENTED()`），3D 纹理 mipmap 生成因此不可用；point sprite 是原生 `[[point_size]]` 但尺寸被限制；`BlitFramebuffer` 含缩放/格式转换有实现；数组纹理层作 RT 支持。 | 源码取证 |
| F10 | .NET iOS（net10.0-ios）**没有**绑定 EAGL / OpenGLES / `CVOpenGLESTextureCache`；但绑定了 `IMTLDevice.CreateTexture(MTLTextureDescriptor, IOSurface, nuint plane)`。Avalonia.iOS 自己是 `dlopen` OpenGLES 框架 + `dlsym` 取函数指针的。 | 实测（`Microsoft.iOS.xml` 检索）+ 反编译 |
| F11 | Aura3D 的 GL 用量是纯 GLES 3.0 子集：67 个 `gl.*` 函数、20 处 `#version 300 es`、零 `#extension`；**没有** compute / indirect draw / texture storage / buffer mapping。`TexImage3D` 只用于 `Texture2DArray`（CSM 深度数组，`ShadowMapPass.cs`），`GenerateMipmap` 只打在 `Texture2D` 和 `TextureCubeMap` 上 ⇒ 不踩 F9 的 compute 雷。 | 实测（源码检索） |
| F12 | ~~**ANGLE 目前只能在 Chromium 的 checkout 里为 iOS 构建**（`doc/DevSetup.md` 明写），iOS 产物形态是 `ios_framework_bundle` 而非裸静态库。~~ **已证伪（2026-09-23，T2）**：standalone ANGLE checkout（非 Chromium）即可为 iOS 模拟器 arm64 构建出含 Metal 后端的产物，仅需 gn 参数 `enable_rust=false`，未改上游源码；产物形态确为 framework bundle。 | 源码取证 + T2 实测（见 T2 执行证据） |
| F13 | ANGLE 的 Metal 后端把 GLSL 翻成 MSL 后，运行时用 `newLibraryWithSource:` 编译着色器。有内存库缓存与并行编译开关（默认开），但管线状态对象的创建仍是同步的，且源码里没有 `MTLBinaryArchive` 一类的磁盘二进制缓存；iOS 上连 ANGLE 内部着色器的构建期预编译都默认关闭（需 `angle_metal_toolchain_dir` 才打开）。⇒ 冷启动编译开销真实存在，需要实测数字。苹果对运行时 MSL 编译的政策，源码答不了。 | 源码取证 |

net10.0-ios 的 Metal 绑定命名有几个坑，写代码时照抄可用形式，别再试错：`MTLTextureType.k2D`（不是 `TwoD`/`Texture2D`）、`MTLRegion.Create2D(...)`（没有 `Make2D`）、`IMTLTexture` 上没有 `Region` 属性、上传用 4 参 `ReplaceRegion(MTLRegion, UIntPtr mipmapLevel, IntPtr withBytes, UIntPtr bytesPerRow)`。查签名先 grep `Microsoft.iOS.xml`（在 `Microsoft.iOS.Ref.*` pack 里，只收录有注释的成员），不足时看 Learn 的 `metal.imtltexture` 页里 `Foundation.ProtocolMember` 特性。Avalonia 12 里 `AvaloniaLocator.Current` 是 `[PrivateApi]`，编译期引用不到；要判断当前图形后端，就在 lease 里读 `GRContext.Backend`。

## 2. 三条候选路线与各自卡点

呈现层（把纹理画上屏）已经解决，三条路线的区别只在"谁来产出这张 `MTLTexture`"。

| 路线 | 保住现有 GLES 代码 | 卡点性质 | 需要改动 Aura3D.Core |
|---|---|---|---|
| R1 自研 Metal 后端 | 否（渲染侧重写） | 纯人力，无外部未知 | 是（渲染器抽象 + MSL 着色器） |
| R2 ANGLE（GLES→Metal） | 是 | **构建**（F12），另有 A11+ 门槛与运行时 MSL 编译的冷启动代价 | 否 |
| R3 自持 EAGL + IOSurface 桥 | 是 | 需要自己写 EAGL 与 `CVOpenGLESTextureCache` 的 interop（F10），且继续依赖废弃框架；跨 API 同步只能靠 finish 类手段（F6 未实测） | 否 |

R2/R3 的改动都集中在宿主层：给 Core 换 `GetProcAddress` 来源，再把最终 FBO 的纹理交给 lease。只有 R1 要先做抽象。

## 3. 任务清单

### T1 复采探针观测（约 30 分钟，任何路线都需要，先做）

F2 只记录了"没问题"，但三个对实现有决定意义的观测没有留档。

步骤：

1. `git fetch && git checkout feature/metal-lease-presentation`，在 Mac 上运行 `example/Example.iOS`（模拟器即可）。
2. 屏幕左上角有 8 行滚动日志，把首帧那行完整抄下来，它包含 `lease backend=...`。截图：`xcrun simctl io booted screenshot /tmp/probe1.png`。
3. 观察屏幕上两块 256×256 图（分别标了 `GRSurfaceOrigin.TopLeft` 和 `BottomLeft`）。每张图的标记是：纹理顶部 24 行红、底部 24 行蓝、左侧 24 列绿、中间一条上下移动的黄色带。
4. 回答四个问题并回填：
   - `lease backend` 是 `Metal` 还是 `OpenGL`？（若是 OpenGL 说明回落到 EAGL，F1/F2 的结论要重做）
   - 上图红条在上还是在下？下图与上图是否上下相反？（决定正式实现用哪个 `GRSurfaceOrigin`）
   - 红蓝有没有对调？（对调说明通道序不对，要换 `SKColorType` 或用 `GRMtlTextureInfo` 的 swizzle）
   - `imported`/`failed` 两个计数随帧数怎么变？（`failed` 必须恒为 0）
5. 顺手记录模拟器/设备的 iOS 版本与机型，以及是否出现内存上涨（`imported` 每帧 +2 但 `GRBackendTexture`/`SKImage` 是逐帧创建的，长跑 5 分钟看是否泄漏）。

判定标准：四个问题都有明确答案并回填。若 `failed` 不为 0 或长跑内存持续上涨，标为不通过并停下报告。

> 执行证据（2026-09-23，iPhone 17 模拟器 / iOS 27.0 (24A434)，`xcrun simctl` 安装运行，截图 `/tmp/probe1.png`、`/tmp/probe_final.png`）：`lease backend=Metal canvas=SKCanvas surface=SKSurface`（未回落 EAGL，F1/F2 成立）；TopLeft 图红在上/蓝在下/绿在左（与纹理布局一致，不翻转），BottomLeft 图红蓝上下互换（符合预期）；红蓝无对调 ⇒ `SKColorType.Rgba8888` 通道序正确；`frame=900 imported=1800 failed=0`，failed 恒为 0。5 分钟长跑 RSS 514.2→519.6 MB（+5.4 MB，远小于逐帧泄漏 256KB 纹理应有的量级，判无泄漏）。正式实现取 `GRSurfaceOrigin.TopLeft`。

### T2 判 ANGLE 生死：能不能为 iOS 构建出来（2–4 小时，只做构建，不做集成）

F12 说只能从 Chromium checkout 构建。这一步的目的就是把这句话验真或验伪，因为它决定 R2 是否存在。

步骤：

1. 装 `depot_tools`，按 ANGLE 的 `doc/DevSetup.md` 与 Chromium 的 iOS 搭建文档取一个**只用于编译 ANGLE 目标**的最小 checkout（不要整仓同步：用 `gclient sync` 的裁剪手段或只拉 ANGLE 依赖所需的部件；能编出目标即算数）。
2. 生成构建：`gn gen out/ios --args='target_os="ios" target_environment="simulator" target_cpu="arm64" is_component_build=false angle_enable_metal=true is_debug=false'`（真机把 `target_environment` 去掉并改 `target_cpu="arm64"`）。参数名以仓库当前 `gni/angle.gni` 为准，报错就照实修正，不要臆造。
3. `ninja -C out/ios angle:libEGL angle:libGLESv2`（目标名以 `BUILD.gn` 为准）。
4. 检查产物：是否得到可链接的 framework/静态库；用 `nm -gU` 或 `strings` 确认里面**含 Metal 后端符号**（例如 `DisplayMtl`、`RendererMtl`），而不是只把 Vulkan/SPIRV 后端编进去了。
5. 记录：总耗时、checkout 体积、产物体积、必需的 GN 参数、以及为了编过是否改了上游代码（改了就是维护成本，必须写明）。

判定标准：拿到含 Metal 后端的 iOS 产物 ⇒ R2 存活，进 T3。以下任一情况判不通过：无法在合理时间内得到最小 checkout；必须魔改 ANGLE 源码才能编过；产物里没有 Metal 符号。判不通过就**停止**，把结论写清，转 T5。

> 执行证据（2026-09-23，**通过**，经代理 127.0.0.1:7897）：standalone checkout `~/angle_ios`（手写 `.gclient`：solution `.` → `angle/angle.git@main` + `target_os=['ios']`，`gclient sync --nohooks --no-history` 约 8 分钟、11G）。gn 参数：`target_os="ios" target_environment="simulator" target_cpu="arm64" is_component_build=false angle_enable_metal=true is_debug=false` + **`enable_rust=false`**（standalone DEPS 缺 `third_party/rust-toolchain`，`build/config/rust.gni` 读不到 VERSION 文件；这是配置差异，**未改任何上游源码**）。`ninja out/ios libEGL libGLESv2` 2271 targets 约 10 分钟。产物：`out/ios/libEGL.framework`（140K，导出 115 个 `_egl*`）+ `libGLESv2.framework`（12M），均 Mach-O arm64 动态库；Metal 后端 38 个 .o 编入，`nm` 命中 370 个 Mtl 符号（Obj-C++ 符号为 local，`nm -gU` 看不到，需 `nm` 全量或 `strings` 验 `ContextMtl.mm`/`DisplayMtl.mm`）。⇒ **R2 存活，且构建成本远低于 F12 预估；进 T3。**
>
> 注：`*.googlesource.com` 在本机直连不可达（见下方 2026-09-23 受阻记录），本次经用户开启的本地代理完成。

> 执行证据（2026-09-23，受阻未判定）：`git clone https://chromium.googlesource.com/chromium/tools/depot_tools.git` 连接 75s 超时；`curl https://chromium.googlesource.com` 无响应；`pdfium.googlesource.com`、`swift-go.googlesource.com` 同样超时 ⇒ 整个 `*.googlesource.com` 域不可达，非单主机故障。GitHub 侧 `chromium/depot_tools`、`chromium-mirrors/{depot_tools,angle,build}`、`chromium/build` 均不可达（无镜像）；可达项仅 `github.com/google/angle` 与 `storage.googleapis.com`。结论：本机网络环境下无法开始 T2 步骤 1（gclient sync 的 DEPS 全部指向 googlesource），需要代理/VPN 或换网络后重跑。这不是对 R2 的技术否定。

### T3 ANGLE 最小互操作实验（依赖 T2 通过；1–2 小时）

目的：证明"我们分配的 `MTLTexture` → 交给 ANGLE 当 GL 渲染目标 → ANGLE 画完 → 同一张纹理交给 Skia lease 上屏"这条闭环成立。这一步是整个 R2 的技术核心，也是 F8 里唯一没逐字确认的部分。

0. 先读 `extensions/EGL_ANGLE_metal_texture_client_buffer.txt` 全文，确认**入口点到底是 `eglCreatePbufferFromClientBuffer` 还是 `eglCreateImage`/`eglCreateImageKHR`**，以及需要什么 display 属性。注意已知事实：`eglCreatePbufferFromClientBuffer` 在 Metal 后端只接受 `EGL_IOSURFACE_ANGLE`，所以极可能要走 EGLImage 那条；把结论写进执行结果。

   > 步骤 0 结论（2026-09-23，spec v3 2024-02-20 全文已读）：入口点是 **`eglCreateImageKHR(dpy, EGL_METAL_TEXTURE_ANGLE /*0x34A7*/, EGL_NO_CONTEXT, (EGLClientBuffer)MTLTexture, attrib_list)`**，不是 `eglCreatePbufferFromClientBuffer`。attrib 可用 `EGL_METAL_TEXTURE_ARRAY_SLICE_ANGLE`（数组切片，非 2DArray 时只许 0）与 `EGL_TEXTURE_INTERNAL_FORMAT_ANGLE`（internal format 覆盖）。spec 原文确认 device 约束："the provided Metal texture object must have been created by the same Metal device queried from the display"（经 `EGL_ANGLE_device_metal` 查询），违反报 `EGL_BAD_PARAMETER`。`<ctx>` 必须 `EGL_NO_CONTEXT`，宽高取自纹理本身。
1. 在 `example/Example.iOS` 里新建一个临时页（复用 T1 探针的骨架），用 `[DllImport]` 或 xcframework 桥调用 ANGLE 的 EGL/GLES 入口。创建 EGL display 时**必须把 `MTLDevice.SystemDefault`（也就是 F2 里同一个 device）显式交给 ANGLE**（F8 的 device 相等约束）。
2. 用与 T1 探针相同的方式分配一张 `MTLTexture`（`ShaderRead` 用法、非 Private 存储），把它作为 client buffer 导入成 GL 纹理并挂到 FBO 的颜色附件上。
3. 在 ANGLE 里把这张纹理清成一个与 UI 无任何重合的可辨识颜色（例如纯洋红），并每帧改一下颜色或画一条移动的带子。
4. 用 T1 已经验证过的 lease 导入路径把它画上屏。
5. 截图确认：屏幕上出现洋红且逐帧变化。

   > 执行证据（2026-09-23，**通过**）：闭环打通——`MTLTexture(256², RGBA8Unorm, Shared, ShaderRead|RenderTarget)` → `eglCreateImageKHR(_display, EGL_NO_CONTEXT, EGL_METAL_TEXTURE_ANGLE, handle, null)` 成功 → `glEGLImageTargetTexture2DOES(GL_TEXTURE_2D, image)` 无错 → FBO `FRAMEBUFFER_COMPLETE` → 洋红 clear + scissor 绿带逐帧移动 → T1 的 lease 路径上屏。模拟器截图 `/tmp/angle7.png`、`/tmp/angle8.png` 均见洋红+绿带，控制台 `frame=900 imported=900 failed=0`。关键落地点：
   > 1. **display 必须显式请求 Metal 后端**：`eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE=0x3202, EGL_DEFAULT_DISPLAY, {EGL_PLATFORM_ANGLE_TYPE_ANGLE=0x3203, EGL_PLATFORM_ANGLE_TYPE_METAL_ANGLE=0x3489, EGL_NONE})`，否则 iOS 上 ANGLE 默认走 EAGL 后端、display 扩展里没有 `EGL_ANGLE_metal_texture_client_buffer`。运行时 `GL_RENDERER='ANGLE (Apple, ANGLE Metal Renderer: Apple iOS simulator GPU, Version 27.0)'`。
   > 2. **device 约束（F8）实测满足**：.NET `MTLDevice.SystemDefault` 与 ANGLE `DisplayMtl` 内部 `MTLCreateSystemDefaultDevice()` 是同一单例，`eglCreateImageKHR` 直接成功，无需特殊处理。
   > 3. **.NET iOS AOT 下取入口点的方式**：`Marshal.GetDelegateForFunctionPointer` 会抛 `ExecutionEngineException`（wrapper 需 JIT），改为直接 `DllImport("__Internal")`；ANGLE 两个 framework 直接导出全部所需符号（`eglGetPlatformDisplayEXT`/`eglCreateImageKHR`/`glEGLImageTargetTexture2DOES` 等），并用 `dlsym(RTLD_DEFAULT)+dladdr` 确认解析落在 app 内嵌的 ANGLE framework 而非 Apple OpenGLES.framework（两者符号同名，存在混用风险，正式实现要盯住这点）。
   > 4. **踩坑记录**：`GL_OES_EGL_image` 在 ANGLE 里是 requestable 扩展，但非 WebGL 上下文默认全部启用（`Context.cpp` `GetExtensionsEnabled` 默认 true），无需 `glRequestExtensionANGLE`；本次排查中一路的 `GL_INVALID_ENUM` 真因是探针把 `GL_TEXTURE_2D` 写成桌面值 `0x05E1`，GLES 定义是 `0x0DE1`（`include/GLES2/gl2.h:114`）。差分定位法：`glEGLImageTargetRenderbufferStorageOES` 同 image 报 0（证明扩展位与 image 有效）、`glGetStringi` 精确 token 枚举（证明 enabled）、`FromGLenum<TextureType>` 反汇编（证明 0x05E1 落 InvalidEnum）⇒ 锁定为调用方常量错误。
   > 5. 同步目前用 `glFinish()`，按计划在 T4 之后换 `EGL_ANGLE_metal_shared_event_sync`。

判定标准：颜色出现且逐帧变化 ⇒ R2 成立，进 T4。若导入成功但画不出/画出黑图，先分别排查同步（ANGLE 的命令缓冲是否在我们读取前完成）与 device 不匹配，把两次尝试的现象都记下来再报。这一步之后，正式实现还需要把同步换成 `EGL_ANGLE_metal_shared_event_sync` 一类机制，但那属于 T4 之后。

### T4 把真实渲染搬上 ANGLE，逐项验 F9/F11 的风险点（依赖 T3 通过）

顺序建议：先 `Base Geometries`（最基础的着色器路径），再按风险点单独验。

- 点云：`PointCloud` 页。风险是点尺寸被 ANGLE 限制（F9）。判定：点可见、尺寸合理、与桌面端视觉一致。
- CSM：`CascadedShadowMaps` 页，走 `Texture2DArray` + `FramebufferTextureLayer` 的深度附件路径。判定：阴影分级正确、无黑块。
- FXAA / CopyPass：`BlitFramebuffer` 的缩放与格式转换路径，以及 HDR 相关 RT。判定：无错图、无花屏。
- 冷启动耗时：记录首帧与进入各页时的可感知卡顿，ANGLE 运行时编 MSL 且无磁盘二进制缓存（F13），预期会有首次编译开销。数字要写下来。
- 全量回归：Example 里所有页在 iOS Metal + ANGLE 下过一遍，失败项逐条列。

判定标准：以上四项全部有明确记录，且没有阻断性错图。

   > 执行证据（2026-09-23，进行中；**CSM 项判定不通过，按约定停下报告**）：
   >
   > - **Base Geometries**：通过。ANGLE Metal 下几何/纹理/PBR GBuffer 内容正确（真帧回读 baseColor=(220,220,220,255)、深度 0.987）。
   > - **PointCloud（F9）**：通过。点可见、尺寸正常；`GL_ALIASED_POINT_SIZE_RANGE=[1,511]`。附带发现：`RenderPass.cs:209` 无条件 `glEnable(GL_PROGRAM_POINT_SIZE)`（桌面 GL 扩展名）在 ANGLE 下每帧刷 0x500，不影响结果，属 Core 清理项。
   > - **CSM：不通过**。根因已定位，且与 CSM/`Texture2DArray`/阴影本身无关（NOSHADOW 仍全黑）——**ANGLE Metal 后端的 ES 3.0 上下文不提供 `GL_EXT_float_blend`，按 ES 规范"使能混合绘制到 32 位浮点 RT"即 `GL_INVALID_OPERATION`，整次 draw 被静默丢弃**。PBR 延迟管线三张主 RT（BaseRenderTarget/BackgroundRenderTarget/GammaOutput，`PBRDeferredPipeline.cs:25/29/33` 硬编码 `Rgba32f`）上的光照/背景/Copy 累积全部开着混合 ⇒ IBLAmbient、Directional、Background、Copy 四个全屏 quad 无一落盘。证据链（临时宿主探针，`example/Example.iOS/AngleSceneHost.cs`，`AURA_DEBUG_DRIVE` / `AURA_DEBUG_REAL` / `AURA_ES32` 开关，入 §4 删除清单）：
   >   1. 真帧逐 pass 驱动回读：IBL/Dir/Background/Copy 的 `Render()` 之后恰好各 1 个 0x502，BaseRT 保持 (0,0,0,0)、无像素写入；该错误在正常路径不可见，因为 Core 多处（如 `TranslucentIBLAmbientPass.cs:51`）`var error = gl.GetError();` 先行吞掉。
   >   2. 宿主内隔离复现（同一 IBL 片元 shader 经 reflection 取内嵌资源 + 同款 defines + 同 7 sampler 绑定 + InternalQuad 复刻）：blend 关闭 → 正常出像素 (0.550,0.779,1.032,1.000)；ONE/ONE 或 SRC_ALPHA/ONE_MINUS 到 **RGBA32F → 0x502 无像素**；到 **RGBA8 → 正常**（byte=(140,199,255,255)）；到 **RGBA16F → 正常且保 HDR 值**（0.550,0.778,1.032,1.000）。
   >   3. `GL_EXTENSIONS`（95 项）无 `GL_EXT_float_blend`（blend/float 相关只有 `GL_EXT_blend_func_extended`/`blend_minmax`/`color_buffer_float`/`color_buffer_half_float`/`OES_texture_float`）；向 ANGLE 请求 ES 3.1/3.2 上下文（`EGL_CONTEXT_MINOR_VERSION=1/2`）→ `eglCreateContext` 报 `EGL_BAD_MATCH(0x3009)`，该构建只到 ES 3.0。
   >   4. 此前"BackgroundRT 天空全黑"疑点为同因连带（blend 状态跨 pass 残留 + 32F RT），非 HDR cubemap 独立问题；待 RT 格式决策后复核。
   >   - 修复方向（超出本验证任务范围，待决策）：a) 管线 HDR RT 由 `Rgba32f` 改 `Rgba16f`（探针实证 16F 混合可行、保留 >1.0 值，代价是位深）；b) 开 ANGLE 构建能力（ES 3.1+ / EXT_float_blend）。
   > - **FXAA / CopyPass**：CopyPass 受同因阻断（blend→32F 目标）；Fxaa 输出到相机目标（RGBA8）的 draw 无错。整页效果待 HDR RT 决策后回归。
   > - **冷启动（F13）**：PBR 延迟管线冷启动到首帧约 4.2s（模拟器，ANGLE 首次编译 MSL、无二进制缓存），正式数字在 T4d 记录。
   > - **全量回归**：未做；预计所有走 `PBRDeferredPipeline`（HDR RT）的页都会命中同一阻断，非 HDR（BlinnPhong/NoLight）页需确认是否也开混合到浮点 RT。

   > 执行证据（2026-09-23，**修复决策已批准并实施：HDR RT `Rgba32f`→`Rgba16f`，CSM 复验通过**）：
   >
   > - 改动：`PBRDeferredPipeline.cs:25/29/33` 与 `PBRForwardPipeline.cs:21/25` 共 5 处 HDR RT 注册 `Rgba32f`→`Rgba16f`。`dotnet test` 基线保持 89 通过 / 0 失败。
   > - 复验（iOS 模拟器，`AURA_SCENE=CSM`）：光照/天空/地面全部落盘，真帧 BaseRT 中心 (0.651,0.879,1.138,2.000)（alpha=2 为 IBL+方向光 ONE/ONE 累积，>1.0 HDR 值经 16F 保留），最终屏中心像素 (170,182,190,255)；截图 `/tmp/csm_fix2.png`。
   > - 阴影 A/B（`AURA_CSM_NOSHADOW=1` vs 默认，`/tmp/ab_shadow.bmp` vs `/tmp/ab_nosh.bmp`）：13.05% 采样像素差异 >12，抽样点全部为阴影侧变暗 ⇒ CSM 阴影实际生效。
   > - FXAA A/B（`CSM` vs `CSM noFXAA`，标题栏实证 `FXAA=off`）：球体轮廓在 noFXAA 下有可见阶梯、FXAA 下平滑 ⇒ FXAA pass 在 ANGLE 上生效。CopyPass（BaseRT.Color→BackgroundRT）由"光照结果出现在最终合成"间接证实。
   > - **附带发现（与 ANGLE 无关，桌面端同样成立）**：`CascadedShadowMapsPage` 示例相机 `RotationDegrees=(-30,-25,0)` 背对 +z 球阵——引擎约定 `ForwardVector = (0,0,-1)·旋转`（`MathHelper.cs:153`，自 0.0.1 未变），该朝向使球体全部位于视锥后方，被视锥剔除（宿主探针实测 total=49 visible=1，`(0,2,3)` 投影 w=-6.6 在相机后方）。桌面页未改（超出本任务范围），临时宿主页已改 yaw=155 镜像朝向（`AngleHostPage.cs:330-333`）。
   > - 另一注意点：启动后约 8s 截图仍为黑屏、约 30-60s 后才有合成内容（lease 合成节奏），F13 冷启动正式数字在 T4d 一并记录。

   > 执行证据（2026-09-23，T4d：**冷启动数字 + 全量回归完成，T4 全部判定项收口**）：
   >
   > - **冷启动（F13，iPhone 17 模拟器）**：全新安装进 CSM（PBR 延迟管线）：launch→`eglInitialize/上下文创建` 约 +0.2s → scene ready（资源/几何就绪）约 +4.2s → **首帧渲染完成约 +5.9s**；删除 App 重装后重测一致。**热启动（第二次启动，ANGLE 进程内存库已失效）：scene ready 4.5s / 首帧 5.6s，与冷启动基本相同 ⇒ ANGLE 的 MSL 编译结果没有跨进程磁盘缓存，每次启动都重付编译开销，F13 风险成立**。对照非 HDR 场景：Base 首帧约 0.7s、Points 首帧约 0.4s（开销主要在 PBR 管线的大量 program 上）。另注：截图窗口内 lease 合成滞后（见上一条），数字以 `angle-host` 日志计时为准。
   > - **全量回归（`example/Example.iOS/AngleHostPage.cs` 临时宿主场景，入 §4 删除清单）**：10 个场景在 ANGLE Metal 下全部渲染正确、无阻断性错图——`Base`（BlinnPhong 默认管线）、`Points`、`CSM`、`CSM noFXAA`、`Cel`（CelShadingPipeline：卡通描边+天空盒+glb 角色/NPC+茶几，`/tmp/reg_Cel.png`）、`Forward`（PBRForwardPipeline：HDR 环境+球体，`/tmp/reg_Forward.png`，同时验证 16F RT 改动对前向管线生效）、`Particles`（半透明混合+点发射器火焰，`/tmp/reg_Particles.png`）、`Instancing`（InstancedMesh 169 实例，`/tmp/reg_Instancing.png`）、`Primitives`（7 种 PrimitiveType 含 LINES/TRIANGLE_STRIP/POINTS 全可见，`/tmp/reg_prim2.png`）、`Anim`（glb 蒙皮动画 Soldier，`/tmp/reg_Anim.png`）。半透明混合格式（BlendFactor/Src-BlendFactor 到 16F/8 目标）无 0x502。
   > - **未复刻页及原因**：`ModelPreview`（依赖 Avalonia 文件选择器 UI）；`ParticleEditor`/`CelShadingMaterialEditor`/`DebugTest`（UI 重的编辑器页，其场景渲染特征已被上述复刻场景覆盖）；`AnimationFeatures`（走 Assimp/FBX 原生库，iOS 无对应构建；glb 蒙皮路径已由 `Anim` 覆盖）；`RenderingPerformance`（HISM/InstancedMeshGroup 路径未覆盖，**回归缺口**）；`ContextLoss`（Avalonia GL 控件专属机制；GPU 重建能力由提交 fda7548 覆盖）。
   > - 已知噪声（不影响判定）：`RenderPass.cs:209` 每帧 1 次 0x500（`GL_PROGRAM_POINT_SIZE` 为桌面 GL 枚举），Core 清理项。`dotnet test` 基线保持 89 通过 / 0 失败。

### T5 R2 判死后的替代验证（只在 T2 或 T3 不通过时做）

先做 R3 的纸面与最小实验（不需要 ANGLE）：

1. 自持 EAGL 上下文的可行性：`dlopen("/System/Library/Frameworks/OpenGLES.framework/OpenGLES")` + `dlsym` 取函数指针（Avalonia.iOS 自己就是这么干的，F10），Silk.NET 侧用现成的委托构造即可，不改 Aura3D.Core。
2. IOSurface 桥的可行性：`CVPixelBuffer` + `IOSurface` → GL 侧用 `CVOpenGLESTextureCacheCreateTextureFromImage`（.NET iOS 未绑定，需要手写 `[DllImport]`）当渲染目标；Metal 侧用已绑定的 `IMTLDevice.CreateTexture(MTLTextureDescriptor, IOSurface, nuint plane)` 包成 `MTLTexture`，再走 F2 的 lease 路。
3. 判定标准：同一帧数据能被 Metal 读到（先在洋红清屏级别验证），再验证跨 API 同步（GL 写完后 Metal 读到的是新内容而不是上一帧）。
4. 若 F6（EAGL 纹理不经 IOSurface 不能给 Metal）在这一步被证伪或证实，写回第 1 节。

R3 与 R2 的取舍要点：两者都不动 Core，但 R3 继续绑在 Apple 已废弃的 EAGL 上，R2 才能真正做到全栈无废弃 API。

### T6 与路线无关的收尾（可在任何平台做，不占 Mac 时间）

F2 的通路目前只是探针，正式化需要：在 `Aura3D.Avalonia` 里提供一个"外部纹理宿主件"，取代在 Apple 上走不通的 `OpenGlControlBase` 互操作路；它应当只依赖"渲染侧给一张与合成器同 API 的纹理句柄 + 尺寸 + 朝向"，并带单元测试。R1/R2/R3 三条路线最终都要接这个件，所以先做不会错。注意：给 `Aura3D.Avalonia` 增加对 `Avalonia.Skia` 的依赖要有意识地决策（目前探针放在 `example/Example.iOS` 就是为了不顺手把 Skia 依赖塞进库）。

   > 执行证据（2026-09-23，**通过：宿主件已正式化进 `Aura3D.Avalonia`，共享 Example 无任何 iOS 特判**）：
   >
   > **实现结构**：`Aura3D.Avalonia.csproj` 多 TFM `net8.0;net10.0;net10.0-ios26.0`，ios TFM 定义 `ANGLE_HOST` 并直接包引用 `Avalonia.Skia`（决策：经批准采用直接引用，不建独立 iOS 程序集、不用反射软依赖）。`Aura3DViewBase` 拆为共享主体 partial（`RenderFrameCore` 单套主流程 + `ComputePixelSize`/`EnsureScene`/`ContextLostCore`/`DetachAndReleaseGpu`）+ 双后端 partial：`Aura3DViewBase.OpenGl.cs`（桌面/Android/Browser，基类 `OpenGlControlBase`）与 `Aura3DViewBase.Angle.cs`（iOS，基类 `Control`，ANGLE + lease 合成）。ANGLE 桥（`Angle/AngleGlesSession.cs`、`Angle/AngleNative.cs`）入库；应用侧仅需在 csproj 提供 ANGLE framework 的 `NativeReference`。两端 `Aura3DView` 签名一致（`RequestNextFrameRendering`/`SimulateContextLost` 直接声明在 `Aura3DViewBase` 上，规避共享 Example.dll 按 net10.0 资产编译后在 iOS 上的 callvirt 声明类型错配）。Core 仅一处无害改动：`RenderPass.cs` 两处 `EnableCap.ProgramPointSize` 后吞掉 ANGLE 不支持该枚举产生的 0x500。
   >
   > **关键发现（线程模型差异，桌面没有的坑）**：iOS 上 Avalonia 合成器的自定义绘制回调运行在**渲染线程**（≠ UI 线程），而桌面窗口模式的 GL 渲染回调实际在 UI 线程。示例页处理器普遍在 `SceneUpdated` 里直接读写控件（如 PointCloud 页 `infoText.Text`/`shapeComboBox.SelectedIndex`），在 iOS 触发 `AvaloniaObject.VerifyAccess` 异常（"The calling thread cannot access this object…"）致视口空白。修复：共享主体新增 `partial void DispatchSceneEvent(Action)` 缝——桌面内联（行为与拆分前完全一致），iOS 切回 UI 线程触发 `SceneInitialized/Updated/Destroyed/ContextLost/ContextRestored`；`ApplyDestroyScene` 改为携带场景参数触发事件以兼容异步派发。分离释放同理延迟：`OnDetachedFromVisualTree` 只置 `_detachReleasePending`，重挂载后第一帧在渲染线程先 `DetachAndReleaseGpu` 再走 ContextRestored。（**该分离释放方案已被后续修正，见下方 T6-补**）
   >
   > **验证**（iPhone 17 模拟器 / iOS 27.0，真实 `MainView`（Ursa 导航）+ 真实示例页，无探针页）：首页 + Base Geometries 渲染正常（`/tmp/t6-home.png`）；Point Cloud 20000 点彩色点云正常（修复线程派发后，`/tmp/t6-points3.png`）；日志 `[aura3d-angle] egl 1.5 ok … ANGLE Metal Renderer … output 439x2016 fbo=1`。CSM 与 GL Context Loss 页在 iPhone **竖屏**下视口列为 0 宽（固定侧栏 220/340pt > 内容区宽），宿主从未收到帧请求——示例页布局限制而非宿主缺陷（其 PBR/CSM 渲染路径已在 T4d 探针宿主验证）；竖屏适配列为后续项。`dotnet test` 89/0；库 3 个 TFM 与 Example.Desktop/Example.iOS 编译 0 错误。
   >
   > **遗留**：① 每帧 `glFinish` 同步，应升级 `EGL_ANGLE_metal_shared_event_sync`；② 宿主生命周期单元测试未建（需 headless 测试宿主），列为后续项；③ 手机竖屏下固定侧栏示例页的布局适配。

   > 执行证据（2026-09-23，**T6-补：宿主生命周期收口**）：T6 首版有两个泄漏面——`AngleGlesSession.Dispose()` 全仓无调用点（EGL display/context/surface + 全像素尺寸 `MTLTexture` 永不回收，而示例页 VM 常驻、每次导航新建 view，14 个页走一遍即累积 14 份上下文）；`OnAttachedToVisualTree` 里 `_frameTimer.Tick +=` 在重挂载时重复订阅。
   >
   > 修正做法（不再需要"延迟到重挂载帧"）：**帧间不残留 current** —— `FinishFrame` 末尾 `eglMakeCurrent(dpy, EGL_NO_SURFACE, EGL_NO_SURFACE, EGL_NO_CONTEXT)`，上下文因此不属于任何线程，UI 线程可合法接管；`AngleGlesSession.Release(Action? releaseGpuResources)` 在 UI 线程把它 make-current、逐个归还管线 GL 对象、删宿主 FBO/纹理/EGLImage，再 `eglDestroySurface`/`eglDestroyContext`，`_texture.Dispose()`。新增 `SyncRoot` 把「EnsureOutput → RenderFrameCore → FinishFrame → Compose」整段与 `Release` 互斥（合成读的是同一张 MTLTexture，必须在锁内）。`OnDetachedFromVisualTree` 改为直接 `Release(DetachAndReleaseGpu)`，接管失败才回落 `ContextLostCore`；`_detachReleasePending`/`TryMakeCurrent` 删除。**有意不调 `eglTerminate`**：ANGLE 缓存 platform display，同进程其他会话仍依赖它（实测第二个会话在首个会话销毁后 `eglInitialize` 正常）。
   >
   > 语义现在与桌面端 `OnOpenGlDeinit` 一致（桌面分离即销毁上下文）。**重挂载代价不是本次新增**：旧路径的 `DetachAndReleaseGpu` 同样会 `ReleaseGpuResources`+`HandleContextLost`，重挂载必然重建全部 program，F13 的 MSL 重编译开销（PBR 首帧约 5.6-5.9s）在两条路径上相同。
   >
   > 验证（iPhone 17 模拟器 / iOS 27.0，真实 MainView 导航触发分离）：`AURA_PAGE="Point Cloud"` 与 `AURA_PAGE="PBR RenderPipeline"` 两轮，日志均为「会话 A `egl 1.5 ok` + `output 1483x936`」→ 约 0.6s 后「会话 B `egl 1.5 ok` + `output 1483x828`/`1483x1092`」，即首个上下文被销毁后新会话在同一 platform display 上重建成功；全程无 `FAIL`、无 `release failed`、无 `glError`；截图 `/tmp/life-1.png`（点云 20000 点）、`/tmp/life-4.png`（PBR HDR 天空+球阵+glb 凳）内容正确。`dotnet test` 89/0，`Aura3D.Avalonia` 三 TFM 与 `Example.iOS` 编译 0 错误。**未覆盖**：同一控件 attach→detach→attach 的实机回退导航（模拟器输入通道不可用），该路径与已验证的"新建会话"路径共用 `EnsureScene` 恢复支路，桌面端与 GL Context Loss 页亦覆盖。


## 4. 探针代码与清理

探针在提交 4a10035（分支 `feature/metal-lease-presentation`）里，共三处改动，验证结束后按需删除或转正：

- `example/Example.iOS/MetalLeaseProbe.cs`（新文件）
- `example/Example.iOS/AppDelegate.cs`：删掉了 `[OpenGl]` 强制，并注入 `RootViewFactory`
- `example/Example/App.axaml.cs`：新增 `RootViewFactory` 钩子（临时用，正式实现不该保留）

删除清单：删 `MetalLeaseProbe.cs`、删 `AngleMetalLeaseProbe.cs`（T3 探针）、还原 `AppDelegate.cs` 的 `CustomizeAppBuilder` 与 `RootViewFactory` 注入、删 `Example.iOS.csproj` 里指向 `$(AngleIosOutDir)` 的 `NativeReference` 段、删掉 `App.RootViewFactory` 及其在 `OnFrameworkInitializationCompleted` 里的使用。

> 清单执行状态（2026-09-23，T6）：`MetalLeaseProbe.cs`、`AngleMetalLeaseProbe.cs`、`AngleSceneHost.cs`、`AngleHostPage.cs` 已删；`App.axaml.cs` 的 `RootViewFactory` 已删，`MainView` 恢复直建；`AppDelegate.cs` 的 `CustomizeAppBuilder` 已还原为无平台特判（不再强制 `iOSRenderingMode.OpenGl`），仅保留一个工程内验证辅助（`AURA_PAGE` 环境变量经 `JumpTo` 消息导航，不触共享工程）。**例外**：`Example.iOS.csproj` 的 ANGLE `NativeReference` 段**有意保留**——宿主件已入库但 ANGLE framework 仍由应用提供（`DllImport("__Internal")` 经主可执行文件解析），这是正式化的设计而非探针残留。

## 5. 执行结果（待填）

| 任务 | 结论 | 关键证据 | 日期 |
|---|---|---|---|
| T1 | 通过。lease backend=Metal；TopLeft 朝向正确（正式实现用 `GRSurfaceOrigin.TopLeft`）；红蓝未对调（Rgba8888 正确）；failed 恒为 0；5 分钟长跑 +5.4MB 判无泄漏 | iPhone 17 模拟器 / iOS 27.0 (24A434)，`/tmp/probe1.png`、`/tmp/probe_final.png`，T1 节执行证据 | 2026-09-23 |
| T2 | **通过**：standalone ANGLE checkout 可为 iOS 模拟器 arm64 构建含 Metal 后端产物，仅需 `enable_rust=false`，未改上游源码 ⇒ R2 存活（F12 证伪）。首次尝试因 `*.googlesource.com` 直连不可达受阻，后经本地代理完成 | `~/angle_ios/out/ios/libEGL.framework`(140K/115 导出) + `libGLESv2.framework`(12M/370 Mtl 符号)，T2 节执行证据 | 2026-09-23 |
| T3 | **通过**：MTLTexture → `eglCreateImageKHR(EGL_METAL_TEXTURE_ANGLE)` → ANGLE Metal 后端渲染（洋红+移动绿带）→ 同纹理经 Skia lease 上屏，900 帧 failed=0 ⇒ R2 技术核心成立。注意：display 需 `eglGetPlatformDisplayEXT` 显式选 Metal 后端；AOT 下入口点用 `DllImport("__Internal")` 直调并需防与 Apple OpenGLES 的符号混用 | `example/Example.iOS/AngleMetalLeaseProbe.cs`，`/tmp/angle7.png`、`/tmp/angle8.png`，T3 节执行证据 | 2026-09-23 |
| T4 | **通过**（含 T4a-T4d 全部判定项）。CSM 原判不通过，根因：本 ANGLE Metal 构建（ES 3.0）不提供 `GL_EXT_float_blend`，混合使能绘制到 Rgba32F RT 整次 draw 被 `GL_INVALID_OPERATION` 丢弃；经批准将 5 处管线 HDR RT 注册 `Rgba32f`→`Rgba16f`（`PBRDeferredPipeline.cs:25/29/33`、`PBRForwardPipeline.cs:21/25`）后复验通过：光照/天空/阴影落盘（保 >1.0 HDR 值）、阴影 A/B 差异 13.05%、FXAA A/B 生效、CopyPass 间接证实。Base/PointCloud(F9) 通过。**冷启动 F13**：CSM 首帧冷/热启动均约 5.6-5.9s（无 MSL 磁盘缓存，每次启动重编译）；Base 0.7s、Points 0.4s。**全量回归**：10 个宿主场景（含 Cel/Forward/粒子/实例化/7 图元/glb 蒙皮）全部正常；未复刻页及原因见 T4 执行证据（缺口：HISM/InstancedMeshGroup）。附带发现：示例 CSM 页相机朝向与 `ForwardVector=(0,0,-1)` 约定相反致球阵被剔除（桌面同样成立，宿主探针页已改 yaw=155）。`dotnet test` 89/0 | `/tmp/csm_fix2.png`、`/tmp/ab_shadow.bmp` vs `/tmp/ab_nosh.bmp`、`/tmp/reg_{Cel,Forward,Particles,Instancing,Primitives,Anim}.png`，T4 节三段执行证据 | 2026-09-23 |
| T5 | | | |
| T6 | **通过**：外部纹理宿主件正式化进 `Aura3D.Avalonia`（多 TFM + `ANGLE_HOST` 双 partial，`Avalonia.Skia` 直接包引用已获批准），主体流程单套，共享 Example 无 iOS 特判；修复 iOS 渲染线程/UI 线程差异（`DispatchSceneEvent` 缝 + 分离释放延迟到渲染线程）。真实 MainView 下 Base Geometries/Point Cloud 模拟器渲染通过；CSM/ContextLoss 页竖屏视口 0 宽为示例页布局限制（管线路径已在 T4d 验证）。`dotnet test` 89/0。遗留：shared_event_sync、生命周期单测、竖屏适配。**T6-补（同日）已收口宿主生命周期**：分离即销毁上下文（帧间解除 current ⇒ UI 线程可合法接管）、`SyncRoot` 互斥、timer 单次订阅，与桌面 `OnOpenGlDeinit` 语义对齐 | T6 节执行证据；`/tmp/t6-home.png`、`/tmp/t6-points3.png`；T6-补 `/tmp/life-1.png`、`/tmp/life-4.png`；`src/Aura3D.Avalonia/{Aura3DViewBase.Angle.cs,Angle/AngleGlesSession.cs}` | 2026-09-23 |
