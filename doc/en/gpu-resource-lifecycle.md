# GPU Resource Lifecycle

Aura3D separates CPU resources from GPU state stored in an OpenGL context. CPU resources such as `Texture`, `Geometry`, and `Material` may be shared; each `RenderPipeline` maintains the corresponding `IGpuState` objects for its own GL context.

## Ownership

| Object | Owner | Release path |
|---|---|---|
| `IResourceGpuState` (textures, geometry, materials, bone buffers) | The first `RenderPipeline` that synchronizes it | Periodic collection after the CPU resource dies, or pipeline release/destruction |
| `IRuntimeGpuState` (render targets, particle buffers, internal geometry) | The `RenderPipeline` receiving it through `EnsureSynced` | Scene removal, or pipeline release/destruction |
| Render-pass programs and immediate buffers | Their `RenderPass` | `ReleaseGpuResources()` or pipeline destruction |
| Render-target texture adapters | Their render target | The adapter never deletes the borrowed texture name |

CPU resources do not own GL names. Callers must not invoke `Destroy(GL)` directly on state that is still registered with a pipeline; release it through the scene or pipeline path so ownership remains unique.

## Operations

- `Upload(GL)` may only run while the supplied context is current. It must be able to create or update the complete GPU state from CPU data.
- `Destroy(GL)` releases every owned name while the context remains valid. It must be idempotent.
- `Invalidate()` handles an already-lost context. It performs no GL calls and resets names and synchronization state. It must be idempotent, and a later `Upload(GL)` must fully recreate the state.

## Releasing and rebuilding GPU resources

When the context is still valid but VRAM should be given back — a control detaching from the visual tree, a tab moving to the background, a low-memory fallback — call:

```csharp
scene.RenderPipeline.ReleaseGpuResources();
```

This really deletes every GL object owned by the pipeline (render-pass programs, textures, geometry buffers, render targets, shadow maps and IBL maps) while keeping the scene, nodes, CPU resources, node registrations, and GPU-state tracking. The `gl` reference is unchanged, so the next frame in the same context rebuilds everything lazily and the picture returns. Unlike context loss, no replacement context is needed and `Initialize` must not be called again.

`ReleaseGpuResources()` releases and reuses, `HandleContextLost()` only invalidates handles because the context is gone, and `Destroy()` is terminal:

| Situation | Call | GL objects | Scene/nodes | Afterwards |
|---|---|---|---|---|
| Context valid, free VRAM | `ReleaseGpuResources()` | Actually deleted | Preserved | Rebuilt next frame, same context |
| Context lost or replaced | `HandleContextLost()` | Names zeroed only | Preserved | `Initialize()`, then lazy rebuild |
| Rendering is over | `Destroy()` | Actually deleted (with a context) | Registrations cleared, pipeline unusable | Create a new pipeline |

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

### Avalonia control

`Aura3DView` performs the calls above for you: it invokes `HandleContextLost()` from `OnOpenGlLost` and re-initializes the pipeline when the replacement context is ready. Subscribe to `ContextLost` / `ContextRestored`, or read `IsContextLost`.

- Loss and recovery leave the scene, nodes, and materials untouched: `SceneInitialized` is not raised again, so a page builds its scene only once.
- Every GPU state is rebuilt lazily from the first frame after recovery. Simulated loss (which keeps the same context) does not delete the previous GL names; a real loss leaves them to the driver.
- When the control detaches from the visual tree, Avalonia calls `OnOpenGlDeinit` before destroying the context: the control runs `ReleaseGpuResources()` to actually delete GL objects and free VRAM, then `HandleContextLost()` so the pipeline can be attached again. `Scene`, its nodes, and `MainCamera` are all preserved and remain accessible while detached; re-attaching reuses the same scene instance and only rebuilds GPU resources, without raising `SceneInitialized` again. Detaching and re-attaching also raise `ContextLost` / `ContextRestored`.
- To free VRAM without detaching, call `Aura3DView.ReleaseGpuResources()`. To end the current scene deliberately, call `Aura3DView.DestroyScene()`, which releases GPU resources, clears `Scene`, and raises `SceneDestroyed`; if the control is still rendering, the next frame creates a fresh scene and raises `SceneInitialized` again.

## Final destruction

`RenderPipeline.Destroy()` is terminal. It first runs `ReleaseGpuResources()`, then clears GPU-state tracking, resource caches, and scene registrations. It is safe to call repeatedly, but a destroyed pipeline cannot be initialized again; create a new pipeline to resume rendering.

If no context exists when destruction occurs, `Destroy()` degrades to the no-GL `HandleContextLost()` path.

## Custom GPU state

A custom `IGpuState` must:

1. Retain enough CPU data for `Upload` to recreate every name in a replacement context.
2. Delete only its owned nonzero names in `Destroy`, then reset all names and `SyncedVersion`.
3. Make no GL calls in `Invalidate`, while resetting the same names and synchronization state.
4. Never delete borrowed names; only clear local cached state.
5. Transfer lifecycle management to the pipeline after passing the state to `EnsureSynced`.

Do not submit the same `IGpuState` instance to multiple pipelines. Existing custom implementations must add the no-GL `Invalidate()` method when upgrading; normally it resets every name and `SyncedVersion` to zero.
