---
section: notes
order: 4
---

# Built-in Passes and Shader Macros

**Pure quick reference**: built-in passes, shader macros, attribute locations, engine-provided uniforms, and dialect rules. Keep it open as a dictionary while writing custom passes.

## Built-in pass inventory

### Shared passes (`Aura3D.Core`)

| Pass | What it does | Group | Pipelines |
|---|---|---|---|
| `BackgroundPass` | Draws the background: solid color / texture / skybox | `EveryCamera` | All built-in pipelines |
| `ShadowMapPass` | Depth pre-pass producing shadow maps for all shadow-casting lights | `Once` (once per frame) | BlinnPhong / PBR Deferred / PBR Forward / CelShading |
| `LightPass` | Blinn-Phong forward main lighting: opaque + Masked, split into static / skinned / instanced passes | `EveryCamera` | BlinnPhong. The most common material-override pass key (`ShaderName` is `"LightPass"`) |
| `TranslucentPass` | Translucent meshes (back-to-front sorted, then blended); derives from `LightPass` | `EveryCamera` | BlinnPhong |
| `ParticlePass` | Billboard particle rendering; mesh-mode emitters render as `InstancedMesh` through the main lighting passes | `EveryCamera` | BlinnPhong / PBR Deferred / PBR Forward / CelShading |
| `GammaCorrectionPass` | Linear → sRGB output (full-screen quad) | `EveryCamera` | BlinnPhong / NoLight / PointCloud / CelShading / PBR (after ToneMapping) |
| `FxaaPass` | FXAA post-process anti-aliasing; with `Settings.EnableFxaa = false` it switches to the `FXAA_DISABLED` passthrough variant | `EveryCamera` | All built-in pipelines (outputs to `CameraOutput`) |
| `ToneMappingPass` | ACES tone mapping consuming `ToneMappingExposure` and `BrightnessClamp` | `EveryCamera` | PBR Deferred / PBR Forward |
| `CopyPass` | Full-screen texture copy moving one RenderTarget's color texture into another (e.g. compositing the lighting result onto the background target in PBR deferred) | `EveryCamera` | PBR Deferred |
| `DebugDrawPass` | Debug wireframe overlay: bounding boxes, light shapes, frustums, bones; driven by `Settings.Debug.*` | `EveryCamera` | All pipelines (auto-registered via `RegisterDebugPass`) |
| `NoLightPass` | Unlit output of the material color | `EveryCamera` | NoLightPipeline |
| `PointCloudPass` | Instanced point-cloud rendering (`gl_PointSize` + per-instance attribute shading) | `EveryCamera` | PointCloudPipeline |

### Pipeline-specific passes

| Pass | Package / pipeline | What it does |
|---|---|---|
| `BasePass` | `Aura3D.Pipeline.PBR` (PBR Deferred) | Writes materials into the GBuffer (BaseColor / NormalRoughness / MetallicEmissive as three RGBA8 attachments + depth) |
| `IBLAmbientPass` / `ConstantAmbientPass` | `Aura3D.Pipeline.PBR` | Ambient term of the deferred lighting: IBL convolved environment / constant ambient |
| `DirectionalLightingPass` / `PointLightingPass` / `SpotLightingPass` | `Aura3D.Pipeline.PBR` | Full-screen deferred lighting, one light type per pass |
| `TranslucentPass` / `TranslucentIBLAmbientPass` / `TranslucentConstantAmbientPass` | `Aura3D.Pipeline.PBR` | The forward supplemental lighting chain for translucency |
| `IrradianceMapPass` / `PrefilteredEnvironmentMapPass` | `Aura3D.Pipeline.PBR.Common` | Convolves the HDR environment into the irradiance map and the prefiltered environment map (lazy IBL generation) |
| `CelLightPass` / `OutlinePass` / `CelTranslucentPass` | `Aura3D.Pipeline.CelShading` | Toon main lighting / outlines / toon translucency |

Registration order and data flow per pipeline: [Choosing and Configuring Pipelines](./pipelines.md); writing your own `RenderPass` subclass: [Custom Render Pipelines](./custom-pipeline.md).

## The shader-macro system

### Injection point and flow

Every built-in GLSL carries a `//{{defines}}` line — when a variant is compiled, the engine expands the current defines list into `#define X` lines and replaces that comment **in place** (in both the vertex and the fragment source). Without the line, passing macros has no effect.

