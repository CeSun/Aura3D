---
section: basics
order: 5
---

# Materials and Textures

A material decides how a mesh looks: color, textures, transparency, face culling. Usually no shaders needed — drop in a few textures and pick a blend mode.

## The fastest way to get something on screen

For the default Blinn-Phong pipeline to light a mesh, give it a material with a `BaseColor` and make sure the scene has a light (see [Lighting and Shadows](./lighting.md)).

```csharp
var mesh = new Mesh();
mesh.Geometry = new BoxGeometry();

var material = new Material();
material.BaseColor = Texture.CreateFromColor(Color.White);
material.BlendMode = BlendMode.Opaque;

mesh.Material = material;
view.AddNode(mesh);
```

> [!WARNING]
> `BaseColor` is an **extension property** on `Material`, so it cannot go in an object initializer: `new Material { BaseColor = ... }` will not compile. To do it in one step, write `new Material { BlendMode = BlendMode.Opaque }` and then assign `material.BaseColor = ...;` separately. `BlendMode`, `DoubleSided` and `AlphaCutoff` are real properties and *can* go in an initializer.

## The three core switches

| Property | Values / default | What it does |
|---|---|---|
| `BlendMode` | `Opaque` / `Masked` / `Translucent`, default `Opaque` | Three render paths: fully opaque, alpha-cutout, translucent blend |
| `DoubleSided` | `bool`, default `false` | Disables back-face culling so both faces draw (thin sheets, cloth, single-sided planes) |
| `AlphaCutoff` | `float`, default `0.5` | Only used by `Masked`: texel alpha below this is discarded |

The three blend modes:

```csharp
material.BlendMode = BlendMode.Opaque;        // Opaque, cheapest, default
material.BlendMode = BlendMode.Masked;        // Cutout: per-pixel keep-or-drop by AlphaCutoff
material.BlendMode = BlendMode.Translucent;   // Translucent: blended with the background by alpha
```

`Masked` fits things that are either fully transparent or fully opaque (pierced signs, chain-link fences, foliage): it uses `discard` to drop pixels and needs no sorting, so it's cheaper than `Translucent`. `Translucent` is the real alpha-graded path for glass and water.

> [!NOTE]
> `discard` (alpha cutout) only takes effect on the `Masked` path. Setting `AlphaCutoff` on a `Translucent` material won't turn it into a cutout, and a translucent soft edge won't be cropped by `AlphaCutoff` either — they are different branches. When you hit "my transparent texture renders solid / translucent sorting is wrong", see [Common Pitfalls and Troubleshooting](./troubleshooting.md).

## Texture channels

A material organizes textures through *channels*, each one a `{ Name, Texture }`. Built-in pipeline shaders sample textures by a agreed-upon channel name:

| Channel | Meaning | Blinn-Phong | PBR |
|---|---|---|---|
| `BaseColor` | Base color / albedo | Sampled | Sampled |
| `Normal` | Tangent-space normal map | Sampled | Sampled |
| `MetallicRoughness` | Packed metallic/roughness map (R=metallic, G=roughness, glTF convention) | Not sampled | Sampled |
| `Occlusion` | Ambient occlusion | Not sampled | Bound but not sampled by the shader |
| `Emissive` | Emissive / self-lit | Not sampled | Sampled only in the deferred pipeline's opaque branch |

Ways to set / read channels:

```csharp
// 1) Convenience extension properties (only BaseColor / Normal have them)
material.BaseColor = Texture.CreateFromColor(Color.White);
material.Normal = normalTexture;

// 2) By channel name (works for every channel)
material.SetTexture("MetallicRoughness", metallicRoughnessTexture);
material.SetTexture("Normal", null);          // pass null to remove the channel
var t = material.GetTexture("Normal");         // returns null if absent

// 3) Operate on a Channel object directly
material.SetChannel(new Channel { Name = "Emissive", Texture = emissiveTexture });
material.SetChannels(new[]
{
    new Channel { Name = "BaseColor", Texture = baseColorTexture },
    new Channel { Name = "Normal",    Texture = normalTexture },
});   // SetChannels clears existing channels first, then replaces them wholesale
```

> [!WARNING]
> `material.Channels` is **read-only** (`IReadOnlyList<Channel>`) — you can read it but not assign it, so don't write `material.Channels = new List<Channel>{...}`. To add or remove channels use `SetChannel` / `SetChannels` / `SetTexture`.

