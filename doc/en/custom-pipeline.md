---
section: advanced
order: 4
---

# Custom Render Pipelines

This page is for **extending the engine**: not just running a ready-made pipeline, but building your own and writing your own `RenderPass`. You need basic rendering knowledge (shaders, uniforms, framebuffers, draw calls), but you don't touch low-level VAO/VBO buffer details — the engine wraps those. If you only want to pick a built-in pipeline and tweak settings, go back to [Choosing and Configuring Pipelines](./pipelines.md).

## Two Roles

A custom pipeline rests on two base classes, each with a distinct job:

- **`RenderPipeline`** — the assembler. It registers render targets (framebuffers) and render steps (passes), decides in what order they run and how often per camera, and calls back hooks at fixed points of each frame.
- **`RenderPass`** — the worker. A single shader-driven draw step — bind an output, set uniforms, draw meshes or a fullscreen quad. Usually one shader (with its macro variants) maps to one pass.

```
RenderPipeline (subclass it, assemble in the constructor)
  ├── RegisterRenderTarget: framebuffer + color/depth texture attachments
  ├── RegisterRenderPass: register a step, its output target, and its timing
  └── Dispatch
       ├── RenderPassGroup.Once — run once per frame (e.g., ShadowMap)
       └── RenderPassGroup.EveryCamera — run once per camera (e.g., main render)
```

## Assembling a Pipeline: RenderPipeline

The pipeline registers targets and passes inside its own constructor. Below is the real assembly of the built-in NoLight pipeline ([NoLightPipeline.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/NoLight/NoLightPipeline.cs)):

```csharp
public class NoLightPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    public NoLightPipeline(Scene scene) : base(scene)
    {
        // 1. Register render targets (framebuffer + attachments)
        var baseRenderTarget = RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(Settings.DepthFormat);

        var gammaOutput = RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(Settings.DepthFormat);

        var noLightPass = new NoLightPass(this);

        // 2. Register passes in order; each SetOutput says where it draws
        RegisterRenderPass(new BackgroundPass(this).SetOutput(baseRenderTarget),
            RenderPassGroup.EveryCamera);
        RegisterRenderPass(noLightPass.SetOutput(baseRenderTarget),
            RenderPassGroup.EveryCamera);
        RegisterRenderPass(new ParticlePass(this).SetOutput(baseRenderTarget),
            RenderPassGroup.EveryCamera);
        RegisterRenderPass(
            new GammaCorrectionPass(this, baseRenderTarget.GetTexture("Color"))
                .SetOutput(gammaOutput),
            RenderPassGroup.EveryCamera);
        // The last pass outputs to CameraOutput, i.e. the screen
        RegisterRenderPass(
            new FxaaPass(this, gammaOutput.GetTexture("Color")).SetOutput(CameraOutput),
            RenderPassGroup.EveryCamera);

        RegisterDebugPass(baseRenderTarget);
    }

    public static RenderPipeline CreateInstance(Scene scene) => new NoLightPipeline(scene);
}
```

**Key APIs:**

| Method | Description |
|---|---|
| `RegisterRenderTarget(name)` | Register a framebuffer, returns a configurator |
| `.AddTexture(name, format)` | Add a color attachment to that target |
| `.SetDepthTexture(format)` | Add a depth attachment (typically `Settings.DepthFormat`) |
| `RegisterRenderPass(pass, group)` | Register a step; `group` sets timing, passes run in registration order |
| `pass.SetOutput(target)` | Where this pass draws; defaults to `CameraOutput` (screen) when unset |
| `CameraOutput` | An output ref meaning "the current camera's final output", usually for the last pass in a chain |

**The two `RenderPassGroup` values:**

- `EveryCamera` — runs once per camera (most of the main render chain).
- `Once` — runs once per frame, before the camera loop (e.g., ShadowMap pre-pass).

> [!NOTE]
> After registering, hand the pipeline to the control in `CreateRenderPipeline`: `view.CreateRenderPipeline = scene => new NoLightPipeline(scene);`, before GL initialization (see [Choosing and Configuring Pipelines](./pipelines.md)). To let the control instantiate it by type, implement `IRenderPipelineCreateInstance` and provide a static `CreateInstance`.

