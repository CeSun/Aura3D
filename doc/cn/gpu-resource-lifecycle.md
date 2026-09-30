---
section: advanced
order: 5
---

# GPU 资源生命周期

显存归谁、何时释放、上下文丢失后如何恢复。平时不用读——`Aura3DView` 都替你做了；需要手动归还显存，或遇到「切后台黑屏」「重建后还是旧画面」时再回来。

## 一分钟心智模型

- **CPU 侧**是 `Texture`、`Geometry`、`Material`、`Mesh`、`Node` 这些对象。它们想被怎么引用都行，可以被多个场景共用，**不持有 GL 句柄**。
- **GPU 侧**是这些资源在某个具体 OpenGL 上下文里的投影，实现是 `IGpuState`（[源码](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/GpuStates/IGpuState.cs)），由**管线**持有。每个 `RenderPipeline` 只服务自己那个上下文。
- 两者靠**版本号**对齐：`IGpuState` 有 `Version` 与 `SyncedVersion`。你在 CPU 侧改一次数据，`Version` 就 +1；渲染时管线比对两个版本，不一致才调用 `Upload(gl)` 重传一次，然后记下同步版本。同帧改一百次也只传一次。

由此得到两条铁律：**别绕过管线去删状态**（`Destroy(gl)` 只能由拥有它的管线来调），以及**上下文丢了就只能「忘」，不能「删」**（旧句柄已经不属于任何可访问的上下文）。

## 谁拥有哪块显存

| 东西 | 所有者 | 什么时候真正释放 |
|---|---|---|
| 纹理 / 几何 / 材质 / 骨骼缓冲的 GPU 状态（资源侧） | **第一个**同步它的 `RenderPipeline` | CPU 资源被回收后由管线定期收集，或随管线 `ReleaseGpuResources()` / `Destroy()` |
| RenderTarget、粒子缓冲、引擎内部几何（运行时状态，`IRuntimeGpuState`） | 接收它的那次 `EnsureSynced(...)` 所属管线 | 从场景移除时由管线释放，或随管线释放 / 销毁 |
| RenderPass 的着色器程序与即时绘制缓冲 | 对应的 `RenderPass` | `ReleaseGpuResources()`，或管线销毁 |
| RenderTarget 的附件纹理适配器 | 那个 RenderTarget | 适配器**不会**去删借来的纹理名 |

自定义 Pass 里把状态交给管线接管，走的就是 `renderPipeline.EnsureSynced(gpuState)`；交出去之后它的生命周期归这条管线，你不要再自己 `Destroy`。同一个 `IGpuState` 实例也不要同时交给两条管线。

## 三种操作：Upload / Destroy / Invalidate

`IGpuState` 只有这三个动作（加上两个版本号）。它们的契约就是全部规则：

| 操作 | 前提 | 必须做到 |
|---|---|---|
| `Upload(GL gl)` | 传入的上下文**当前有效** | 只凭 CPU 侧数据就能完整创建或更新这份 GPU 状态——包括在全新上下文上从零重建 |
| `Destroy(GL gl)` | 上下文**仍然有效** | 删掉自己**拥有**的全部非零句柄；可重复调用，第二次不得再删旧句柄 |
| `Invalidate()` | 上下文**已经丢了**时用 | **不发任何 GL 调用**，只把句柄与 `SyncedVersion` 归零；可重复调用，之后的 `Upload` 必须能完整重建 |

## 该按哪个按钮：ReleaseGpuResources / HandleContextLost / Destroy

| 你的处境 | 调用 | GL 对象 | 场景与节点 | 之后 |
|---|---|---|---|---|
| 上下文还在，只想把显存还回去（切后台、低内存降级、这一页不画了但还会回来） | `scene.RenderPipeline.ReleaseGpuResources()` | **真删** | 全保留 | 同一上下文下一帧按需全部重建，画面自动回来，不需要重新 `Initialize` |
| 上下文丢了或被换掉了 | `scene.RenderPipeline.HandleContextLost()` | 只归零句柄，不删 | 全保留 | 拿到新上下文后 `Initialize(getProcAddress)`（`Func<string, nint>`），资源按需重建 |
| 彻底不画了，管线也要退役 | `pipeline.Destroy()` | 真删（有上下文时）；没上下文时自动退化成 `HandleContextLost()` | 清空注册、缓存与状态跟踪 | **不可逆**：这条管线再也初始化不了，要继续渲染就新建一条 |

`Destroy()` 可以安全重复调用；`ReleaseGpuResources()` 在管线已销毁后会抛 `ObjectDisposedException`，`HandleContextLost()` 则直接返回什么都不做。

## 用 Aura3DView 时：控件已经替你做了

只要用 `Aura3DView`，上面这些接口你一个都不用手调。控件的行为是这样的：

- **宿主报上下文丢失** → 控件内部执行 `HandleContextLost()`，把 `IsContextLost` 置 `true`，派发 `ContextLost` 事件，并请求一帧来推进恢复。
- **下一帧取得新上下文** → 控件重新 `Initialize`，把 `IsContextLost` 置回 `false`，派发 `ContextRestored`。**不会**再派发 `SceneInitialized`——场景、节点、材质都是原来那一批实例。所以一个页面只在首次初始化时建一次场景，别指望在 `SceneInitialized` 里「重建」。
- **控件从视觉树分离**（切页、折叠面板收起）→ Avalonia 在销毁上下文之前通知控件：此时上下文还活着，于是先 `ReleaseGpuResources()` 真正删掉 GL 对象归还显存，再 `HandleContextLost()` 让管线可被重新挂载，并派发 `ContextLost`。
- **重新挂回视觉树** → 走上面「取得新上下文」那条路径，复用同一个 `Scene` 实例，只重建 GPU 资源，派发 `ContextRestored`。分离期间 `Scene`、节点、`MainCamera` 都还在，可以继续读写。

