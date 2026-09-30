---
section: advanced
order: 2
---

# Custom Materials and Shaders

**Change how a single material shades, without writing a whole render pipeline**: swap a Pass's GLSL, or write a shader that reads per-instance attributes. Taking over the whole pipeline: [Custom Render Pipelines](./custom-pipeline.md).

## Shortest runnable: turn a material into a constant color

`SetShaderSource(passKey, ShaderType, src)` hangs custom GLSL on a material by Pass name, overriding that Pass's vertex / fragment shader. The snippet below replaces the opaque lighting with a flat color taken from the material parameter `uColor`:

```csharp
var material = new Material { BlendMode = BlendMode.Opaque };

material.SetShaderSource("LightPass", ShaderType.Vertex, """
    #version 300 es
    precision highp float;

    layout(location = 0) in vec3 position;

    uniform mat4 modelMatrix;
    uniform mat4 viewMatrix;
    uniform mat4 projectionMatrix;

    void main()
    {
        gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
    }
    """);

material.SetShaderSource("LightPass", ShaderType.Fragment, """
    #version 300 es
    precision highp float;

    uniform vec4 uColor;

    out vec4 outColor;

    void main()
    {
        outColor = uColor;
    }
    """);

// uColor binds to the same-named uniform in the fragment
material.SetParameterValue("uColor", new Vector4(1f, 0f, 0f, 1f));

var mesh = new Mesh { Geometry = new BoxGeometry(), Material = material };
view.AddNode(mesh);
```

## passKey: put the Pass's name

`passKey` is the render Pass's name (equal to the Pass class name). In the default Blinn-Phong pipeline, the Pass that lights **opaque / masked** objects is called `LightPass`; **translucent** objects go through a different Pass, `TranslucentPass`.

> [!WARNING]
> If you override only `LightPass` but set the material to `BlendMode.Translucent`, you won't see your shader — that object is now on `TranslucentPass`. To make a custom shader apply to translucent objects, hang it on the `"TranslucentPass"` key too.

Vertex and fragment can be overridden separately, and you can override **only one** — the other keeps that Pass's default source. The stripes example below swaps only the fragment; the vertex stays the engine default (so the model must have UVs):

```csharp
var material = new Material { BlendMode = BlendMode.Opaque };

material.SetShaderSource("LightPass", ShaderType.Fragment, """
    #version 300 es
    precision highp float;
    //{{defines}}

    in vec2 vTexCoord;

    uniform vec4 uColor;
    uniform vec2 uStripe;
    uniform float uTime;
    uniform float alphaCutoff;

    out vec4 outColor;

    void main()
    {
        float phase = vTexCoord.x * uStripe.x + vTexCoord.y * uStripe.y + uTime;
        float edge = smoothstep(0.35, 0.95, sin(phase * 6.28318));

        float alpha = uColor.a;

    #if defined(BLENDMODE_MASKED) || defined(BLENDMODE_TRANSLUCENT)
        alpha = mix(0.0, uColor.a, edge);

        if (alpha <= alphaCutoff)
            discard;
    #endif

        outColor = vec4(mix(uColor.rgb, vec3(1.0), edge), alpha);
    }
    """);

material.SetParameterValue("uColor",  new Vector4(0.15f, 0.45f, 0.85f, 1f));
material.SetParameterValue("uStripe", new Vector2(6f, 2f));
material.SetParameterValue("uTime",   0f);

// Advance uTime each frame to animate the stripes
material.SetParameterValue("uTime", (material.TryGetParameterValue("uTime", out float t) ? t : 0f) + (float)deltaTime);
```

## What the engine always gives you

When you write this GLSL, the engine feeds you these by convention:

- **Uniforms**: `modelMatrix`, `viewMatrix`, `projectionMatrix`, `cameraPosition` always exist; `alphaCutoff` comes from the material's `AlphaCutoff`.
- **Channel → sampler**: a material channel named `<X>` is bound as a `sampler2D` named `<X>Texture` — the `BaseColor` channel is `BaseColorTexture` in your fragment (custom GLSL declares and samples it itself).
- **Fixed vertex attribute slots**: 0 = position, 1 = UV, 2 = vertex color (`vec4`), 3 = normal.
- **Material parameter → uniform**: `SetParameterValue(name, value)` binds to the **same-named** uniform; the value's type decides whether it's `float` / `Vector2/3/4` / `Matrix4x4` / `int`.
- **Macro injection point**: leave one line `//{{defines}}` in the GLSL; at compile time the engine substitutes the current macros. The blend mode arrives as `BLENDMODE_MASKED` / `BLENDMODE_TRANSLUCENT`, instancing as `INSTANCED_MESH`, skinning as `SKINNED_MESH` — so one fragment can branch with `#if defined(BLENDMODE_MASKED)` (discarding only under masked / translucent, as above).

