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
| F12 | **ANGLE 目前只能在 Chromium 的 checkout 里为 iOS 构建**（`doc/DevSetup.md` 明写），iOS 产物形态是 `ios_framework_bundle` 而非裸静态库。 | 源码取证 |
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

### T2 判 ANGLE 生死：能不能为 iOS 构建出来（2–4 小时，只做构建，不做集成）

F12 说只能从 Chromium checkout 构建。这一步的目的就是把这句话验真或验伪，因为它决定 R2 是否存在。

步骤：

1. 装 `depot_tools`，按 ANGLE 的 `doc/DevSetup.md` 与 Chromium 的 iOS 搭建文档取一个**只用于编译 ANGLE 目标**的最小 checkout（不要整仓同步：用 `gclient sync` 的裁剪手段或只拉 ANGLE 依赖所需的部件；能编出目标即算数）。
2. 生成构建：`gn gen out/ios --args='target_os="ios" target_environment="simulator" target_cpu="arm64" is_component_build=false angle_enable_metal=true is_debug=false'`（真机把 `target_environment` 去掉并改 `target_cpu="arm64"`）。参数名以仓库当前 `gni/angle.gni` 为准，报错就照实修正，不要臆造。
3. `ninja -C out/ios angle:libEGL angle:libGLESv2`（目标名以 `BUILD.gn` 为准）。
4. 检查产物：是否得到可链接的 framework/静态库；用 `nm -gU` 或 `strings` 确认里面**含 Metal 后端符号**（例如 `DisplayMtl`、`RendererMtl`），而不是只把 Vulkan/SPIRV 后端编进去了。
5. 记录：总耗时、checkout 体积、产物体积、必需的 GN 参数、以及为了编过是否改了上游代码（改了就是维护成本，必须写明）。

判定标准：拿到含 Metal 后端的 iOS 产物 ⇒ R2 存活，进 T3。以下任一情况判不通过：无法在合理时间内得到最小 checkout；必须魔改 ANGLE 源码才能编过；产物里没有 Metal 符号。判不通过就**停止**，把结论写清，转 T5。

### T3 ANGLE 最小互操作实验（依赖 T2 通过；1–2 小时）

目的：证明"我们分配的 `MTLTexture` → 交给 ANGLE 当 GL 渲染目标 → ANGLE 画完 → 同一张纹理交给 Skia lease 上屏"这条闭环成立。这一步是整个 R2 的技术核心，也是 F8 里唯一没逐字确认的部分。

0. 先读 `extensions/EGL_ANGLE_metal_texture_client_buffer.txt` 全文，确认**入口点到底是 `eglCreatePbufferFromClientBuffer` 还是 `eglCreateImage`/`eglCreateImageKHR`**，以及需要什么 display 属性。注意已知事实：`eglCreatePbufferFromClientBuffer` 在 Metal 后端只接受 `EGL_IOSURFACE_ANGLE`，所以极可能要走 EGLImage 那条；把结论写进执行结果。
1. 在 `example/Example.iOS` 里新建一个临时页（复用 T1 探针的骨架），用 `[DllImport]` 或 xcframework 桥调用 ANGLE 的 EGL/GLES 入口。创建 EGL display 时**必须把 `MTLDevice.SystemDefault`（也就是 F2 里同一个 device）显式交给 ANGLE**（F8 的 device 相等约束）。
2. 用与 T1 探针相同的方式分配一张 `MTLTexture`（`ShaderRead` 用法、非 Private 存储），把它作为 client buffer 导入成 GL 纹理并挂到 FBO 的颜色附件上。
3. 在 ANGLE 里把这张纹理清成一个与 UI 无任何重合的可辨识颜色（例如纯洋红），并每帧改一下颜色或画一条移动的带子。
4. 用 T1 已经验证过的 lease 导入路径把它画上屏。
5. 截图确认：屏幕上出现洋红且逐帧变化。

判定标准：颜色出现且逐帧变化 ⇒ R2 成立，进 T4。若导入成功但画不出/画出黑图，先分别排查同步（ANGLE 的命令缓冲是否在我们读取前完成）与 device 不匹配，把两次尝试的现象都记下来再报。这一步之后，正式实现还需要把同步换成 `EGL_ANGLE_metal_shared_event_sync` 一类机制，但那属于 T4 之后。