你能主动做的三件事：

```csharp
// 1) 归还显存，但控件不分离、场景不丢：下一帧画面自己长回来
view.ReleaseGpuResources();

// 2) 彻底结束当前场景：释放 GPU 资源 + 销毁管线 + 清空 Scene，随后派发 SceneDestroyed
view.DestroyScene();

// 3) 开发期验证恢复逻辑是否写对了：不依赖驱动，主动走一遍丢失路径
view.SimulateContextLost();
```

> [!IMPORTANT]
> 这三件事都**不是当场生效**的。GL 调用只能在渲染线程执行，所以控件只记一个请求，实际动作发生在**下一帧渲染开始时**（它们各自已经替你请求了一帧）。这意味着：按需渲染的页面点了按钮至少要看到一帧跳过去；`DestroyScene` 之后 `view.Scene` 立刻读到的仍是旧场景，正确做法是在 `SceneDestroyed` 回调里清自己的缓存字段——事件参数带着那个已被销毁的场景，别再去读 `view.Scene`。

事件签名一览（都在 `Aura3D.Avalonia`，参数对象都有 `Scene`）：

| 事件 | 参数类型 | 什么时候 |
|---|---|---|
| `SceneInitialized` | `InitializedRoutedEventArgs` | 首次创建场景（`Scene` 原来是 `null`） |
| `ContextLost` | `ContextLostRoutedEventArgs` | 真实丢失、模拟丢失、以及控件分离 |
| `ContextRestored` | `ContextRestoredRoutedEventArgs` | 已有场景重新拿到上下文（含重新挂载） |
| `SceneDestroyed` | `DestroyedRoutedEventArgs` | `DestroyScene()` 生效之后 |
| `SceneUpdated` | `UpdateRoutedEventArgs` | 每帧渲染前，带 `DeltaTime` |

配套只读属性 `view.IsContextLost`；想区分「管线还能不能用」可以看 `scene.RenderPipeline.IsInitialized` / `IsDestroyed`。

## 自定义 IGpuState 必须守的 5 条

写自己的 GPU 状态（自定义 Pass 的中间缓冲、渲染到纹理的私有资源等）时：

1. **留够 CPU 侧数据**，让 `Upload` 能在一个**全新**上下文上把每个句柄重建出来——上下文换了以后没有「旧对象」可查。
2. **`Destroy` 只删自己拥有的非零句柄**，然后把所有句柄与 `SyncedVersion` 归零。
3. **`Invalidate` 一个 GL 调用都不发**，同样把所有句柄与 `SyncedVersion` 归零。
4. **借来的句柄不删**（例如 RenderTarget 的附件纹理）：只清自己那份缓存状态。
5. **交给 `EnsureSynced` 之后就把生命周期交给这条管线**：不再自己 `Destroy`，也不给第二条管线用。

给已有的自定义实现做升级时，主要缺口是新增无 GL 调用的 `Invalidate()`——通常把所有句柄和 `SyncedVersion` 设成零就对了。

## 常见坑

- **在 `SceneInitialized` 里建场景、又在 `ContextRestored` 里再建一遍**：恢复路径不重建场景，节点会被加两遍。恢复后要做的事只有「按需刷新你自己的缓存字段」。
- **以为 `ReleaseGpuResources()` 之后画面立刻没了**：它删的是 GPU 侧对象，下一帧立刻全量重建，肉眼看到的只是一次卡顿；要的是「分离出去这段时间不占显存」这个效果。
- **上下文丢了以后调 `Destroy(gl)`**：旧句柄已不属于任何可访问的上下文，这时只能走 `Invalidate()` / `HandleContextLost()`。
- **模拟丢失不等于真实丢失**：`SimulateContextLost()` 复用同一个上下文，旧 GL 名称不会被驱动回收；真实丢失时才由驱动负责回收。别用模拟来验证「显存到底掉没掉」。
- **`DestroyScene()` 之后继续用旧的 `Mesh` / `Material` 引用**：管线已经销毁并清空注册，场景字段要重新赋值；继续渲染时控件会创建新场景并再发一次 `SceneInitialized`。
- **关了自动渲染又改了实例化分组**：`InstancedMeshGroup` 的后台构建要由后续几帧来收尾，一帧都不请求就会一直什么都没有（见[实例化渲染](./instanced-rendering.md)）。
- **`Destroy()` 之后还想 `Initialize`**：`Destroy` 是终止操作，改这条管线不行，新建一条。
- **`ReleaseGpuResources()` 抛 `ObjectDisposedException`**：管线已经 `Destroy` 过了，别再调。
- **同一个 `IGpuState` 交给两条管线**：所有权不再唯一，会重复删除同一个句柄，表现为随机 GL 报错。

## 可运行示例

- GpuLifecycle 示例把三条回收路径 + 五个场景事件全做成了可点按的按钮，带帧号与事件计数读数：[GpuLifecycleDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/GpuLifecycle/GpuLifecycleDemo.axaml.cs)
- 相关源码：[IGpuState.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/GpuStates/IGpuState.cs)、[RenderPipeline.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/RenderPipeline.cs)、[Aura3DViewBase.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Avalonia/Aura3DViewBase.cs)

## 下一步

- 平台差异（哪个后端真会丢上下文、丢完怎么恢复）：[平台与渲染后端](./platform-render-backends.md)
- 实例缓冲这类大头资源怎么用得更省：[实例化渲染](./instanced-rendering.md)
- 自定义 Pass 与 `EnsureSynced`：[自定义渲染管线](./custom-pipeline.md)
- 按症状查这一类现象：[常见坑与排障](./troubleshooting.md)