## A Minimal RenderPass

A pass is just: tell it which shader source to use, then override `Render(Camera)` to draw. Here's the complete NoLight-style pass:

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
            camera.View, camera.Projection);

        // Render opaque skinned meshes (SKINNED_MESH macro variant)
        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(
            mesh => mesh.IsSkinnedMesh
                 && (mesh.Material == null
                     || mesh.Material.BlendMode == BlendMode.Opaque),
            camera.View, camera.Projection);
    }
}
```

> For new pipelines, **prefer the culled render methods** (`RenderVisibleMeshesInCamera`) — they iterate only the current camera's visible meshes, the best-performing choice. `mesh.IsStaticMesh` / `mesh.IsSkinnedMesh` are `Mesh` properties distinguishing static/skinned meshes, so you don't hand-check skeletons.

### Mesh Filtering and Render Method Quick Reference

**Preferred — culled rendering:**

```csharp
// Render meshes that pass frustum culling
RenderVisibleMeshesInCamera(filter, camera.View, camera.Projection);

// Render instanced meshes that pass frustum culling
RenderVisibleInstancedMeshesInCamera(filter, camera.View, camera.Projection);
```

A typical opaque pass:

```csharp
public override void Render(Camera camera)
{
    // Opaque static meshes
    UseShader();
    RenderVisibleMeshesInCamera(
        mesh => mesh.IsStaticMesh
             && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque),
        camera.View, camera.Projection);

    // Opaque skinned meshes
    UseShader("SKINNED_MESH");
    RenderVisibleMeshesInCamera(
        mesh => mesh.IsSkinnedMesh
             && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque),
        camera.View, camera.Projection);

    // Instanced meshes
    RenderVisibleInstancedMeshesInCamera(
        im => im.EnableFrustumCulling, camera.View, camera.Projection);
}
```

**Fallback — unculled** (only when objects are very few and culling costs more than it saves; you must traverse by type; you render from a pre-filtered external list; or you disable culling for debugging):

```csharp
RenderMeshes(filter, view, proj);              // all meshes, ignoring visibility
RenderStaticMeshes(filter, view, proj);        // static meshes only
RenderSkinnedMeshes(filter, view, proj);       // skinned meshes only
RenderInstancedMeshes(filter, view, proj);     // all instanced meshes
RenderMeshesFromList(list, filter, view, proj);// from a specific list
```

| Method | Type | Culled | Recommendation |
|---|---|---|---|
| `RenderVisibleMeshesInCamera(filter, view, proj)` | Mesh | ✅ | ⭐ Preferred |
| `RenderVisibleInstancedMeshesInCamera(filter, view, proj)` | InstancedMesh | ✅ | ⭐ Preferred |
| `RenderMeshesFromList(list, filter, view, proj)` | Mesh | ❌ | External list |
| `RenderStaticMeshes(filter, view, proj)` | Mesh | ❌ | Traverse by type |
| `RenderSkinnedMeshes(filter, view, proj)` | Mesh | ❌ | Traverse by type |
| `RenderMeshes(filter, view, proj)` | Mesh | ❌ | Debugging / few objects |
| `RenderInstancedMeshes(filter, view, proj)` | InstancedMesh | ❌ | Debugging / few objects |

> [!TIP]
> `RenderVisibleMeshesInCamera` relies on the visible list the pipeline computed before this camera — and that list is governed by `PipelineSettings.EnableFrustumCulling` (see [Choosing and Configuring Pipelines](./pipelines.md#frustum-culling)). With culling off, the visible methods fall back to iterating every mesh.

## Passing Parameters to a Single Mesh

To vary parameters per mesh (color, flags, etc.), override `RenderMesh` and set each mesh's uniforms before it draws. Note that `base.RenderMesh` already sets `modelMatrix`, binds material parameters, and issues the draw call for you — so you only add your own uniforms and **must set view / projection**:

```csharp
public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
{
    if (someCondition)
    {
        UniformFloat("someParameter", value);
        UniformVector4("someColor", new Vector4(1, 0, 0, 1));
    }

    // These two base matrices must be set
    UniformMatrix4("viewMatrix", view);
    UniformMatrix4("projectionMatrix", projection);

    base.RenderMesh(mesh, view, projection);
}
```

**Common Uniform methods:**

| Method | Use |
|---|---|
| `UniformInt(name, value)` | int |
| `UniformFloat(name, value)` | float |
| `UniformVector2 / 3 / 4(name, value)` | vectors |
| `UniformMatrix4(name, value)` | 4×4 matrix |
| `UniformMatrix4Array(name, span)` | matrix array |
| `UniformColor(name, color)` | color (converted to vec3 internally) |
| `UniformTexture(name, texture)` | 2D texture (auto-takes a texture unit) |
| `UniformTextureCubeMap(name, texture)` | cube map |

> If `CurrentShader` isn't active yet, or the shader has no uniform of that name, `Uniform*` silently skips and never throws — this is exactly the visible symptom of the "UseShader must come first" pitfall below.

## The Shader Macro System

Shader "variants" (one source compiled into several programs by different macros) are produced by three cooperating methods. Understanding their relationship is the crux of custom pipelines.

### Division of Labor

| Method | Role | Touches GPU? |
|---|---|---|
| `UseShader(params string[] defines)` | **Replace** the current defines list | No, only records intent |
| `AddDefines(params string[] defines)` | **Append** to the current defines list | No |
| `UseShader_Internal(...)` | Read current defines, compile/cache/activate the variant | Yes, `gl.UseProgram` |

`UseShader` / `AddDefines` are **declarative** — they only record "which macros I want," never touching the GPU. Real compilation and binding happen in `UseShader_Internal`, which render methods like `RenderVisibleMeshesInCamera` call **automatically** before each mesh.

```
1. UseShader("SKINNED_MESH")     → defines = ["SKINNED_MESH"]
2. RenderVisibleMeshesInCamera(...)
   ├─ per mesh:
   │   UseShader_Internal(mesh)  → reads defines = ["SKINNED_MESH"]
   │      cache key = "SKINNED_MESH"; hit → gl.UseProgram, miss → compile + cache
   │   RenderMesh(mesh, ...)     → set uniforms, gl.DrawElements
