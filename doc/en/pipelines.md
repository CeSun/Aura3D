# Rendering Pipelines

The rendering pipeline determines the visual style of the scene. Aura3D provides multiple built-in pipelines and supports full customization.

## Built-in Pipelines

### BlinnPhong Pipeline (Default)

A realistic forward rendering pipeline using the Blinn-Phong lighting model. No extra configuration needed — just use `Aura3DView`.

Features:
- Directional, point, and spot lights (max 4 per type)
- Shadows
- Skeletal animation
- Transparent / translucent materials

### NoLight Pipeline

An unlit pipeline that outputs raw material colors. Useful for debugging or stylized rendering.

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

Or specify in code:

```csharp
view.CreateRenderPipeline = scene => new NoLightPipeline(scene);
```

## PBR Deferred Pipeline

Physically Based Rendering using the Metallic-Roughness workflow with a deferred rendering architecture.

### Installation

```shell
dotnet add package Aura3D.Pipeline.PBR
```

### Usage

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:pbr="clr-namespace:Aura3D.Pipeline.PBR;assembly=Aura3D.Pipeline.PBR"
    ...>
    <a:Aura3DView x:TypeArguments="pbr:PBRDeferredPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

### Material Configuration

The PBR pipeline uses PBR material parameters:

```csharp
var mesh = new Mesh();
mesh.Geometry = new SphereGeometry();
mesh.Material = new Material();

// Base color
mesh.Material.BaseColor = Texture.CreateFromColor(Color.FromArgb(255, 200, 50, 50));

// Normal map
mesh.Material.SetTexture("Normal",
    Texture.CreateFromColor(Color.FromArgb(128, 128, 255)));

// Metallic/Roughness map: R channel = metallic, G channel = roughness
mesh.Material.SetTexture("MetallicRoughness",
    Texture.CreateFromColor(Color.FromArgb(200, 100, 0)));

view.AddNode(mesh);
```

## Cel Shading Pipeline

Non-photorealistic rendering in the Cel Shading / Toon Shading style.

### Installation

```shell
dotnet add package Aura3D.Pipeline.CelShading
```

### Usage

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:cel="clr-namespace:Aura3D.Pipeline.CelShading;assembly=Aura3D.Pipeline.CelShading"
    ...>
    <a:Aura3DView x:TypeArguments="cel:CelShadingPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

The cel shading pipeline works the same as the default — load models, set up lights, and the rendering style automatically becomes toon-shaded.

## Point Cloud Pipeline

The built-in `PointCloudPipeline` is designed for point cloud scenes, ready to use without custom shaders:

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:core="clr-namespace:Aura3D.Core.Renderers;assembly=Aura3D.Core"
    ...>
    <a:Aura3DView x:TypeArguments="core:PointCloudPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

Or set in code:

```csharp
view.CreateRenderPipeline = scene => new PointCloudPipeline(scene);
```

The point cloud pipeline includes built-in point size control and color attribute support. The render pass chain is: BackgroundPass → PointCloudPass → GammaCorrectionPass → FxaaPass → DebugDrawPass.

## Custom Rendering Pipelines

Aura3D's pipeline consists of two components: **RenderPipeline** and **RenderPass**. Custom pipelines require implementing both classes. Developers don't need to deal with VAO/VBO details, but basic rendering knowledge is still required.

### Architecture Overview

```
RenderPipeline
  ├── Register RenderTargets (framebuffer + texture attachments)
  ├── Register RenderPasses (render steps, each with an output target)
  └── Dispatch by RenderPassGroup
       ├── Once — Execute once globally (e.g., ShadowMap)
       └── EveryCamera — Execute per camera (e.g., main rendering)
```

### RenderPipeline

`RenderPipeline` is responsible for registering RenderPasses and RenderTargets.

