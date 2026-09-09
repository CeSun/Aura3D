# GPU Resource Lifecycle

Aura3D separates CPU resources from GPU state stored in an OpenGL context. CPU resources such as `Texture`, `Geometry`, and `Material` may be shared; each `RenderPipeline` maintains the corresponding `IGpuState` objects for its own GL context.

## Ownership

| Object | Owner | Release path |
|---|---|---|
| `IResourceGpuState` (textures, geometry, materials, bone buffers) | The first `RenderPipeline` that synchronizes it | Periodic collection after the CPU resource dies, or pipeline destruction |
| `IRuntimeGpuState` (render targets, particle buffers, internal geometry) | The `RenderPipeline` receiving it through `EnsureSynced` | Scene removal or pipeline destruction |
| Render-pass programs and immediate buffers | Their `RenderPass` | Pipeline destruction |
| Render-target texture adapters | Their render target | The adapter never deletes the borrowed texture name |

CPU resources do not own GL names. Callers must not invoke `Destroy(GL)` directly on state that is still registered with a pipeline; release it through the scene or pipeline path so ownership remains unique.

## Operations

- `Upload(GL)` may only run while the supplied context is current. It must be able to create or update the complete GPU state from CPU data.
- `Destroy(GL)` releases every owned name while the context remains valid. It must be idempotent.
- `Invalidate()` handles an already-lost context. It performs no GL calls and resets names and synchronization state. It must be idempotent, and a later `Upload(GL)` must fully recreate the state.

## Context loss and recovery

After the host detects context loss, call:

```csharp
scene.RenderPipeline.HandleContextLost();
```

This preserves the scene, CPU resources, and registrations while invalidating all context-owned state. Attach the replacement context with:

```csharp
scene.RenderPipeline.Initialize(getProcAddress);
```

Resources are recreated lazily. Shadow maps, IBL convolution maps, and cached render targets are regenerated as well. Do not call `Destroy(GL)` after loss because the old names no longer belong to an accessible context.

## Final destruction

`RenderPipeline.Destroy()` is terminal. It releases resources when a context remains valid, clears caches and registrations, and is safe to call repeatedly. A destroyed pipeline cannot be initialized again; create a new pipeline to resume rendering.

If no context exists when destruction occurs, the pipeline automatically follows the no-GL `Invalidate()` path.

## Custom GPU state

A custom `IGpuState` must:

1. Retain enough CPU data for `Upload` to recreate every name in a replacement context.
2. Delete only its owned nonzero names in `Destroy`, then reset all names and `SyncedVersion`.
3. Make no GL calls in `Invalidate`, while resetting the same names and synchronization state.
4. Never delete borrowed names; only clear local cached state.
5. Transfer lifecycle management to the pipeline after passing the state to `EnsureSynced`.

Do not submit the same `IGpuState` instance to multiple pipelines. Existing custom implementations must add the no-GL `Invalidate()` method when upgrading; normally it resets every name and `SyncedVersion` to zero.