| Method | What it does | Touches the GPU |
|---|---|---|
| `UseShader(params string[] defines)` | **Replaces** the current defines list | No |
| `AddDefines(params string[] defines)` | **Appends** to the current list | No |
| `RemoveDefines(params string[] defines)` | Removes from the list | No |
| `UseShader_Internal()` | Compiles / fetches the cached variant for the current list and calls `glUseProgram` | Yes |

`RenderVisibleMeshesInCamera` and friends call `UseShader_Internal` automatically before each mesh; post-process passes that don't iterate meshes must call it **manually** before `RenderQuad()`. The cache key is the defines joined with `;` in order — a different order is a different variant, so declare all macros in one `UseShader` call. `UseShader`/`AddDefines` must run **before** `UseShader_Internal`, otherwise the previous frame's variant gets bound (an empty first frame).

### Macro names in actual use

All of the following appear verbatim in the engine source (keep the exact spelling and case):

| Macro | Injected by | What it does |
|---|---|---|
| `SKINNED_MESH` | LightPass / TranslucentPass / ShadowMapPass / NoLightPass / Cel / PBR passes | Skinning branch: deforms vertices from the `BoneMatrices` UBO (`MAX_BONES` 256) |
| `INSTANCED_MESH` | Every pass that supports instancing | `modelMatrix` / `normalMatrix` switch from uniforms to per-instance vertex attributes (locations 8–11 / 12–15) |
| `BLENDMODE_MASKED` | LightPass / ShadowMapPass / NoLightPass / Cel / PBR `BasePass` | Alpha cutout: `discard` when `alpha ≤ alphaCutoff` |
| `BLENDMODE_TRANSLUCENT` | TranslucentPass / NoLightPass / PointCloudPass / Cel / PBR | Translucent branch |
| `ENABLE_CSM` | BlinnPhong `LightPass` (when a main directional light is set), PBR `DirectionalLightingPass` | The main directional light samples cascaded shadow maps |
| `ENABLE_SHADOWS` | PBR Deferred lighting passes | This light type samples its shadows |
| `ENABLE_DIR_LIGHT` / `ENABLE_POINT_LIGHT` / `ENABLE_SPOT_LIGHT` | PBR Deferred / PBR Forward lighting and translucent passes | The full-screen lighting pass computes only this light type |
| `ENBALE_DEFERRED_SHADING` | PBR Deferred (`IBLAmbientPass`, `ConstantAmbientPass`, the lighting passes) | The GBuffer-reading deferred-lighting variant. **Note this is the source spelling — "ENBALE", one letter short of "ENABLE".** Copy it exactly; do not "fix" it |
| `IS_FIRST_LIGHT` | PBR `TranslucentConstantAmbientPass` | First-term handling in the multi-light translucent accumulation order |
| `FACE_RENDER` | CelShading `CelLightPass` | Face-specific toon lighting variant |
| `SKYBOX` / `ORTHOGRAPHIC` / `BACKGROUND_TEXTURE` | `BackgroundPass` | Skybox / skybox under an orthographic camera / textured background variants |
| `PARTICLE_OPAQUE` / `PARTICLE_MASKED` / `PARTICLE_TRANSLUCENT` | `ParticlePass` | Picks the particle variant per emitter BlendMode |
| `PARTICLE_TEXTURE` / `PARTICLE_FLIPBOOK` | `ParticlePass` | Samples the particle texture / additionally indexes flipbook tiles |
| `FXAA_DISABLED` | `FxaaPass` | Passthrough copy when anti-aliasing is off |

Additionally, the built-in shaders' headers define compile-time constants `MAX_BONES`, `MAX_DIRECTIONAL_LIGHTS` / `MAX_POINT_LIGHTS` / `MAX_SPOT_LIGHTS` (default 4). Changing the three light limits in `PipelineSettings` makes `LightPass` rewrite those `#define` lines by string replacement — which is why the limits must be set before the pipeline is created.

## Vertex and instance attribute location convention

**One rule: in GLSL, `layout(location = N)`'s N is the numeric value of the `BuildInVertexAttribute` enum.** The engine binds the VAO with the enum value directly — there is no remapping.

