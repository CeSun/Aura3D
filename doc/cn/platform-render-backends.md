# 平台与渲染后端

`Aura3DView` 在各平台上拿 GL 上下文的来源不同。本文说明每个平台走哪条路、iOS 需要额外准备什么，以及在 GLES 子集上写自定义 Pass 时要避的坑。

## 各平台的渲染后端

| 平台 | 宿主渲染器 | Aura3D 取 GL 上下文的方式 | 应用侧配置 |
|---|---|---|---|
| Windows / Linux | 桌面 OpenGL | Avalonia `OpenGlControlBase` | 无 |
| Android | OpenGL ES | Avalonia `OpenGlControlBase` | 无 |
| macOS | OpenGL | Avalonia `OpenGlControlBase` | 无需平台特判；若视口不出图，照抄 `Example.Desktop/Program.cs` 里显式的 `AvaloniaNativeRenderingMode.OpenGl` |
| iOS | Metal（Avalonia 默认） | **自持 ANGLE(Metal) 上下文 + Skia lease 合成**；宿主显式改为 OpenGl 时回退到 `OpenGlControlBase` | 无平台特判，但需链接 ANGLE 的 iOS framework |

代码里两条路径分别位于 `src/Aura3D.Avalonia/Aura3DViewBase.OpenGl.cs` 与 `Aura3DViewBase.Angle.cs`，共享的主体流程在 `Aura3DViewBase.cs`。应用侧引用 `Aura3DView` 的签名在所有平台一致。

## iOS：为什么自持 ANGLE

Avalonia 的 iOS 宿主默认使用 Metal 合成器，而该模式下 Avalonia 不提供 GL 互操作，`OpenGlControlBase` 会静默初始化失败（画面全白、无异常）。在此之前的做法是把整个宿主 App 强制成 `iOSRenderingMode.OpenGl`，代价是绑到已废弃的 EAGL 后端。

现在 `Aura3D.Avalonia` 的 iOS 目标内置了另一条路：自己创建 ANGLE(Metal) 的 EGL 上下文完成渲染，再把输出 `MTLTexture` 经 Skia lease 零拷贝合成上屏。宿主 App 不必再做任何渲染模式上的取舍。

两条路径编译进同一个二进制，首帧判定一次归属：

- **权威信号** — `OpenGlControlBase.OnOpenGlInit` 被回调，说明宿主开了 OpenGL 模式，ANGLE 分支整体退出，由 Avalonia 驱动。
- **次级信号** — 读一次 Skia lease 的 `GRContext.Backend`：`Metal` 表示走 ANGLE，其他表示交回 `OpenGlControlBase`。

判定结果会打一行日志 `[aura3d-angle] compositor backend=..., path=...`，排查时先看这行。

## iOS：准备 ANGLE framework

1. 用 standalone ANGLE checkout（非 Chromium checkout）为 iOS 构建含 Metal 后端的产物，gn 参数只需 `enable_rust=false`，得到 `libEGL.framework` 与 `libGLESv2.framework`。构建产物不入仓库。
2. 在 iOS 应用工程中以 `NativeReference`（`Kind=Framework`、`SmartLink=False`）引入两个 framework。`example/Example.iOS/Example.iOS.csproj` 用 `AngleIosOutDir` 属性指定产物目录。
3. 注意该 `ItemGroup` 带 `Exists(...)` 条件：framework 不在时这段会静默跳过，编译照样通过，运行时 ANGLE 会话失败、视口不出图。找不到出图原因时先确认这两个 framework 真的被链接了。
4. 设备要求：ANGLE 的 Metal 后端需要 Metal GPU family 4（A11 及以后）；tvOS 不支持。`Aura3D.Avalonia` 的 iOS 目标 `SupportedOSPlatformVersion` 为 15.0。

## GLES 3.0 子集：写自定义 Pass 要知道的限制

框架的着色器与 GL 调用统一按 OpenGL ES 3.0 子集编写，以便同一条管线在桌面 GL、Android GLES 与 iOS 的 ANGLE 上都成立。其中几条在 ANGLE 上会真实咬人：

- ANGLE 的 Metal 后端只暴露 ES 3.0，申请 ES 3.1/3.2 上下文会以 `EGL_BAD_MATCH` 失败，且不提供 `GL_EXT_float_blend`。**混合开启时绘制到 32 位浮点颜色附件，整次 draw 会被当作 `GL_INVALID_OPERATION` 静默丢弃**——表现是黑屏而不是报错。框架的 HDR 渲染目标因此统一用 `Rgba16f`；自定义 Pass 不要申请 `Rgba32f` 颜色附件后又开混合。
- 深度附件 `DEPTH_COMPONENT16/24/32F` 与 `DEPTH24_STENCIL8`/`DEPTH32F_STENCIL8` 都是 ES 3.0 core，在 ANGLE 下可正常使用；上面那条只针对颜色附件。
- 没有 compute；`TexImage3D` 一类的 3D 纹理不能作为外部纹理导入。
- 着色器由 ANGLE 在运行时把 GLSL 翻成 MSL 并编译，没有磁盘二进制缓存。iOS 首帧开销实测（iPhone 17 模拟器 / iOS 27.0）：PBR 与级联阴影页约 5.6–5.9 s，简单场景页 0.4–0.7 s。

## 帧调度与线程语义

- `RequestNextFrameRendering()` 在两个后端签名一致。`AutoRequestNextFrameRendering = false` 时由应用自己逐帧请求。
- iOS 的帧回调运行在合成器渲染线程，与桌面端（回调即 UI 线程）不同：`SceneInitialized` / `SceneUpdated` / `ContextLost` / `ContextRestored` 会切回 UI 线程触发，事件处理器可以安全读写控件。
- 控件从视觉树分离即销毁上下文，并真正删除 GL 对象归还显存；场景、节点与 CPU 侧资源全部保留，重新挂载时走 `ContextRestored` 而不是再次 `SceneInitialized`。语义与桌面端 `OnOpenGlDeinit` 一致，细节见 [GPU 资源生命周期](./gpu-resource-lifecycle.md)。

## GL(EAGL) 回退路径的已知问题

只有宿主显式设置 `iOSRenderingMode.OpenGl` 时才会走到这条路径（Avalonia `OpenGlControlBase` + Apple EAGL）。iOS 默认配置不需要关心本节。目前掌握的情况来自模拟器实测、未在真机验证：

- 走 PBR Deferred 的页面帧时是分钟级——模拟器的 EAGL 是转译/软件路径，扛不住多个高分辨率浮点渲染目标加级联阴影。真机 EAGL 由硬件驱动，表现可能不同。
- 级联阴影示例页的地面网格自第 3 帧起不再产生片元，而同帧一个只吃 `gl_VertexID` 的最小 program 能稳定落地，疑似模拟器 GLES-on-Metal 层静默丢弃该 draw。

结论：iOS 上保持默认（ANGLE）路径即可，没有理由再强制 OpenGL 模式。