```csharp
public class NoLightPipeline : RenderPipeline
{
    public NoLightPipeline(Scene scene) : base(scene)
    {
        var baseRenderTarget = RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(Settings.DepthFormat);

        var gammaOutput = RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        var noLightPass = new NoLightPass(this);

        // Register RenderPasses (executed in registration order)
        RegisterRenderPass(
            new BackgroundPass(this).SetOutput(baseRenderTarget),
            RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            noLightPass.SetOutput(baseRenderTarget),
            RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            new GammaCorrectionPass(this, baseRenderTarget.GetTexture("Color"))
                .SetOutput(gammaOutput),
            RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            new FxaaPass(this, gammaOutput.GetTexture("Color"))
                .SetOutput(CameraOutput),
            RenderPassGroup.EveryCamera);
    }
}
```

**Key APIs:**

| Method | Description |
|---|---|
| `RegisterRenderPass(pass, group)` | Register a render step; `group` determines execution timing |
| `RegisterRenderTarget(name)` | Register a framebuffer, returns a configurator |
| `AddTexture(name, format)` | Add a color attachment to the RenderTarget |
| `SetDepthTexture(format)` | Add a depth attachment to the RenderTarget |

**RenderPassGroup enum:**
- `EveryCamera` — Executed once per camera (used by most passes)
- `Once` — Executed once globally (e.g., ShadowMap rendering)

### RenderPass

`RenderPass` is a single shader-driven render step. Generally one Shader (with variants) maps to one RenderPass.

```csharp
public class NoLightPass : RenderPass
{
    public NoLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        // Specify shader source
        this.FragmentShader = ShaderResource.NoLightFrag;
        this.VertexShader = ShaderResource.NoLightVert;
    }

    public override void Render(Camera camera)
    {
        // Render opaque non-skinned meshes
        UseShader();
        RenderVisibleMeshesInCamera(
            mesh => !mesh.IsSkinnedMesh
                 && (mesh.Material == null
                     || mesh.Material.BlendMode == BlendMode.Opaque),
            camera.View,
            camera.Projection);

        // Render opaque skinned meshes (with SKINNED_MESH macro variant)
        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(
            mesh => mesh.IsSkinnedMesh
                 && (mesh.Material == null
                     || mesh.Material.BlendMode == BlendMode.Opaque),
            camera.View,
            camera.Projection);
    }
}
```

> This is a simplified example. The actual built-in `NoLightPipeline` iterates all meshes and filters manually. For new pipelines, prefer the culled versions. `mesh.IsSkinnedMesh` / `mesh.IsStaticMesh` are properties on `Mesh`, replacing manual skeleton logic.

**Key APIs:**