| Enum member | location | Notes |
|---|---|---|
| `Position` | 0 | Vertex position (vec3) |
| `TexCoord_0` | 1 | Primary UV |
| `Color_0` | 2 | Vertex color (vec4) |
| `Normal` | 3 | Normal |
| `Tangent` | 4 | Tangent |
| `Bitangent` | 5 | Bitangent |
| `Joints_0` | 6 | Bone indices |
| `Weights_0` | 7 | Bone weights |
| `InstancedTransformColumn0..3` | 8–11 | The per-instance `modelMatrix` under `INSTANCED_MESH` (a mat4 occupies 4 locations) |
| `InstancedNormalTransformColumn0..3` | 12–15 | The per-instance `normalMatrix` under `INSTANCED_MESH` |
| `TexCoord_1` | 16 | Free slot (the usual choice for a per-instance custom attribute, e.g. a vec4 instance color) |
| `TexCoord_2` | 17 | Free slot |
| `TexCoord_3` | 18 | Free slot |
| `Joints_1` | 19 | Reserved for extended skinning data |
| `Weights_1` | 20 | Reserved for extended skinning data |

> [!WARNING]
> For a custom per-instance attribute, the location declared in the shader must equal the **numeric value** of the enum member you passed (e.g. `SetInstanceAttribute<Vector4>(BuildInVertexAttribute.TexCoord_1, 4, …)` pairs with `layout(location = 16) in vec4 instanceColor;`). Don't guess the number from memory.

Conventions worth knowing:

- Built-in attributes with location ≤ 7 are enabled by default; switch off what you don't need via `geometry.SetAttributeEnabled(BuildInVertexAttribute.TexCoord_1, false)` to save bandwidth. On the instance side, `instancedMesh.SetAttributeEnabled("InstanceNormalTransform", false)` frees locations 12–15.
- In the `INSTANCED_MESH` variant `modelMatrix` is a per-instance vertex attribute (8–11), not a uniform — the two declarations are picked by an `#ifdef INSTANCED_MESH` branch, as every built-in shader does.
- Once you override the vertex shader, nobody multiplies the instance matrix for you: declare it at location 8 inside the `#ifdef INSTANCED_MESH` branch and multiply it in yourself (see the Gallery's `Kit/Shaders.cs`).

## Uniforms the engine always provides

| Uniform name | Provided by | Notes |
|---|---|---|
| `modelMatrix` / `viewMatrix` / `projectionMatrix` | Bound automatically by the render methods | Under the instanced variant, `modelMatrix` becomes a vertex attribute |
| `cameraPosition` | The camera | For parallax / environment sampling |
| `alphaCutoff` | The material's `AlphaCutoff` | Used in the `BLENDMODE_MASKED` branch |
| `BaseColorTexture` / `NormalTexture` and friends | Material channels | Channel `BaseColor` → sampler `BaseColorTexture` (PBR channels follow the same naming convention) |
| Any custom name | `material.SetParameterValue(name, value)` | Binds by name to the same-named uniform in the GLSL |

## Shader dialect (ShaderDialect)

In one sentence: **all built-in and custom shaders are written as GLSL ES 3.0 (`#version 300 es`)**; the dialect is chosen automatically at runtime from the context — an ES context compiles the source as-is, a desktop GL context swaps `#version 300 es` for `#version 410 core` and strips every `precision` declaration (so desktop requires GL 4.1+; macOS only offers desktop GL and always takes this branch).

The built-in shader sources live in [src/Aura3D.Core/Assets/Shaders/](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Assets/Shaders/) and are exposed through `ShaderResource` as `MeshVert`/`MeshFrag` (base.vert/base.frag), `BackgroundVert/Frag`, `ShadowMapVert/Frag`, `NoLightVert/Frag`, `DebugVert/Frag`; the GLSL for FXAA, Gamma, ToneMapping, Copy, Particle and PointCloud is embedded in the respective pass classes.

## Related pages

- [Custom Materials and Shaders](./custom-material.md) — overriding material-level shaders by pass key
- [Custom Render Pipelines](./custom-pipeline.md) — subclassing `RenderPass`, macros and `UseShader_Internal` in depth
- [Platforms and Render Backends](./platform-render-backends.md) — the GLES 3.0 subset and WebGL2 silent-drop rules
- [Common Pitfalls and Troubleshooting](./troubleshooting.md) — the symptom-indexed pitfall list