3. UseShader("SKINNED_MESH", "BLENDMODE_MASKED")
                                → defines = ["SKINNED_MESH", "BLENDMODE_MASKED"]
4. RenderVisibleMeshesInCamera(...)
   └─ UseShader_Internal(mesh)  → cache key = "SKINNED_MESH;BLENDMODE_MASKED" (different key = different variant)
```

When a group of meshes shares most macros and differs in only a few, append with `AddDefines` instead of re-listing everything:

```csharp
UseShader("SKINNED_MESH");
RenderVisibleMeshesInCamera(filter1, camera.View, camera.Projection);

// Append one macro, producing the SKINNED_MESH + BLENDMODE_MASKED variant
AddDefines("BLENDMODE_MASKED");
RenderVisibleMeshesInCamera(filter2, camera.View, camera.Projection);
```

(There's also `RemoveDefines(params string[])` that drops named macros from the current list, used the same way.)

### Compilation Flow (Condensed)

When `UseShader_Internal` runs, it roughly:

1. Joins `defines` with `;` into a cache key (e.g. `"SKINNED_MESH;BLENDMODE_MASKED"`).
2. If the material supplies custom source via `SetShaderSource` → check the material-level cache; on miss compile with the material source; otherwise check the Pass-level cache; on miss compile with the Pass's `VertexShader`/`FragmentShader`.
3. During compilation, injects `#define SKINNED_MESH\n...` at the `//{{defines}}` marker in the source.
4. Picks the dialect per context automatically (reads `GL_VERSION` on first use): a GLES context gets the source as authored; a desktop GL context gets `#version 300 es` rewritten to `#version 410 core` with `precision` declarations stripped (macOS offers only desktop GL, so it takes this branch; desktop hosts need GL 4.1+).
5. Links the program, enumerates all uniform locations, and caches them.

### Two-Level Caching

A given defines combination compiles only once; later frames reuse the cached program handle. Caching is two-level:

