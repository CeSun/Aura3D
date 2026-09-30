---
section: advanced
order: 5
---

# GPU Resource Lifecycle

This is the "read it when something breaks or when you need VRAM back" mechanism page: who actually owns the memory, when GL objects really get deleted, and why the picture comes back on its own after a context loss. Day-to-day scene code does not need it — with `Aura3DView` the control already does the right thing. Come back to this page when you must hand VRAM back manually, or when you hit "black screen after returning from background" / "still the old frame after a rebuild".

## The mental model in one minute

- **CPU side** is `Texture`, `Geometry`, `Material`, `Mesh`, `Node`. Reference them however you like; several scenes can share them, and they **hold no GL handles**.
- **GPU side** is the projection of those resources inside one specific OpenGL context, implemented as `IGpuState` ([source](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/GpuStates/IGpuState.cs)) and owned by the **pipeline**. Each `RenderPipeline` serves only its own context.
- The two sides stay aligned through **version numbers**: `IGpuState` exposes `Version` and `SyncedVersion`. Every CPU-side edit bumps `Version`; at render time the pipeline compares the two and calls `Upload(gl)` only when they differ, then records the synced version. A hundred edits in one frame cost one upload.

Which yields the two iron rules: **never delete state behind the pipeline's back** (`Destroy(gl)` may only be called by the pipeline that owns it), and **after a context loss you can only forget, never delete** (the old handles no longer belong to any accessible context).

## Who owns which piece of VRAM

| Thing | Owner | When it is actually released |
|---|---|---|
| GPU state of textures / geometry / materials / bone buffers (resource state) | The **first** `RenderPipeline` that synchronizes it | Periodic collection once the CPU resource dies, or via the pipeline's `ReleaseGpuResources()` / `Destroy()` |
| Render targets, particle buffers, engine-internal geometry (`IRuntimeGpuState`) | The pipeline whose `EnsureSynced(...)` received it | Scene removal, or pipeline release / destruction |
| Render-pass shader programs and immediate draw buffers | The `RenderPass` itself | `ReleaseGpuResources()`, or pipeline destruction |
| Render-target attachment texture adapters | That render target | The adapter **never** deletes the borrowed texture name |

A custom pass hands its state to the pipeline through `renderPipeline.EnsureSynced(gpuState)`; from that moment the pipeline manages its lifetime, so stop calling `Destroy` on it yourself. And never give the same `IGpuState` instance to two pipelines.

## Three operations: Upload / Destroy / Invalidate

`IGpuState` has exactly these three actions (plus the two version numbers). Their contract *is* the whole rulebook:

| Operation | Precondition | Guarantee required |
|---|---|---|
| `Upload(GL gl)` | The supplied context is **currently valid** | Able to create or update the complete GPU state from CPU-side data alone — including rebuilding from scratch on a brand-new context |
| `Destroy(GL gl)` | The context is **still valid** | Deletes every **owned** non-zero handle; safe to call repeatedly, the second call must not delete old handles again |
| `Invalidate()` | Use once the context is **already lost** | Issues **no GL calls**; zeroes all handles and `SyncedVersion`; safe to call repeatedly, and a later `Upload` must fully recreate the state |

## Which button to press: ReleaseGpuResources / HandleContextLost / Destroy

| Your situation | Call | GL objects | Scene and nodes | Afterwards |
|---|---|---|---|---|
| Context still alive, you just want VRAM back (backgrounded, low-memory fallback, "not drawing this page now but I'll come back") | `scene.RenderPipeline.ReleaseGpuResources()` | **Really deleted** | Fully preserved | Everything is rebuilt lazily on the next frame in the same context; the picture returns; no re-`Initialize` needed |
| Context lost or replaced | `scene.RenderPipeline.HandleContextLost()` | Handles zeroed only, nothing deleted | Fully preserved | `Initialize(getProcAddress)` (`Func<string, nint>`) with the new context, then lazy rebuild |
| Rendering is over and the pipeline retires too | `pipeline.Destroy()` | Really deleted when a context exists; degrades to the `HandleContextLost()` path when none does | Registrations, caches and state tracking cleared | **Terminal**: this pipeline can never be initialized again; create a new one to keep rendering |

`Destroy()` is safe to call repeatedly. `ReleaseGpuResources()` throws `ObjectDisposedException` once the pipeline is destroyed, while `HandleContextLost()` simply returns and does nothing.

## Using Aura3DView: the control already does this

With `Aura3DView` you never call any of the above by hand. The control behaves like this:

- **The host reports context loss** → the control runs `HandleContextLost()` internally, sets `IsContextLost` to `true`, raises `ContextLost`, and requests a frame to drive recovery.
- **The next frame obtains a context** → the control calls `Initialize` again, sets `IsContextLost` back to `false`, and raises `ContextRestored`. It does **not** raise `SceneInitialized` again — the scene, nodes and materials are the very same instances. So a page builds its scene once, on first initialization; don't plan on "rebuilding" inside `SceneInitialized`.
- **The control detaches from the visual tree** (page switch, collapsed panel) → Avalonia notifies the control *before* destroying the context. Since the context is still alive, the control runs `ReleaseGpuResources()` to actually delete GL objects and free VRAM, then `HandleContextLost()` so the pipeline can be attached again, and raises `ContextLost`.
- **Re-attached to the visual tree** → the "obtained a context again" path above, reusing the same `Scene` instance and rebuilding only GPU state, raising `ContextRestored`. While detached, `Scene`, its nodes and `MainCamera` all remain available for reading and writing.

