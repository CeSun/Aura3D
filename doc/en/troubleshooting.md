---
section: notes
order: 2
---

# Common Pitfalls and Troubleshooting

This page is indexed by **symptom**: find your situation in the table, jump to the section, and each entry gives "likely cause → what to do → where to read more". Full tutorials live elsewhere; only repeatedly-tripped-on pitfalls are collected here.

| Symptom | Jump |
|---|---|
| Black screen / nothing renders at all | [#black-screen](#black-screen) |
| Surfaces flicker, overlapping faces can't be ordered (Z-Fighting) | [#z-fighting](#z-fighting) |
| Distant or animated models wrongly culled, vanishing into thin air | [#culling](#culling) |
| macOS viewport stays blank | [#macos](#macos) |
| Black screen on iOS once blending is on (float render targets) | [#ios-blend](#ios-blend) |
| Something vanishes silently in the browser but works on desktop | [#browser-drops](#browser-drops) |
| Browser crashes on the first frame / aborts at startup | [#browser-crash](#browser-crash) |
| First frame very slow on iOS / browser | [#slow-first-frame](#slow-first-frame) |
| Writing bone matrices directly has no effect | [#bone-matrix](#bone-matrix) |
| Particles completely invisible | [#particles](#particles) |
| Per-instance attributes (color etc.) do nothing or render wrong | [#instance-location](#instance-location) |
| Custom pass draws nothing on frame 1, fine from frame 2 | [#shader-order](#shader-order) |
| Uniforms never take effect | [#uniform-silent](#uniform-silent) |
| GLSL compiles nowhere although it looks correct | [#glsl-chars](#glsl-chars) |
| PipelineSettings changes have no effect | [#settings-timing](#settings-timing) |
| Compile error: `Color` is an ambiguous reference between `System.Drawing.Color` and `Avalonia.Media.Color` | [#color-ambiguity](#color-ambiguity) |
| The 5th light of a type does nothing / ambient parameter ignored | [#limits-ambient](#limits-ambient) |
| After backgrounding or detach/reattach, must the scene be rebuilt? | [#context-restore](#context-restore) |

## Nothing renders

<a id="black-screen"></a>
### Black screen / nothing renders at all

Check the four causes in order of hit rate:

1. **No light added** — the default BlinnPhong pipeline renders pure black without lights. Add a `DirectionalLight` and set its `RotationDegrees` (a directional light's direction comes from its rotation; rotating it away from your model is just as dark).
2. **Camera not aimed** — the scene needs a camera; `Aura3DView.MainCamera` is the default one. Make sure it isn't behind the objects or inside them; use `camera.LookAt(...)` or `FitToBoundingBox(model.BoundingBox, padding: 0.5f)`.
3. **`CreateRenderPipeline` assigned too late** — it must be set before the control's GL initialization (i.e. before the control loads). Assigning it after `SceneInitialized` is too late and the chosen pipeline never applies.
4. **`BlendMode.Masked` clipping translucent pixels away** — under Masked, pixels with alpha ≤ `AlphaCutoff` (default 0.5) are `discard`ed, so translucent textures show big holes or disappear entirely. Use `BlendMode.Translucent` for actual transparency.

Light and camera details: [Lighting and Shadows](./lighting.md), [Cameras and View Control](./camera.md); pipeline assignment timing: [Choosing and Configuring Pipelines](./pipelines.md).

<a id="z-fighting"></a>
### Surfaces flicker, overlapping faces can't be ordered (Z-Fighting)

- **Likely cause**: the depth buffer lacks precision, so two nearly-coincident faces can't be ordered.
- **What to do**: set `PipelineSettings.DepthFormat` to `DepthComponent32f` (it is the default; if you dropped it to 16/24 to save memory, raise it back). It cannot be changed after the pipeline is created.
- **Read more**: [Choosing and Configuring Pipelines](./pipelines.md).

<a id="culling"></a>
### Distant or animated models wrongly culled, vanishing into thin air

- **Likely cause**: frustum culling uses the **T-Pose bounding box** computed from static vertex data; when skinning animation (walking, jumping…) pushes the model visibly outside it, meshes still on screen get culled.
- **What to do**: widen the box with `model.BoundingBoxPadding = new Vector3(2f)`, or fully cover the animation range with `model.CustomBoundingBox = new BoundingBox(min, max)`. To verify, temporarily set `Settings.EnableFrustumCulling = false`, or look at the boxes directly via `Settings.Debug.ShowBoundingBox = true`. Static models need no adjustment.
- **Read more**: [Animation System](./animation.md).

<a id="macos"></a>
### macOS viewport stays blank

- **Likely cause**: the host is running a native rendering mode other than OpenGL.
- **What to do**: pin `AvaloniaNativeRenderingMode.OpenGl` explicitly in `AppBuilder`. iOS needs no such special-casing (it has its own ANGLE path).
- **Read more**: [Platforms and Render Backends](./platform-render-backends.md).

## Platform-specific silent failures

<a id="ios-blend"></a>
### Black screen on iOS once blending is on (float render targets)

- **Likely cause**: ANGLE's Metal backend on iOS does not provide `GL_EXT_float_blend`; drawing with blending enabled into a **32-bit float color attachment** (e.g. `Rgba32f`) is treated as `GL_INVALID_OPERATION` and the entire draw is dropped silently — a black image, not an error.
- **What to do**: use `Rgba16f` for HDR render targets (all built-in pipelines do); never allocate an `Rgba32f` color attachment and then enable blending in a custom pass.
- **Read more**: [Platforms and Render Backends → the GLES 3.0 subset](./platform-render-backends.md).

<a id="browser-drops"></a>
### Something vanishes silently in the browser but works on desktop

- **Likely cause**: WebGL2 validates strictly and drops the entire offending draw **silently**. Four triggers: (1) an enabled draw buffer with no fragment output actually written (a depth-only FBO without `glDrawBuffers([GL_NONE])`; a fragment shader declaring `out` that no `#ifdef` branch ever writes); (2) `UseShader`/`UseShader_Internal` written in the wrong order (see [#shader-order](#shader-order)); (3) a color attachment using an RGB-family internal format (`RGB8`/`RGB16F`/`RGB32F` are not color-renderable — use the RGBA family); (4) calling an entry point WebGL2 does not have (e.g. `glGetTexLevelParameteriv`).
- **What to do**: check a custom pass against these four GLES 3.0-subset rules; built-in pipelines are unaffected.
- **Read more**: [Platforms and Render Backends → the GLES 3.0 subset](./platform-render-backends.md).

<a id="browser-crash"></a>
### Browser crashes on the first frame / aborts at startup

- **Likely cause**: .NET 10 Release trimming removed WASM interpreter-to-native trampolines (the log reaches `[aura3d-webgl] output ...`, then `aot-runtime-wasm.c:188 <disabled>` and `exit(1)`), or the linked-icall table disagrees with the loaded `System.Private.CoreLib` (`Your mono runtime and class libraries are out of sync` / `function signature mismatch`). A `DllNotFoundException: libSkiaSharp` right at startup means the wasm module was never natively linked.
- **What to do**: in the app `.csproj` under Release set all three together: `PublishTrimmed=false` + `RunAOTCompilation=false` + `WasmLinkIcalls=false` (one or two of the three still fails); after changing them, publish to fresh `bin`/`obj`/publish directories, replace the remote static site wholesale, and refresh the `dotnet.js` caches.
- **Read more**: [Platforms and Render Backends → .NET 10 Release required settings](./platform-render-backends.md#browser-net10-release-config).

<a id="slow-first-frame"></a>
### First frame very slow on iOS / browser — expected

- **Likely cause**: on iOS, ANGLE translates GLSL to MSL and compiles every variant at runtime with no on-disk cache; in the browser the Mono interpreter executes host code slowly. Measured: roughly 5.6–5.9 s for a PBR scene with cascaded shadows and 0.4–0.7 s for simple scenes on iOS; heavy browser scenes can take minutes to reach frame 1.
- **What to do**: wait for frame 1 — it is normal. Do not enable `RunAOTCompilation` alone as a performance fix (it regresses to the [#browser-crash](#browser-crash) crash).
- **Read more**: [Platforms and Render Backends](./platform-render-backends.md).

## Animation, instancing and particles

<a id="bone-matrix"></a>
### Writing bone matrices directly has no effect

- **Likely cause**: bone matrices are recomputed every frame during the **animation sampling stage** by `IAnimationSampler.Update()`; values you write in `SceneUpdated` get overwritten.
- **What to do**: for procedural bone control, implement a custom `IAnimationSampler`, or make sure the override happens after animation sampling.
- **Read more**: [Animation System → manual bone manipulation](./animation.md).

<a id="instance-location"></a>
### Per-instance attributes (color etc.) do nothing or render wrong

- **Likely cause**: the `layout(location = N)` in the vertex shader doesn't match the numeric value of the `BuildInVertexAttribute` enum (the location *is* the enum value), or the `#ifdef INSTANCED_MESH` branch forgot to multiply the per-instance `modelMatrix` itself — overriding the vertex stage means taking over the instance transform.
- **What to do**: use the location convention table in [Built-in Passes and Shader Macros](./reference-shaders.md); `InstancedMesh.SetAttributeEnabled("InstanceNormalTransform", false)` drops the unneeded instance normal matrix to save bandwidth.
- **Read more**: [Instanced Rendering](./instanced-rendering.md).

<a id="particles"></a>
### Particles completely invisible

- **Likely cause**: `Play()` was never called; or auto-rendering is off (`AutoRequestNextFrameRendering = false`) without per-frame `RequestNextFrameRendering()`.
- **What to do**: call `ps.Play()` after configuration; for "particles stuck at origin / not moving / mesh mode all black / flipbook not animating" etc., walk the per-parameter checklist.
- **Read more**: [Particle System → troubleshooting table](./particle-system.md).

## Shaders and custom passes

<a id="shader-order"></a>
### Custom pass draws nothing on frame 1, fine from frame 2

- **Likely cause**: `UseShader_Internal()` was called *before* `UseShader(...)`/`AddDefines(...)` — the frame bound the previous frame's variant (on frame 1, the empty-macro one).
- **What to do**: always write `UseShader` → `UseShader_Internal` → set uniforms → draw; note also that macro order affects the cache key (joined with `;`), so declare all macros in one `UseShader` call.
- **Read more**: [Custom Render Pipelines](./custom-pipeline.md).

<a id="uniform-silent"></a>
### Uniforms never take effect

- **Likely cause**: the `Uniform*` methods **silently skip** — no error — when no shader is active (`CurrentShader` is null) or the uniform name doesn't exist in the program. A pass that doesn't iterate meshes (pure `RenderQuad`/`RenderCube`) must call `UseShader_Internal()` manually.
- **What to do**: first check the activation order ([#shader-order](#shader-order)), then verify the exact uniform name in the GLSL; the engine always provides `modelMatrix`/`viewMatrix`/`projectionMatrix`/`cameraPosition`, and material parameters bind to same-named uniforms.
- **Read more**: [Custom Render Pipelines](./custom-pipeline.md), [Custom Materials and Shaders](./custom-material.md).

<a id="glsl-chars"></a>
### GLSL compiles nowhere although it looks correct

- **Likely cause**: the shader source contains **Chinese comments or other full-width characters**.
- **What to do**: keep GLSL comments ASCII-only; write the explanations on the C# side.
- **Read more**: [Custom Materials and Shaders](./custom-material.md).

## Configuration and timing

<a id="settings-timing"></a>
### PipelineSettings changes have no effect

- **Likely cause**: some settings are only read **before the pipeline is created** and are ignored afterwards: `DepthFormat`, `DirectionalLightLimit`/`PointLightLimit`/`SpotLightLimit`, `CsmCascadeCount`, `CsmShadowMapResolution`. The rest (`CsmSplitLambda`, `ToneMappingExposure`, `BrightnessClamp`, `AmbientIntensity`, `EnableFxaa`, `EnableFrustumCulling`, `Debug.*`) can change any time and take effect on the next frame.
- **What to do**: set pre-creation values in XAML `PipelineSettings` or at control construction; for live tuning use `view.Scene.RenderPipeline.Settings.X = ...`.
- **Read more**: [Choosing and Configuring Pipelines](./pipelines.md).

<a id="color-ambiguity"></a>
### Compile error: `Color` is an ambiguous reference

- **Likely cause**: Aura3D color properties (`Material.BaseColor`, `Light.LightColor`, `Texture.CreateFromColor(...)` etc.) take **`System.Drawing.Color`**, while an Avalonia project normally uses `Avalonia.Media.Color`; with both namespaces imported, a bare `Color` collides.
- **What to do**: write the fully qualified `System.Drawing.Color.White`, or alias it: `using DrawingColor = System.Drawing.Color;`. To convert an Avalonia color: `System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)`.
- **Read more**: [Quick Start](./quickstart.md).

<a id="limits-ambient"></a>
### The 5th light of a type does nothing / ambient parameter ignored

- **Likely cause**: each light type defaults to a limit of 4; lights beyond the limit produce neither illumination nor shadows. Limits range 1..10 and must be raised before pipeline creation. Also, `AmbientIntensity` applies to BlinnPhong/CelShading only — **the PBR pipeline uses IBL ambient light and ignores this parameter**.
- **What to do**: raise the matching `*LightLimit` before the pipeline is created; to brighten a PBR scene's ambience, set up an HDR environment.
- **Read more**: [Lighting and Shadows](./lighting.md), [Choosing and Configuring Pipelines](./pipelines.md).

## Context and lifecycle

<a id="context-restore"></a>
### After backgrounding or detach/reattach, must the scene be rebuilt?

- **No.** When the context is lost, `ContextLost` fires; when a new context is ready, `ContextRestored` fires and every GPU resource is rebuilt on demand — the picture comes back by itself. The scene, nodes and materials are preserved, so **there is nothing to rebuild**, and `SceneInitialized` does not fire a second time — the scene is built once, on first initialization.
- **Note**: don't "guard against loss" by rebuilding the scene from `SceneInitialized`; to return VRAM explicitly use `Aura3DView.ReleaseGpuResources()`, to end the current scene use `DestroyScene()`.
- **Read more**: [GPU Resource Lifecycle](./gpu-resource-lifecycle.md).
