---
section: advanced
order: 3
---

# Choosing and Configuring Pipelines

The render pipeline determines the scene's visual style. This page is for **using pipelines**: pick a built-in one and tune its output with `PipelineSettings`. Writing your own pipeline: [Custom Render Pipelines](./custom-pipeline.md).

## What You Can Do

Aura3D ships a set of built-in pipelines covering common needs from realistic to stylized. Choosing a pipeline comes down to two things:

1. **Pick one** — specify it with `x:TypeArguments` in XAML, or assign a factory delegate to `CreateRenderPipeline` in code.
2. **Configure it** — use `PipelineSettings` to adjust depth precision, light counts, exposure, cascaded shadows, anti-aliasing, and debug visualization.

## Built-in Pipelines and How to Choose

| Pipeline | Style / purpose | Package | In Core? |
|---|---|---|---|
| `BlinnPhongPipeline` | Realistic forward rendering, Blinn-Phong model (**default**) | `Aura3D.Avalonia` | Yes, no extra install |
| `NoLightPipeline` | Unlit, outputs raw material color; debugging or stylized | `Aura3D.Avalonia` | Yes |
| `PointCloudPipeline` | Point-cloud scenes, built-in point size and color attributes | `Aura3D.Avalonia` | Yes |
| `PBRDeferredPipeline` | Physically based Metallic-Roughness workflow, deferred architecture | `Aura3D.Pipeline.PBR` | Extra install |
| `PBRForwardPipeline` | The same PBR workflow, forward architecture | `Aura3D.Pipeline.PBRForward` | Extra install |
| `CelShadingPipeline` | Cel / Toon non-photorealistic shading | `Aura3D.Pipeline.CelShading` | Extra install |

BlinnPhong is the default pipeline. It supports directional, point, and spot lights (max 4 per type), shadows, skeletal animation, and transparent/translucent materials — just use `Aura3DView` and you get it, no configuration.

Install the extension pipelines (as needed):

```shell
# PBR deferred pipeline
dotnet add package Aura3D.Pipeline.PBR

# PBR forward pipeline
dotnet add package Aura3D.Pipeline.PBRForward

# Cel shading pipeline
dotnet add package Aura3D.Pipeline.CelShading
```

> [!NOTE]
> `BlinnPhong`, `NoLight`, and `PointCloud` live in `Aura3D.Core` and come with `Aura3D.Avalonia` — **no** extra install command.

### How to Select a Pipeline

Either of the two ways works; the result is the same.

**Way 1 — XAML `x:TypeArguments`:**

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:acr="clr-namespace:Aura3D.Core.Renderers;assembly=Aura3D.Core"
    ...>
    <a:Aura3DView x:TypeArguments="acr:NoLightPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

Core built-ins use `acr:` (`Aura3D.Core.Renderers`); swap in each extension pipeline's own namespace:

```xaml
<!-- PBR: xmlns:pbr="clr-namespace:Aura3D.Pipeline.PBR;assembly=Aura3D.Pipeline.PBR" -->
<a:Aura3DView x:TypeArguments="pbr:PBRDeferredPipeline" ... />

<!-- PBR forward: xmlns:pbrf="clr-namespace:Aura3D.Pipeline.PBRForward;assembly=Aura3D.Pipeline.PBRForward" -->
<a:Aura3DView x:TypeArguments="pbrf:PBRForwardPipeline" ... />

<!-- Cel: xmlns:cel="clr-namespace:Aura3D.Pipeline.CelShading;assembly=Aura3D.Pipeline.CelShading" -->
<a:Aura3DView x:TypeArguments="cel:CelShadingPipeline" ... />

<!-- Point cloud (Core built-in): xmlns:core="clr-namespace:Aura3D.Core.Renderers;assembly=Aura3D.Core" -->
<a:Aura3DView x:TypeArguments="core:PointCloudPipeline" ... />
```

**Way 2 — code `CreateRenderPipeline`:**

```csharp
view.CreateRenderPipeline = scene => new NoLightPipeline(scene);
// or
view.CreateRenderPipeline = scene => new PointCloudPipeline(scene);
```

> [!WARNING]
> `CreateRenderPipeline` **must be assigned before GL initialization** (before the control loads). By the time `SceneInitialized` fires, the pipeline is already built — setting it then is too late.