The three things you can trigger deliberately:

```csharp
// 1) Hand VRAM back without detaching the control or losing the scene: the picture regrows next frame
view.ReleaseGpuResources();

// 2) End the current scene for real: release GPU resources + destroy the pipeline + clear Scene, then SceneDestroyed
view.DestroyScene();

// 3) Verify your recovery logic during development without depending on a driver: run the loss path on purpose
view.SimulateContextLost();
```

> [!IMPORTANT]
> None of these three takes effect on the spot. GL calls may only run on the render thread, so the control just records a request and the actual work happens **at the start of the next rendered frame** (each of them requests that frame for you). Practically: an on-demand page will visibly advance at least one frame after you press the button, and `view.Scene` still returns the old scene immediately after `DestroyScene()` — clear your own cached fields in the `SceneDestroyed` handler instead, whose event args carry the already-destroyed scene; don't read `view.Scene` there.

Event overview (all in `Aura3D.Avalonia`, and every args object exposes `Scene`):

| Event | Args type | When |
|---|---|---|
| `SceneInitialized` | `InitializedRoutedEventArgs` | First scene creation (`Scene` was `null`) |
| `ContextLost` | `ContextLostRoutedEventArgs` | Real loss, simulated loss, and control detachment |
| `ContextRestored` | `ContextRestoredRoutedEventArgs` | An existing scene gets a context again (including re-attachment) |
| `SceneDestroyed` | `DestroyedRoutedEventArgs` | After `DestroyScene()` takes effect |
| `SceneUpdated` | `UpdateRoutedEventArgs` | Before every rendered frame, carries `DeltaTime` |

Plus the read-only `view.IsContextLost`; to tell whether the pipeline is still usable, look at `scene.RenderPipeline.IsInitialized` / `IsDestroyed`.

## Five rules a custom IGpuState must follow

When you write your own GPU state (intermediate buffers for a custom pass, private resources for render-to-texture, …):

1. **Keep enough CPU-side data** so that `Upload` can recreate **every** handle on a **brand-new** context — after a context switch there is no old object to query.
2. **`Destroy` deletes only its own non-zero handles**, then zeroes all handles and `SyncedVersion`.
3. **`Invalidate` issues no GL call at all**, and zeroes the same handles and `SyncedVersion`.
4. **Never delete borrowed handles** (e.g. a render target's attachment textures): clear only your own cached state.
5. **Once passed to `EnsureSynced`, the lifecycle belongs to that pipeline**: don't `Destroy` it yourself, and don't hand it to a second pipeline.

When upgrading an existing custom implementation, the usual gap is the new no-GL `Invalidate()` — normally setting every handle and `SyncedVersion` to zero is all it takes.

## Common pitfalls

- **Building the scene in `SceneInitialized` and again in `ContextRestored`**: the recovery path doesn't rebuild the scene, so your nodes get added twice. After recovery all you do is refresh your own cached fields.
- **Expecting the picture to disappear right after `ReleaseGpuResources()`**: it deletes GPU-side objects which are rebuilt wholesale on the next frame, so all you notice is a hitch. What you actually bought is "no VRAM held while this page is off-screen".
- **Calling `Destroy(gl)` after a context loss**: those handles no longer belong to an accessible context; only `Invalidate()` / `HandleContextLost()` are valid there.
- **Simulated loss is not real loss**: `SimulateContextLost()` keeps the same context, so the previous GL names are not reclaimed by a driver; only a real loss leaves that to the driver. Don't use the simulation to verify "did VRAM actually drop".
- **Reusing old `Mesh` / `Material` references after `DestroyScene()`**: the pipeline is destroyed and its registrations cleared, so reassign your scene fields; the control creates a fresh scene on the next rendered frame and raises `SceneInitialized` again.
- **Turning off auto-rendering while also changing an instancing group**: `InstancedMeshGroup`'s background build is adopted by subsequent frames — request no frames and nothing ever appears (see [Instanced Rendering](./instanced-rendering.md)).
- **Trying to `Initialize` after `Destroy()`**: `Destroy` is terminal for that pipeline; create a new one.
- **`ReleaseGpuResources()` throws `ObjectDisposedException`**: the pipeline was already `Destroy`ed; stop calling it.
- **The same `IGpuState` given to two pipelines**: ownership stops being unique and the handle gets deleted twice, which shows up as random GL errors.

## Runnable examples

- The GpuLifecycle demo wires all three reclaim paths plus the five scene events into clickable buttons, with a frame number and event counters readout: [GpuLifecycleDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/GpuLifecycle/GpuLifecycleDemo.axaml.cs)
- Related sources: [IGpuState.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/GpuStates/IGpuState.cs), [RenderPipeline.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/RenderPipeline.cs), [Aura3DViewBase.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Avalonia/Aura3DViewBase.cs)

## Next steps

- Instance buffers and what they cost in VRAM: [Instanced Rendering](./instanced-rendering.md)
- Custom passes that own GPU state: [Custom Render Pipelines](./custom-pipeline.md)
- Per-platform context-loss behavior: [Platforms and Render Backends](./platform-render-backends.md)
- Symptom-first lookup: [Common Pitfalls and Troubleshooting](./troubleshooting.md)