The `BaseColor` / `Normal` extension properties are just sugar over `SetTexture("BaseColor", …)` / `SetTexture("Normal", …)`; underneath they are channels, and you can mix both styles. How a custom channel becomes an `xxxTexture` uniform for your shader to sample is covered in [Custom Materials and Shaders](./custom-material.md).

## Loading textures

Texture objects come from `TextureLoader` or `Texture`'s static methods:

```csharp
using var stream = File.OpenRead("brick.png");
var albedo = TextureLoader.LoadTexture(stream);          // ordinary 2D image

using var hdr = File.OpenRead("studio.hdr");
var hdri = TextureLoader.LoadHdrTexture(stream);         // HDR environment map

// A solid-color texture, no file needed — the easiest placeholder / flat material
var flat = Texture.CreateFromColor(Color.FromArgb(255, 200, 50, 50));
```

Assign the loaded texture straight into a channel: `material.SetTexture("BaseColor", albedo);`. Cube maps (`LoadCubeTexture`) and HDR→cube conversion are mostly for environment and background — see [Environment and Background](./environment.md).

## Sampling configuration

A `Texture` also carries wrap, filter and color-space settings (`SetWrapS` / `SetWrapT` / `SetMinFilter` / `SetMagFilter` / `SetColorFormat` / `SetIsGammaSpace`); they directly control whether texture seams go black, whether minification shows moiré, and whether colors look right. This same set is shared with scene backgrounds and environment maps and is explained together in [Environment and Background](./environment.md), so we won't repeat it here — just remember you can chain these methods on a `Texture` before assigning it into a channel.

## Material parameters (for feeding custom shaders)

`SetParameterValue` attaches a name-bound value to the material. The built-in pipelines usually ignore it, but once you give a material a custom shader, same-named uniforms pick these values up automatically:

```csharp
material.SetParameterValue("uColor", new Vector4(1f, 0.3f, 0.2f, 1f));  // vec4
material.SetParameterValue("uTime", 0f);                                // float

if (material.TryGetParameterValue("uTime", out float time))
{
    material.SetParameterValue("uTime", time + 0.016f);                 // advance per frame
}

material.RemoveParameterValue("uColor");
```

The value's type decides which uniform kind it binds to: `int` / `float` / `Vector2` / `Vector3` / `Vector4` / `Matrix4x4`. Full usage — including overriding a Pass's GLSL — is in [Custom Materials and Shaders](./custom-material.md).

## Copying a material

To derive from an existing material while keeping the original, clone:

```csharp
var copy = material.Clone();                        // channel Textures share the same object as the original
var variant = material.DeepClone();                 // each channel clones its Texture (sampling config independent, pixels still shared)
var independent = material.DeepClone(deepCopyTextures: true); // pixel data duplicated too
```

| Method | Switches / channels / parameters | The channel's Texture |
|---|---|---|
| `Clone()` | All copied | Shares the same object — editing one reaches the other |
| `DeepClone()` (default) | All copied | Each shallow-cloned: sampling config independent, pixel data still shared |
| `DeepClone(deepCopyTextures: true)` | All copied | Each deep-cloned: pixel data stored separately too |

Pick `Clone()` when you don't mind the two materials sharing textures; use `DeepClone(deepCopyTextures: true)` only when you need fully independent copies (e.g. you'll rewrite pixels on each).

## Common pitfalls

- `new Material { BaseColor = ... }` won't compile: `BaseColor` is an extension property — assignable, but not in an initializer.
- `material.Channels = ...` has no effect: `Channels` is read-only; use `SetChannel` / `SetChannels` / `SetTexture`.
- The model is pitch black or missing: usually the scene has no light or no camera, not the material's fault — revisit [Lighting and Shadows](./lighting.md).
- Tweaking `AlphaCutoff` on a `Translucent` material to crop an edge, or wanting a soft edge on a `Masked` one: wrong mode — they're two paths, see [Common Pitfalls and Troubleshooting](./troubleshooting.md).
- Black seams or full-screen noise when the texture shrinks: that's a sampling (wrap / filter) issue — cross-check [Environment and Background](./environment.md).

## See it in action

These two Gallery samples use zero external assets — every texture is computed on the fly, so they're the quickest to copy:
- **PbrChannels** — which of the five channels actually get sampled under Blinn-Phong vs PBR, plus the `Masked` / `Translucent` / `AlphaCutoff` differences.
- **MaterialShaders** — `SetTexture`, material parameters, and a custom material that overrides only the fragment shader.

Source: [Material.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/Material.cs), [MaterialExtensions.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/MaterialExtensions.cs).