| Method | Description |
|---|---|
| `UseShader(params string[] defines)` | Set shader macro defines (replace mode), see [Shader Macro System](#shader-macro-system) |
| `AddDefines(params string[] defines)` | Append macro defines (append mode), call after `UseShader` |
| `RenderVisibleMeshesInCamera(filter, view, proj)` | Render meshes that pass frustum culling |

**Mesh Key Properties:**

| Property | Description |
|---|---|
| `mesh.IsStaticMesh` | Non-skinned mesh (returns `!IsSkinnedMesh`) |
| `mesh.IsSkinnedMesh` | Mesh bound to a skeleton (returns `Model != null && Skeleton != null`) |

### Per-Mesh Parameter Passing

Override `RenderMesh` to set uniforms before rendering a specific mesh:

```csharp
public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
{
    if (someCondition)
    {
        UniformFloat("someParameter", value);
        UniformVector4("someColor", new Vector4(1, 0, 0, 1));
    }

    // These base matrices must be set
    UniformMatrix4("viewMatrix", view);
    UniformMatrix4("projectionMatrix", projection);

    base.RenderMesh(mesh, view, projection);
}
```

### Shader Macro System

Aura3D's shader variants are implemented through three cooperating methods. Understanding their relationship is key to custom pipelines.

#### Division of Labor

| Method | Role | GPU Operation |
|---|---|---|
| `UseShader(params string[] defines)` | **Replace** the defines list | None |
| `AddDefines(params string[] defines)` | **Append** to the existing defines list | None |
| `UseShader_Internal` | Read defines, compile/cache/activate shader | `gl.UseProgram` |

`UseShader` and `AddDefines` are **declarative** — they only record intent, never touching the GPU. Actual compilation and binding happens in `UseShader_Internal`, which is called automatically by rendering methods like `RenderVisibleMeshesInCamera` before each mesh.

#### Workflow

Execution order in a typical Pass:

```
1. UseShader("SKINNED_MESH")       → defines = ["SKINNED_MESH"]
2. RenderVisibleMeshesInCamera(...)
   ├─ for each mesh:
   │   UseShader_Internal(mesh)    → reads defines = ["SKINNED_MESH"]
   │      cache key = "SKINNED_MESH"
   │      hit → gl.UseProgram      (miss → compile + cache)
   │   RenderMesh(mesh, ...)       → set uniforms, gl.DrawElements
   │
3. UseShader("SKINNED_MESH", "BLENDMODE_MASKED")
                                   → defines = ["SKINNED_MESH", "BLENDMODE_MASKED"]
4. RenderVisibleMeshesInCamera(...)
   └─ for each mesh:
       UseShader_Internal(mesh)    → reads defines = [...]
          cache key = "SKINNED_MESH;BLENDMODE_MASKED"  (different key, different variant)
```

#### When to Use AddDefines

Use `AddDefines` to append when a group of meshes shares most macros and differs in only a few:

```csharp
// Base variant
UseShader("SKINNED_MESH");
RenderVisibleMeshesInCamera(filter1, camera.View, camera.Projection);

// Append one macro, producing SKINNED_MESH + BLENDMODE_MASKED variant
AddDefines("BLENDMODE_MASKED");
RenderVisibleMeshesInCamera(filter2, camera.View, camera.Projection);
```

#### Two Details of UseShader_Internal

**1. Two-Level Caching**

| Cache Level | Storage Location | When Used |
|---|---|---|
| Pass-level | `RenderPass.Shaders["key"]` | When the material has no custom shader |
| Material-level | `Material.Shaders["key"]` | When the material overrides shader source via `SetShaderSource` |

A given defines combination compiles only once; subsequent frames reuse the cached `glUseProgram`.

**2. Compilation Flow**

1. Join `defines` list with `;` as cache key (e.g., `"SKINNED_MESH;BLENDMODE_MASKED"`)
2. If Material provides custom source → check Material cache; on miss, compile with Material source
3. Otherwise check Pass cache; on miss, compile with Pass's `VertexShader`/`FragmentShader`
4. During compilation, inject `#define SKINNED_MESH\n#define BLENDMODE_MASKED` at `//{{defines}}`
5. On macOS, automatically replace `#version 300 es` with `#version 330 core`
6. Link shader, enumerate all uniform locations, and cache them

> **Note**: Defines order affects the cache key. `UseShader("A").AddDefines("B")` produces key `"A;B"`, and `UseShader("A", "B")` also produces `"A;B"` — they match. But `UseShader("B")` then `AddDefines("A")` produces `"B;A"`, a different variant. Prefer declaring all needed macros at once with `UseShader`.

#### Macro Marker in Shader Source

GLSL source uses `//{{defines}}` as the macro injection point:

```glsl
#version 300 es
precision mediump float;

//{{defines}}   ← Replaced at compile time with #define SKINNED_MESH etc.

layout(location = 0) in vec3 position;

#ifdef INSTANCED_MESH
layout(location = 7) in mat4 modelMatrix;
#endif

#ifndef INSTANCED_MESH
uniform mat4 modelMatrix;
#endif
```

#### Manual UseShader_Internal Calls

`UseShader_Internal` is normally called automatically by mesh rendering methods like `RenderVisibleMeshesInCamera` before each mesh. But if your Pass doesn't iterate over meshes — for example, a post-processing Pass that renders a fullscreen quad — you must **call it manually**.

Standard post-processing Pass flow:

```
UseShader()           → Declare macros (optional)
UseShader_Internal()  → Compile/activate the variant
UniformTexture(...)   → Set input textures and other uniforms
RenderQuad()          → Draw a fullscreen quad
```

Real example — Gamma Correction Pass ([GammaCorrectionPass.cs](../../src/Aura3D.Core/Renderers/Common/GammaCorrectionPass.cs)):

```csharp
public override void Render(Camera camera)
{
    BindOutputRenderTarget(camera);
    var source = GetTexture(inputTexture, camera);

    gl.Disable(EnableCap.DepthTest);
    gl.Disable(EnableCap.Blend);

    UseShader();               // No macros needed, can be omitted
    ClearTextureUnit();         // Reset texture unit counter
    UseShader_Internal();       // ← Manual activation! No Material context, passes null
    UniformTexture("colorTexture", source);
    RenderQuad();               // Draw fullscreen quad, sampling input texture for gamma correction
}
```

FXAA Pass similarly ([FxaaPass.cs](../../src/Aura3D.Core/Renderers/Common/FxaaPass.cs)):

```csharp
UseShader();
ClearTextureUnit();
UseShader_Internal();
UniformTexture("u_texture", rt.GetTexture(inputTextureName));
UniformVector2("u_textureSize", new Vector2(texWidth, texHeight));
RenderQuad();
```

Post-processing with macro variants — PBR IBL Ambient Pass ([IBLAmbientPass.cs](../../src/Aura3D.Pipeline.PBR/IBLAmbientPass.cs)):

```csharp
UseShader("ENBALE_DEFERRED_SHADING");  // Declare macro
UseShader_Internal();                   // Compile variant with macros and activate
ClearTextureUnit();
UniformTexture("gBufferBaseColor", gBufferBaseColor);
UniformTexture("gBufferNormalRoughness", gBufferNormalRoughness);
// ... more uniforms ...
UniformMatrix4("u_viewMatrix", camera.View);
UniformMatrix4("u_projMatrix", camera.Projection);
RenderQuad();
```

> **Critical rule**: `UseShader` / `AddDefines` must be called **before** `UseShader_Internal`. `UseShader_Internal` reads the current defines list to decide which variant to activate; modifying defines afterward does not affect the already-active shader.

`RenderQuad()` and `RenderCube()` are built-in methods on `RenderPass` that draw a quad covering NDC space and a unit cube respectively, for post-processing and debugging.

### Custom Material Shaders

Instead of creating an entire RenderPass, you can replace shaders for a specific material's Pass:

```csharp
var material = new Material();

// Set custom shaders for the "LightPass" render step
material.SetShaderSource("LightPass", ShaderType.Vertex, vertexShaderSource);
material.SetShaderSource("LightPass", ShaderType.Fragment, fragmentShaderSource);

// Set shader parameter callback
material.SetShaderPassParametersCallback("LightPass", pass =>
{
    pass.UniformVector4("uColor", new Vector4(1, 0, 0, 1));
});
```

This approach is for local customization — changing how a specific material renders without creating an entire pipeline.

## Pipeline Settings

Use `PipelineSettings` to adjust rendering behavior and visual quality. Some settings must be configured before the pipeline is created, while others can be adjusted on the fly and take effect immediately.

### Configuration

**XAML:**

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

**Code:**

```csharp
// Set before pipeline creation (for depth format, light limits, etc.)
var view = new Aura3DView<CelShadingPipeline>
{
    PipelineSettings = new PipelineSettings
    {
        DepthFormat = TextureFormat.DepthComponent32f,
        DirectionalLightLimit = 2,
    }
};

// Adjust at any time (exposure, ambient light, toggles — takes effect next frame)
view.Scene.RenderPipeline.Settings.ToneMappingExposure = 1.3f;
view.Scene.RenderPipeline.Settings.EnableFxaa = false;
```

### Settings Reference

#### Depth Format (DepthFormat)

Controls the precision of depth testing — how accurately the GPU determines which object is in front of another. Think of it as "how finely divided the ruler is" when measuring depth.

| Value | Precision | When to use |
|---|---|---|
| `DepthComponent16` | 16-bit | Normal scenes |
| `DepthComponent24` | 24-bit | Larger scenes, or when finer depth precision is needed |
| `DepthComponent32f` | 32-bit floating point (default) | Very large scenes (cities, terrain) where 16-bit isn't enough |

> If distant objects flicker or appear to overlap incorrectly (a visual artifact known as Z-Fighting[^1]), switch to `DepthComponent32f`.

#### Light Limits

Cap the number of lights that take effect simultaneously. Lights beyond the limit won't produce illumination or shadows.

| Parameter | Description |
|---|---|
| `DirectionalLightLimit` | Max directional lights (default 4) — for sun-like, parallel light sources |
| `PointLightLimit` | Max point lights (default 4) — for bulbs, candles, omnidirectional sources |
| `SpotLightLimit` | Max spot lights (default 4) — for flashlights, stage spotlights |

> Lower limits improve performance; raise them to support more lights. If you placed 6 lights but only 4 are working, increase the corresponding limit.

#### Tone Mapping & Brightness

Tone mapping[^2] compresses HDR (high dynamic range) colors into the range a display can show. These two parameters control the overall brightness feel of the scene.

| Parameter | Effect | Default |
|---|---|---|
| `ToneMappingExposure` | Global brightness, like a camera's exposure compensation. Higher = brighter | `0.7` |
| `BrightnessClamp` | The brightness ceiling. Values above this are cut off to prevent blown-out highlights | `4.0` |

> If the scene looks too dark, increase `ToneMappingExposure`. If bright areas are washed out in white, increase `BrightnessClamp`.

#### Ambient Light Intensity (AmbientIntensity)

Areas not directly lit by any light source aren't pitch black — ambient light simulates the subtle scattered and reflected light that fills a scene. Higher values brighten shadow areas.

| Range | Visual effect |
|---|---|
| `0` | Shadows are completely black |
| `0.1` (default) | Slightly lifts dark areas |
| `0.5`+ | Noticeably bright shadows; stylistic look |

> Note: The PBR pipeline uses physically-based IBL ambient lighting and is not affected by this parameter.

#### Cascaded Shadow Maps (CSM)

Directional light shadows can show aliasing at long distances. CSM solves this by splitting the view frustum into multiple cascades, each with its own shadow map. Only pipelines with `SupportsCSM = true` (e.g., BlinnPhong) will use CSM.

Specify which directional light uses CSM via `Scene.MainDirectionalLight`; other directional lights fall back to a single shadow map:

```csharp
view.Scene.MainDirectionalLight = dl;  // This directional light uses CSM
```

| Parameter | Effect | Default |
|---|---|---|
| `CsmCascadeCount` | Number of cascades. Set to 1 to fall back to a single shadow map | `3` |
| `CsmSplitLambda` | PSSM split parameter. 0=uniform, 1=logarithmic | `0.5` |
| `CsmShadowMapResolution` | Shadow map resolution per cascade | `1024` |

> `CsmCascadeCount` and `CsmShadowMapResolution` must be set before pipeline creation. `CsmSplitLambda` can be adjusted at runtime.

#### Debug Visualization (DebugSettings)

Control built-in debug drawing via `PipelineSettings.Debug` to visualize scene structure during development:

```csharp
var debug = settings.Debug;
debug.Enable = true;                // Master switch
debug.ShowBoundingBox = true;       // Show bounding boxes for all meshes
debug.ShowDirectionalLight = true;  // Show directional light direction lines
debug.ShowPointLight = true;        // Show point light range spheres
debug.ShowSpotLight = true;         // Show spot light cones
debug.ShowCamera = true;            // Show camera frustums
debug.ShowBone = true;              // Show bone hierarchy
```

> All `DebugSettings` properties can be adjusted at runtime. Debug drawing has additional performance overhead; recommended for development only.

#### Feature Toggles

| Parameter | Effect | Default |
|---|---|---|
| `EnableFxaa` | Enables FXAA anti-aliasing[^3] — smooths jagged edges on objects | `true` |
| `EnableFrustumCulling` | Only render objects inside the camera's view. Invisible objects are automatically skipped | `true` |

> Disable `EnableFxaa` for a small performance gain. `EnableFrustumCulling` is generally best left on — it significantly speeds up scenes with many objects.

### Quick Reference

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

> The NoLight pipeline skips lighting and tone mapping passes, so light limits, exposure, and ambient parameters have no effect on it.

### Backward Compatibility

Existing properties on `RenderPipeline` (such as `EnableFrustumCulling`) still work and internally forward to `Settings`:

```csharp
// These two lines are equivalent
pipeline.EnableFrustumCulling = false;
pipeline.Settings.EnableFrustumCulling = false;
```

[^1]: Z-Fighting: When two surfaces are nearly coplanar, the GPU can't reliably determine which is in front, causing pixels from both surfaces to flicker. Increasing depth buffer precision helps. Reference: https://en.wikipedia.org/wiki/Z-fighting

[^2]: Tone Mapping: The process of mapping HDR color values to the limited range a display can show. The human eye can perceive detail in both dark and bright areas, but displays have a limited brightness range; tone mapping preserves detail in both highlights and shadows. Reference: https://en.wikipedia.org/wiki/Tone_mapping

[^3]: FXAA (Fast Approximate Anti-Aliasing): A lightweight anti-aliasing technique that analyzes the rendered image, detects edges, and applies smoothing to reduce the jagged "staircase" appearance.

## Frustum Culling

Frustum culling makes the renderer only draw objects within the camera's view, skipping everything outside. Controlled by `PipelineSettings.EnableFrustumCulling` (enabled by default). See [Pipeline Settings](#pipeline-settings) for details.

## Pipeline Lifecycle Hooks

`RenderPipeline` and `RenderPass` provide multiple virtual methods for inserting logic at different stages of the rendering process:

### RenderPipeline Hooks

```csharp
public class MyPipeline : RenderPipeline
{
    // Called once after GL initialization (after registering RenderTargets/RenderPasses)
    public override void Setup() { }

    // Before rendering the entire frame (once per frame, before all cameras)
    public override void BeforeRender() { }

    // After rendering the entire frame (once per frame, after all cameras)
    public override void AfterRender() { }

    // Before each camera renders
    public override void BeforeCameraRender(Camera camera) { }

    // After each camera renders
    public override void AfterCameraRender(Camera camera) { }

    // Custom mesh sorting (e.g., sort transparent objects by distance)
    public override void SortMeshes(List<Mesh> meshes, Camera camera)
    {
        // Default sorts by material; override as needed
        base.SortMeshes(meshes, camera);
    }
}
```

### RenderPass Hooks

```csharp
public class MyPass : RenderPass
{
    // Called once when the Pass is first initialized
    public override void Setup() { }

    // Per-frame, before/after rendering (for Once-type Passes)
    public override void BeforeRender() { }
    public override void AfterRender() { }

    // Per-camera, before/after rendering (for EveryCamera-type Passes)
    public override void BeforeRender(Camera camera) { }
    public override void AfterRender(Camera camera) { }
}
```

### Custom Mesh Filtering

Always prefer the frustum-culled rendering methods. Culled versions automatically skip invisible meshes for optimal performance.

**Preferred — Culled rendering:**

```csharp
// Render meshes that pass frustum culling
RenderVisibleMeshesInCamera(filter, camera.View, camera.Projection);

// Render instanced meshes that pass frustum culling
RenderVisibleInstancedMeshesInCamera(filter, camera.View, camera.Projection);
```

Typical opaque Pass example:

```csharp
public override void Render(Camera camera)
{
    // Render opaque static meshes
    UseShader();
    RenderVisibleMeshesInCamera(
        mesh => mesh.IsStaticMesh
             && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque),
        camera.View, camera.Projection);

    // Render opaque skinned meshes (with skinning macro variant)
    UseShader("SKINNED_MESH");
    RenderVisibleMeshesInCamera(
        mesh => mesh.IsSkinnedMesh
             && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque),
        camera.View, camera.Projection);

    // Render instanced meshes
    RenderVisibleInstancedMeshesInCamera(
        im => im.EnableFrustumCulling,
        camera.View, camera.Projection);
}
```

**Fallback — Unculled rendering (use only when):**

- Very few objects — culling overhead exceeds benefit
- Need to iterate by type rather than visibility (e.g., `RenderStaticMeshes` / `RenderSkinnedMeshes`)
- Rendering from a pre-filtered external list (`RenderMeshesFromList`)
- Debugging — temporarily disable culling to narrow down issues

```csharp
// All meshes (regardless of static/skinned, regardless of visibility)
RenderMeshes(filter, camera.View, camera.Projection);

// Static meshes only
RenderStaticMeshes(filter, camera.View, camera.Projection);

// Skinned meshes only
RenderSkinnedMeshes(filter, camera.View, camera.Projection);

// All instanced meshes
RenderInstancedMeshes(filter, camera.View, camera.Projection);

// From a specific list
RenderMeshesFromList(myMeshList, filter, camera.View, camera.Projection);
```

### Render Method Quick Reference

| Method | Type | Culled | Recommendation |
|---|---|---|---|
| `RenderVisibleMeshesInCamera(filter, view, proj)` | Mesh | ✅ | ⭐ Preferred |
| `RenderVisibleInstancedMeshesInCamera(filter, view, proj)` | InstancedMesh | ✅ | ⭐ Preferred |
| `RenderMeshesFromList(list, filter, view, proj)` | Mesh | ❌ | External list scenario |
| `RenderStaticMeshes(filter, view, proj)` | Mesh | ❌ | Iterate by type |
| `RenderSkinnedMeshes(filter, view, proj)` | Mesh | ❌ | Iterate by type |
| `RenderMeshes(filter, view, proj)` | Mesh | ❌ | Debugging / few objects |
| `RenderInstancedMeshes(filter, view, proj)` | InstancedMesh | ❌ | Debugging / few objects |

## Multi-Camera Rendering

Aura3D supports rendering multiple camera views simultaneously, e.g., for split-screen or minimaps.

### Creating Additional Cameras

```csharp
// Create a second camera in SceneInitialized
var secondCamera = new Camera
{
    Position = new Vector3(10, 5, 0),
    IsRenderBackground = false  // Don't re-render skybox from the second view
};
secondCamera.LookAt(Vector3.Zero);

scene.AddNode(secondCamera);
```

All `Camera` nodes in the scene are automatically discovered and rendered by the `RenderPipeline`. Each Pass registered as `RenderPassGroup.EveryCamera` executes once per camera.

### Render to Texture

Use `ControlRenderTarget` to render a camera's view to a texture for minimaps, surveillance views, etc.:

```csharp
// Create an offscreen render target
var renderTarget = new ControlRenderTarget(width, height);
secondCamera.RenderTarget = renderTarget;

// After rendering, the target contains that camera's view
// Can be read in SceneUpdated and used as material input
```

## Resource Management

### GPU Resource Lifecycle

All objects implementing `IGpuResource` (Geometry, Material, Texture, RenderTarget, etc.) have their lifecycle managed by the `RenderPipeline`:

```csharp
// Manually add a resource to the pipeline (usually automatic via AddNode)
view.Scene.RenderPipeline.AddGpuResource(myResource);

// Manually remove
view.Scene.RenderPipeline.RemoveGpuResource(myResource);
```

**IGpuResource Interface:**

| Member | Description |
|---|---|
| `NeedsUpload` (bool) | Whether the resource needs uploading to the GPU |
| `Upload(GL gl)` | Upload data to GPU |
| `Destroy(GL gl)` | Destroy GPU resource |

### Enumerate All GPU Resources of a Model

```csharp
// Get all GPU resources under a model (geometry, material textures, etc.)
var resources = model.GetGpuResources();
foreach (var res in resources)
{
    // e.g., check if upload is needed
    if (res.NeedsUpload) { /* ... */ }
}
```

### RenderPass Context Methods

```csharp
// Resolve a saved handle at the current camera size
var rt = GetRenderTarget(baseRenderTarget, camera);

// Bind the SetOutput target (defaults to CameraOutput when unset)
BindOutput(camera);

// Render a fullscreen quad (common for post-processing)
RenderQuad();

// Render a unit cube (for debugging / environment maps)
RenderCube();
```