> [!TIP]
> To switch pipelines at runtime, keep the delegates in a single table. The Gallery pipelines demo does exactly this — see [PipelinesDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Pipelines/PipelinesDemo.axaml.cs) and [PipelineCatalog.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/PipelineCatalog.cs), which switch live among BlinnPhong / NoLight / PBR Deferred / PBR Forward / Cel Shading so you can see which pipeline reads which texture and which ignores it entirely.

## Configuring a Pipeline: PipelineSettings

`PipelineSettings` controls pipeline behavior and image quality. The key point is that it **splits into two kinds**: one kind (depth format, light limits, CSM cascade count and resolution) must be set before the pipeline is created and won't take effect if changed afterward; the other kind (exposure, ambient light, FXAA toggle, debug visualization, etc.) can be changed any time and shows up on the next frame.

### Configuration

**XAML** (declared with the control before it loads, satisfying the "before creation" requirement):

```xml
<Window xmlns:core="clr-namespace:Aura3D.Core.Renderers;assembly=Aura3D.Core" ...>
    <a:Aura3DView x:TypeArguments="cel:CelShadingPipeline">
        <a:Aura3DView.PipelineSettings>
            <core:PipelineSettings DepthFormat="DepthComponent32f"
                                   DirectionalLightLimit="2"
                                   ToneMappingExposure="1.2f" />
        </a:Aura3DView.PipelineSettings>
    </a:Aura3DView>
</Window>
```

**Code** — set one-time parameters before creation:

```csharp
var view = new Aura3DView<CelShadingPipeline>
{
    PipelineSettings = new PipelineSettings
    {
        DepthFormat = TextureFormat.DepthComponent32f,
        DirectionalLightLimit = 2,
    }
};
```

**Change at runtime** (takes effect next frame):

```csharp
view.Scene.RenderPipeline.Settings.ToneMappingExposure = 1.3f;
view.Scene.RenderPipeline.Settings.EnableFxaa = false;
```

### Depth Format (DepthFormat)

Controls front/back occlusion precision — think of it as "how finely divided the ruler is" when measuring depth.

| Value | Precision | When to use |
|---|---|---|
| `DepthComponent16` | 16-bit | Normal scenes |
| `DepthComponent24` | 24-bit | Larger scenes, or when finer depth precision is needed |
| `DepthComponent32f` | 32-bit float (default) | Very large scenes (cities, terrain) where 16-bit isn't enough |

> [!TIP]
> If distant objects flicker or two surfaces appear to overlap without a clear front (the artifact known as Z-Fighting), precision is too low — switch to `DepthComponent32f`.

### Light Limits

Cap how many lights take effect at once; lights past the limit produce neither illumination nor shadows. Each defaults to `4`, valid range `1..10`: lower for performance, higher to support more lights.

| Parameter | Light type |
|---|---|
| `DirectionalLightLimit` | Directional lights — sun, global parallel light |
| `PointLightLimit` | Point lights — bulbs, candles, omnidirectional sources |
| `SpotLightLimit` | Spot lights — flashlights, stage spots, cone sources |

> How to place and configure lights and their shadows is covered in [Lighting and Shadows](./lighting.md).

### Tone Mapping & Brightness

Tone mapping compresses HDR colors into the range a display can show; these two parameters set the overall brightness feel.

| Parameter | Effect | Default |
|---|---|---|
| `ToneMappingExposure` | Global brightness, like exposure compensation; higher = brighter | `0.7` |
| `BrightnessClamp` | Brightness ceiling; values above are cut off to prevent blown-out highlights | `4.0` |

> [!TIP]
> Scene too dark → raise `ToneMappingExposure`. Highlights blown to white → raise `BrightnessClamp`.

### Ambient Intensity (AmbientIntensity)

Areas with no direct light aren't pitch black — ambient light simulates the subtle scattered light in a scene. `0` = fully black shadows, `0.1` (default) = slight lift, `0.5`+ = noticeably bright shadows and a stylized look.

> [!NOTE]
> The PBR pipeline uses physically based IBL ambient lighting and is **not** affected by `AmbientIntensity` (it has its own IBL ambient strength).

### Cascaded Shadow Maps (CSM)

Directional-light shadows alias at distance; CSM splits the view frustum into cascades, each with its own shadow map. Only pipelines with `SupportsCSM = true` (e.g., BlinnPhong) use it. Designate which directional light uses CSM via `Scene.MainDirectionalLight`; the others fall back to a single shadow map:

```csharp
view.Scene.MainDirectionalLight = dl;  // this directional light uses CSM
```