> [!NOTE]
> A material's custom source takes priority over the Pass's default source; the same "Pass name + macro combination" is compiled only once and reused across frames. Macro order affects the cache key, so don't sometimes use `A,B` and other times `B,A`.

## Per-instance custom-attribute shaders

To send custom data per instance (color being the most common), use a material-level shader plus a per-instance vertex attribute. The convention is: **`TexCoord_1` lands on `location=16` to carry `instanceColor`; under the `INSTANCED_MESH` macro the per-instance `modelMatrix` lands on `location=8`** (a `mat4` occupies 4 consecutive slots 8–11, the declaration is written at 8). Instancing itself is covered in [Instanced Rendering](./instanced-rendering.md).

On the C# side — hang the GLSL below on the instanced mesh's material and feed per-instance colors into `location=16`:

```csharp
var material = new Material { BlendMode = BlendMode.Opaque };
material.SetShaderSource("LightPass", ShaderType.Vertex, instanceColorVert);
material.SetShaderSource("LightPass", ShaderType.Fragment, instanceColorFrag);

var sourceMesh = new Mesh { Geometry = new BoxGeometry(), Material = material };
var instancedMesh = InstancedMesh.FromMesh(sourceMesh);
instancedMesh.SetAttributeEnabled("InstanceNormalTransform", false);   // no per-instance normals needed, saves bandwidth

var colors = new List<Vector4>();
for (int i = 0; i < totalInstances; i++)
    colors.Add(new Vector4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), 1f));

instancedMesh.SetInstanceAttribute<Vector4>(BuildInVertexAttribute.TexCoord_1, 4, colors); // → location 16
view.AddNode(instancedMesh);
```

Vertex shader key point: once you **override the vertex stage you take over the instance transform** — the engine won't multiply the per-instance matrix in for you, so you must declare `mat4 modelMatrix` at `location=8` inside the `#ifdef INSTANCED_MESH` branch, along with `instanceColor` at `location=16`:

```glsl
#version 300 es
precision highp float;
//{{defines}}

layout(location = 0) in vec3 position;

#ifdef INSTANCED_MESH
layout(location = 8) in mat4 modelMatrix;
layout(location = 16) in vec4 instanceColor;
#else
uniform mat4 modelMatrix;
uniform vec4 instanceColor;
#endif

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

out vec4 vColor;

void main()
{
    vColor = instanceColor;
    gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
}
```

The fragment just outputs the per-instance color it received:

```glsl
#version 300 es
precision highp float;

in vec4 vColor;

out vec4 outColor;

void main()
{
    outColor = vColor;
}
```

## Common pitfalls

> [!WARNING]
> **Do not put Chinese comments or full-width characters inside GLSL source** — they make the shader fail to compile. Keep comments ASCII-only; put explanations on the C# side.

- `passKey` doesn't match the actual Pass: translucent objects go through `TranslucentPass`, so overriding only `LightPass` has no effect.
- A material parameter does nothing: the uniform name must match the `SetParameterValue` key **character for character** — a typo doesn't error, the uniform just keeps its default.
- Per-instance colors all black / all white: the `location`s aren't aligned to the convention (`TexCoord_1`→15, `modelMatrix`→7), or you forgot to multiply the per-instance matrix yourself in the `#ifdef INSTANCED_MESH` branch.
- Use `discard` only on the masked / translucent branch — see the `#if defined(...)` above; discarding under opaque will carve the whole object away. Symptom triage is in [Common Pitfalls and Troubleshooting](./troubleshooting.md).
- Every new macro combination triggers one shader recompile; keep adjustable values in `SetParameterValue` rather than calling `SetShaderSource` again just to change a color.

## See it in action

The Gallery's **MaterialShaders** sample covers all three: channel `SetTexture`, constant-color / fragment-only custom shaders, and material-parameter-driven stripes with a masked / translucent comparison.

Source: [MaterialShadersDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/MaterialShaders/MaterialShadersDemo.axaml.cs); the shader snippets live in [Kit/Shaders.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Kit/Shaders.cs). For the full list of built-in Pass names and macros, see [Built-in Passes and Shader Macros Reference](./reference-shaders.md).
