# GPU 资源生命周期

Aura3D 将 CPU 资源与 OpenGL 上下文中的 GPU 状态分离。`Texture`、`Geometry`、`Material` 等 CPU 资源可以被多个场景引用；每个 `RenderPipeline` 为自己的 GL 上下文维护对应的 `IGpuState`。

## 所有权

| 对象 | 所有者 | 释放方式 |
|---|---|---|
| `IResourceGpuState`（纹理、几何、材质、骨骼缓冲） | 首次同步它的 `RenderPipeline` | CPU 资源被回收后由管线定期收集，或随管线销毁 |
| `IRuntimeGpuState`（RenderTarget、粒子缓冲、内部几何） | 将它传给 `EnsureSynced` 的 `RenderPipeline` | 从场景移除时由管线释放，或随管线销毁 |
| RenderPass 着色器和即时绘制缓冲 | 对应 `RenderPass` | 随管线销毁 |
| RenderTarget 附件纹理适配器 | RenderTarget | 适配器不单独删除纹理名 |

CPU 资源本身不拥有 GL 句柄。调用方不应对仍注册在管线中的状态直接调用 `Destroy(GL)`；应通过场景/管线的移除流程释放，避免重复所有权。

## 三种操作

- `Upload(GL)`：仅在参数所代表的 GL 上下文当前有效时调用。它必须能够从 CPU 数据完整创建或更新 GPU 状态。
- `Destroy(GL)`：上下文仍有效时释放该状态拥有的全部句柄。实现必须可重复调用；第二次调用不得再次删除旧句柄。
- `Invalidate()`：上下文已经丢失时使用，不执行任何 GL 调用，只清零句柄与同步版本。实现必须可重复调用，后续 `Upload(GL)` 必须可以完整重建资源。

## 上下文丢失与恢复

宿主检测到上下文丢失后调用：

```csharp
scene.RenderPipeline.HandleContextLost();
```

该方法保留场景、CPU 资源和管线注册，仅使所有上下文相关状态失效。取得新的上下文后重新附加：

```csharp
scene.RenderPipeline.Initialize(getProcAddress);
```

资源随后按需重建；阴影图、IBL 卷积贴图和缓存 RenderTarget 也会重新生成。不要在上下文已经丢失后调用 `Destroy(GL)`，因为旧 GL 对象名已不再属于可访问的上下文。

## 最终销毁

`RenderPipeline.Destroy()` 是终止操作：它释放仍有效上下文中的资源，清空管线缓存和场景注册，并且可以安全地重复调用。调用后该管线不能再次初始化；需要继续渲染时应创建新的管线实例。

如果销毁时已经没有有效上下文，管线自动采用 `Invalidate()` 路径，不会尝试调用 GL。

## 自定义 GPU 状态

自定义 `IGpuState` 必须遵守以下要求：

1. 保存足够的 CPU 数据，使 `Upload` 能在新上下文中重建全部句柄。
2. `Destroy` 只删除自身拥有的非零句柄，并将所有句柄和 `SyncedVersion` 归零。
3. `Invalidate` 不调用 GL，同样将所有句柄和 `SyncedVersion` 归零。
4. 借用的句柄不得删除；借用方只清理自己的缓存状态。
5. 将状态交给 `EnsureSynced` 后，其生命周期由该管线管理。

同一个 `IGpuState` 实例不得同时交给多个管线。升级已有自定义实现时，需要新增无 GL 调用的 `Invalidate()`；通常将所有句柄和 `SyncedVersion` 设为零即可。