| Parameter | Effect | Default |
|---|---|---|
| `CsmCascadeCount` | Number of cascades; set to 1 to fall back to a single shadow map (`1..4`) | `3` |
| `CsmSplitLambda` | PSSM split parameter, 0=uniform, 1=logarithmic (`0..1`) | `0.5` |
| `CsmShadowMapResolution` | Shadow map resolution per cascade (must be positive) | `1024` |

> Set `CsmCascadeCount` and `CsmShadowMapResolution` before pipeline creation; `CsmSplitLambda` may change at runtime. How CSM works and tuning guidance are in [Lighting and Shadows](./lighting.md#csm).

### Feature Toggles EnableFxaa / EnableFrustumCulling

| Parameter | Effect | Default |
|---|---|---|
| `EnableFxaa` | FXAA anti-aliasing — smooths jagged edges | `true` |
| `EnableFrustumCulling` | Only render objects inside the camera's view; invisible ones are skipped | `true` |

> [!TIP]
> Disable `EnableFxaa` to save a little overhead when performance is tight. Leave `EnableFrustumCulling` on — it significantly speeds up scenes with many objects. See [Frustum Culling](#frustum-culling).

### Debug Visualization (Debug)

`PipelineSettings.Debug` (a `DebugSettings`) controls built-in debug drawing to make scene structure visible during development; every property can be changed at runtime:

```csharp
var debug = view.Scene.RenderPipeline.Settings.Debug;
debug.Enable = true;                // master switch
debug.ShowBoundingBox = true;       // bounding boxes for all meshes
debug.ShowDirectionalLight = true;  // directional light direction lines
debug.ShowPointLight = true;        // point light range spheres
debug.ShowSpotLight = true;         // spot light cones
debug.ShowCamera = true;            // camera frustums
debug.ShowBone = true;              // bone hierarchy
```

> [!WARNING]
> Debug drawing adds performance overhead; enable it only during development. `Debug` cannot be `null`; all intensity/tone-mapping floats must be finite and non-negative, otherwise assignment throws `ArgumentOutOfRangeException`.

### Which Settings Need to Be Set When

This is the easiest place to write wrong code — follow this table and you're good.

| Setting | Must set before creation? | Applies to |
|---|---|---|
| `DepthFormat` | ✅ Yes — won't take effect later | All pipelines |
| `DirectionalLightLimit` | ✅ Yes | BlinnPhong / PBR / CelShading |
| `PointLightLimit` | ✅ Yes | BlinnPhong / PBR / CelShading |
| `SpotLightLimit` | ✅ Yes | BlinnPhong / PBR / CelShading |
| `CsmCascadeCount` | ✅ Yes | BlinnPhong |
| `CsmShadowMapResolution` | ✅ Yes | BlinnPhong |
| `CsmSplitLambda` | ❌ Anytime | BlinnPhong |
| `ToneMappingExposure` | ❌ Anytime | BlinnPhong / PBR / CelShading |
| `BrightnessClamp` | ❌ Anytime | BlinnPhong / PBR / CelShading |
| `AmbientIntensity` | ❌ Anytime | BlinnPhong / CelShading |
| `EnableFxaa` | ❌ Anytime | All pipelines |
| `EnableFrustumCulling` | ❌ Anytime | All pipelines |
| `Debug.*` | ❌ Anytime | All pipelines |

> [!NOTE]
> The NoLight pipeline skips lighting and tone mapping, so light limits, exposure, and ambient parameters have no effect on it.

### Backward Compatibility

Existing properties on `RenderPipeline` (such as `EnableFrustumCulling`, `DirectionalLightLimit`) still work and internally forward to `Settings`:

```csharp
// These two lines are equivalent
pipeline.EnableFrustumCulling = false;
pipeline.Settings.EnableFrustumCulling = false;
```

## Frustum Culling

Frustum culling makes the renderer only draw objects within the camera's view, skipping everything outside to cut draw cost. It's controlled by `PipelineSettings.EnableFrustumCulling` and is **on by default**. When on, the pipeline computes a per-camera list of visible meshes each frame and culled methods like `RenderVisibleMeshesInCamera` only iterate that list; when off, every mesh in the scene is drawn (turn it off only for very few objects or when you need to force a full traversal). With multiple cameras, culling is computed per camera.

## Multi-Camera Rendering

A single scene can render multiple camera views at once — for split-screen or minimaps. Every `Camera` node in the scene is automatically discovered and rendered one by one, and each Pass registered as `RenderPassGroup.EveryCamera` runs once per camera (the Pass grouping mechanism is in [Custom Render Pipelines](./custom-pipeline.md)).

```csharp
// Create a second camera in SceneInitialized
var secondCamera = new Camera
{
    Position = new Vector3(10, 5, 0),
    IsRenderBackground = false  // the second view doesn't re-render the skybox
};
secondCamera.LookAt(Vector3.Zero);
scene.AddNode(secondCamera);
```

### Render to Texture

Use `ControlRenderTarget` to render a camera's view into a texture for minimaps, surveillance views, and the like:

```csharp
// Create an offscreen target and attach it to the camera
var renderTarget = new ControlRenderTarget(width, height);
secondCamera.RenderTarget = renderTarget;

// After rendering, this target holds that camera's view
// Read its texture in SceneUpdated and feed it as another material's input
```

More camera usage (projection types, `FitToBoundingBox`, controllers) is in [Cameras and View Control](./camera.md).

## GPU Resource Auto-Management (Overview)

When you add a mesh, material, texture, or model to the scene with `view.AddNode(...)`, the pipeline takes over the GPU-side state of those resources: uploading them on first use, re-syncing after content changes, and reclaiming them periodically once they're no longer referenced — day-to-day use needs no manual work, and there is no registration API to call by hand.

Context loss and restoration, releasing and rebuilding VRAM, and the `IGpuState` contract are covered in depth in [GPU Resource Lifecycle](./gpu-resource-lifecycle.md) — read that page when something goes wrong or you need precise VRAM control.

## PBR Material Parameters (Usage Example)

Once you pick the PBR pipeline, it reads material channel textures via the Metallic-Roughness workflow. Here's an example of "how to feed the parameters"; material channels themselves and the custom-shader mechanism are left to [Custom Materials and Shaders](./custom-material.md).

```csharp
var mesh = new Mesh();
mesh.Geometry = new SphereGeometry();
mesh.Material = new Material();

// Base color (BaseColor is an extension property — new first, then assign; don't put it in an object initializer)
mesh.Material.BaseColor = Texture.CreateFromColor(Color.FromArgb(255, 200, 50, 50));

// Normal map
mesh.Material.SetTexture("Normal",
    Texture.CreateFromColor(Color.FromArgb(128, 128, 255)));

// Metallic/Roughness map: R channel = metallic, G channel = roughness
mesh.Material.SetTexture("MetallicRoughness",
    Texture.CreateFromColor(Color.FromArgb(200, 100, 0)));

view.AddNode(mesh);
```

> [!NOTE]
> In `MetallicRoughness`, R stores metallic and G stores roughness — the PBR channel-packing convention. The Cel Shading pipeline works like the default one — load models, set lights, and the rendering style automatically becomes toon-shaded.

## Common Pitfalls

- **`CreateRenderPipeline` set too late**: it must be assigned before GL initialization (before the control loads); putting it inside the `SceneInitialized` callback is already too late.
- **Treating before-creation settings as runtime settings**: `DepthFormat`, the three `*LightLimit`s, `CsmCascadeCount`, and `CsmShadowMapResolution` won't take effect if changed after the pipeline is built — you must rebuild it (reload the control). Match your code to [the table above](#which-settings-need-to-be-set-when).
- **Adjusting `AmbientIntensity` in PBR does nothing**: PBR uses IBL ambient light and ignores this parameter — that's expected.
- **Configuring lights/exposure for the NoLight pipeline**: NoLight skips lighting and tone mapping, so those settings have no effect.
- **Non-negative validation**: passing a negative, `NaN`, or `Infinity` for intensity/tone-mapping floats, or `null` for `Debug`, throws on assignment.
- **Wrong XAML `x:TypeArguments` namespace**: Core built-ins are in `Aura3D.Core.Renderers`; PBR / Cel must `clr-namespace` their own assemblies, and the matching NuGet package must be installed first.

## Runnable Example

- Gallery pipelines demo (one scene across BlinnPhong / NoLight / PBR Deferred / PBR Forward / Cel Shading, showing which channel each reads): [PipelinesDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Pipelines/PipelinesDemo.axaml.cs)
- Pipeline-kind-to-type mapping table: [PipelineCatalog.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/PipelineCatalog.cs)

## Next Steps

- Writing your own pipeline or `RenderPass`: [Custom Render Pipelines](./custom-pipeline.md)
- Material and texture channel mechanisms: [Custom Materials and Shaders](./custom-material.md)
- VRAM and context recovery: [GPU Resource Lifecycle](./gpu-resource-lifecycle.md)