| Cache level | Storage | When used |
|---|---|---|
| Pass-level | `RenderPass.Shaders["key"]` | When the material has no custom shader |
| Material-level | Under the material's GPU state, keyed `ShaderName;key` | When the material overrides shader source via `SetShaderSource` |

### Macro Injection Point in Shader Source

GLSL source uses `//{{defines}}` as the injection marker:

```glsl
#version 300 es
precision mediump float;

//{{defines}}   ← Replaced at compile time with #define SKINNED_MESH etc.

layout(location = 0) in vec3 position;

#ifdef INSTANCED_MESH
layout(location = 8) in mat4 modelMatrix;
#endif

#ifndef INSTANCED_MESH
uniform mat4 modelMatrix;
#endif
```

> [!IMPORTANT]
> **Defines order affects the cache key.** It's joined with `;`: `UseShader("A").AddDefines("B")` gives `"A;B"`, and `UseShader("A", "B")` also gives `"A;B"` — they match. But `UseShader("B")` then `AddDefines("A")` gives `"B;A"`, a **different** variant. Prefer declaring all needed macros at once with a single `UseShader`.

## Post-Processing Passes: Manual UseShader_Internal + RenderQuad / RenderCube

The render methods above call `UseShader_Internal` for you. But **if a pass doesn't iterate meshes** — say, a post-processing pass that just draws a fullscreen quad — you must **call it manually**. `RenderQuad()` draws a quad covering NDC and `RenderCube()` draws a unit cube; both are built-in `RenderPass` methods used for post-processing and debugging.

Standard post-processing pass flow:

```
UseShader()           → Declare macros (optional)
UseShader_Internal()  → Compile/activate the variant (manual!)
UniformTexture(...)   → Set input textures and other uniforms
RenderQuad()          → Draw a fullscreen quad
```

Real example — the Gamma Correction pass ([GammaCorrectionPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/Common/GammaCorrectionPass.cs)):

```csharp
public override void Render(Camera camera)
{
    BindOutputRenderTarget(camera);
    var source = GetTexture(inputTexture, camera);

    gl.Disable(EnableCap.DepthTest);
    gl.Disable(EnableCap.Blend);

    UseShader();              // No macros needed, can be omitted
    ClearTextureUnit();       // Reset the texture unit counter
    UseShader_Internal();     // ← Manual activation! No Material context here, passes null
    UniformTexture("colorTexture", source);
    RenderQuad();             // Draw fullscreen quad, sampling the input texture for gamma correction
}
```

The FXAA pass works the same ([FxaaPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/Common/FxaaPass.cs)):

```csharp
UseShader();
ClearTextureUnit();
UseShader_Internal();
UniformTexture("u_texture", rt.GetTexture(inputTextureName));
UniformVector2("u_textureSize", new Vector2(texWidth, texHeight));
RenderQuad();
```

Post-processing with a macro variant — the PBR IBL ambient pass ([IBLAmbientPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Pipeline.PBR/IBLAmbientPass.cs)):

```csharp
UseShader("ENBALE_DEFERRED_SHADING");  // Declare macro first
UseShader_Internal();                  // Then compile the macro variant and activate
ClearTextureUnit();
UniformTexture("gBufferBaseColor", gBufferBaseColor);
UniformTexture("gBufferNormalRoughness", gBufferNormalRoughness);
// ... more uniforms ...
UniformMatrix4("u_viewMatrix", camera.View);
UniformMatrix4("u_projMatrix", camera.Projection);
RenderQuad();
```

> [!IMPORTANT]
> **`UseShader` / `AddDefines` must be called *before* `UseShader_Internal`.** `UseShader_Internal` reads the defines list **as of that moment** to decide which variant to activate; changing defines afterward does not affect the already-active shader. Hand-writing a post-processing pass and forgetting `UseShader()` first — or reversing the order — is the most common source of a blank screen or the wrong variant.

## Lifecycle Hooks and Mesh Sorting

Both `RenderPipeline` and `RenderPass` provide virtual methods to insert logic at fixed points of the render flow.

### RenderPipeline Hooks