### T4 把真实渲染搬上 ANGLE，逐项验 F9/F11 的风险点（依赖 T3 通过）

顺序建议：先 `Base Geometries`（最基础的着色器路径），再按风险点单独验。

- 点云：`PointCloud` 页。风险是点尺寸被 ANGLE 限制（F9）。判定：点可见、尺寸合理、与桌面端视觉一致。
- CSM：`CascadedShadowMaps` 页，走 `Texture2DArray` + `FramebufferTextureLayer` 的深度附件路径。判定：阴影分级正确、无黑块。
- FXAA / CopyPass：`BlitFramebuffer` 的缩放与格式转换路径，以及 HDR 相关 RT。判定：无错图、无花屏。
- 冷启动耗时：记录首帧与进入各页时的可感知卡顿，ANGLE 运行时编 MSL 且无磁盘二进制缓存（F13），预期会有首次编译开销。数字要写下来。
- 全量回归：Example 里所有页在 iOS Metal + ANGLE 下过一遍，失败项逐条列。

判定标准：以上四项全部有明确记录，且没有阻断性错图。

### T5 R2 判死后的替代验证（只在 T2 或 T3 不通过时做）

先做 R3 的纸面与最小实验（不需要 ANGLE）：

1. 自持 EAGL 上下文的可行性：`dlopen("/System/Library/Frameworks/OpenGLES.framework/OpenGLES")` + `dlsym` 取函数指针（Avalonia.iOS 自己就是这么干的，F10），Silk.NET 侧用现成的委托构造即可，不改 Aura3D.Core。
2. IOSurface 桥的可行性：`CVPixelBuffer` + `IOSurface` → GL 侧用 `CVOpenGLESTextureCacheCreateTextureFromImage`（.NET iOS 未绑定，需要手写 `[DllImport]`）当渲染目标；Metal 侧用已绑定的 `IMTLDevice.CreateTexture(MTLTextureDescriptor, IOSurface, nuint plane)` 包成 `MTLTexture`，再走 F2 的 lease 路。
3. 判定标准：同一帧数据能被 Metal 读到（先在洋红清屏级别验证），再验证跨 API 同步（GL 写完后 Metal 读到的是新内容而不是上一帧）。
4. 若 F6（EAGL 纹理不经 IOSurface 不能给 Metal）在这一步被证伪或证实，写回第 1 节。

R3 与 R2 的取舍要点：两者都不动 Core，但 R3 继续绑在 Apple 已废弃的 EAGL 上，R2 才能真正做到全栈无废弃 API。

### T6 与路线无关的收尾（可在任何平台做，不占 Mac 时间）

F2 的通路目前只是探针，正式化需要：在 `Aura3D.Avalonia` 里提供一个"外部纹理宿主件"，取代在 Apple 上走不通的 `OpenGlControlBase` 互操作路；它应当只依赖"渲染侧给一张与合成器同 API 的纹理句柄 + 尺寸 + 朝向"，并带单元测试。R1/R2/R3 三条路线最终都要接这个件，所以先做不会错。注意：给 `Aura3D.Avalonia` 增加对 `Avalonia.Skia` 的依赖要有意识地决策（目前探针放在 `example/Example.iOS` 就是为了不顺手把 Skia 依赖塞进库）。

## 4. 探针代码与清理

探针在提交 4a10035（分支 `feature/metal-lease-presentation`）里，共三处改动，验证结束后按需删除或转正：

- `example/Example.iOS/MetalLeaseProbe.cs`（新文件）
- `example/Example.iOS/AppDelegate.cs`：删掉了 `[OpenGl]` 强制，并注入 `RootViewFactory`
- `example/Example/App.axaml.cs`：新增 `RootViewFactory` 钩子（临时用，正式实现不该保留）

删除清单：删 `MetalLeaseProbe.cs`、还原 `AppDelegate.cs` 的 `CustomizeAppBuilder`、删掉 `App.RootViewFactory` 及其在 `OnFrameworkInitializationCompleted` 里的使用。

## 5. 执行结果（待填）

| 任务 | 结论 | 关键证据 | 日期 |
|---|---|---|---|
| T1 | | | |
| T2 | | | |
| T3 | | | |
| T4 | | | |
| T5 | | | |
| T6 | | | |