```csharp
public class MyPipeline : RenderPipeline
{
    // Called once after GL init (after RenderTargets/RenderPasses are registered)
    public override void Setup() { }

    // Before the whole frame (once per frame, before all cameras)
    public override void BeforeRender() { }

    // After the whole frame (once per frame, after all cameras)
    public override void AfterRender() { }

    // Before / after each camera
    public override void BeforeCameraRender(Camera camera) { }
    public override void AfterCameraRender(Camera camera) { }

    // Custom mesh sorting (default sorts by distance to camera; override,
    // e.g., sort transparent objects far-to-near)
    public override void SortMeshes(IReadOnlyList<Mesh> meshes, Camera camera)
    {
        base.SortMeshes(meshes, camera);
    }
}
```

The built-in NoLight pipeline sorts the visible meshes and sets the viewport in `BeforeCameraRender`:

```csharp
public override void BeforeCameraRender(Camera camera)
{
    base.BeforeCameraRender(camera);
    if (gl == null) return;
    SortMeshes(VisibleMeshesInCamera, camera);
    gl.Viewport(0, 0, camera.Width, camera.Height);
}
```

### RenderPass Hooks

```csharp
public class MyPass : RenderPass
{
    // Called once when the pass first initializes
    public override void Setup() { }

    // Per-frame before/after (Once-type passes use these no-arg versions)
    public override void BeforeRender() { }
    public override void AfterRender() { }

    // Per-camera before/after (EveryCamera-type passes use these camera versions)
    public override void BeforeRender(Camera camera) { }
    public override void AfterRender(Camera camera) { }
}
```

> Your actual drawing goes in `Render()` (Once-type) or `Render(Camera camera)` (EveryCamera-type); the scheduler wraps it between the matching Before/After hooks.

## Just Want to Change One Material?

If your goal is only to make one specific material use a different shader — not to build a whole pipeline/pass — material-level shader replacement is a better fit: `material.SetShaderSource(passKey, ShaderType.Vertex/Fragment, src)`, with parameters set via `SetParameterValue` and auto-bound to same-named uniforms by the pass. That belongs to material extension; the full recipe is in [Custom Materials and Shaders](./custom-material.md).

## Common Pitfalls

- **`UseShader` after `UseShader_Internal`**: In hand-written post/custom passes, declare macros first, then activate. Reverse it and `UseShader_Internal` reads the old defines — wrong variant, or a blank screen.
- **Defines order changes the cache key**: `"A;B"` and `"B;A"` are two different variants. Declare all macros in one `UseShader` call; don't build the list in varying order.
- **Forgetting `UseShader_Internal`**: A pass that doesn't iterate meshes (pure `RenderQuad` / `RenderCube`) must activate the shader manually, or `CurrentShader` stays null and every `Uniform*` call is silently skipped.
- **Overriding `RenderMesh` without view/projection**: `base.RenderMesh` only sets `modelMatrix` and material parameters for you; you must set `viewMatrix`/`projectionMatrix` yourself or projection breaks.
- **A pass with no `SetOutput`**: Defaults to `CameraOutput` (screen). Forgetting output on an intermediate pass smears its result straight onto the screen.
- **When to register the pipeline**: Custom pipelines load via `view.CreateRenderPipeline = scene => new MyPipeline(scene);`, and it must be assigned before GL initialization.
- **No injection marker in the shader source**: If the source has no `//{{defines}}`, the `#define` from `UseShader("SOME_MACRO")` can't be injected and the `#ifdef` never holds.

## Runnable Examples and Source

- NoLight pipeline and pass (assembly + EveryCamera chain): [NoLightPipeline.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/NoLight/NoLightPipeline.cs)
- Post-processing with manual `UseShader_Internal`: [GammaCorrectionPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/Common/GammaCorrectionPass.cs), [FxaaPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Renderers/Common/FxaaPass.cs)
- Post-processing with a macro variant: [IBLAmbientPass.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Pipeline.PBR/IBLAmbientPass.cs)
- Built-in passes and macro quick reference (which macros each pass uses, injection conventions): [Built-in Passes and Shader Macros](./reference-shaders.md)

## Next Steps

- Picking/configuring pipelines by what you want to achieve: [Choosing and Configuring Pipelines](./pipelines.md)
- Material and shader extension: [Custom Materials and Shaders](./custom-material.md)
