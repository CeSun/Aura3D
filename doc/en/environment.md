---
section: basics
order: 6
---

# Environment and Background

The background is decided by a single property: `Scene.Background`. This page covers solid colors, images, HDR / six-face skyboxes, and the sampling knobs to turn when "the texture looks wrong".

> [!TIP]
> The background ignores lighting entirely. With no light source your models go black while the background still shows — so "I added a background but still see nothing" is usually a lighting problem, see [Lighting and Shadows](./lighting.md).

## Shortest: replace the default background

```csharp
view.Scene.Background = Texture.CreateFromColor(Color.Gray);
```

`Scene` already ships a solid-color background (AliceBlue) from its constructor, so a fresh scene is never transparent. `Background` is typed `OneOf<CubeTexture, Texture>`, and the two branches are drawn in completely different ways:

| What you assign | How it is drawn |
|---|---|
| `CubeTexture` (cube map) | Skybox: sampled by view direction, so turning the camera reveals other directions |
| `Texture` (ordinary 2D image) | Stretched to fill the whole window, **aspect ratio not preserved**, independent of camera orientation |

(`Texture.CreateFromColor(...)` takes a `System.Drawing.Color`, 0..255 channels.)

## Solid color and single-image backgrounds

```csharp
// Solid color
view.Scene.Background = Texture.CreateFromColor(Color.Gray);

// One image: stretched over the window — brand plates, gradients
using (var stream = File.OpenRead("background.jpg"))
{
    view.Scene.Background = TextureLoader.LoadTexture(stream);
}
```

For an actual surrounding sky you need a cube map — the next two sections.

## HDR panorama → skybox

An equirectangular `.hdr` panorama cannot be assigned directly; it must be baked into a cube map first. The second argument is the **edge size of each face**:

```csharp
using (var stream = File.OpenRead("environment.hdr"))
{
    var hdri = TextureLoader.LoadHdrTexture(stream);

    // HDR data is linear; flagging it as gamma space decodes it twice and the sky turns washed out
    hdri.SetIsGammaSpace(false);

    view.Scene.Background = HDRIToCubeTextureConverter.ConvertFromTexture(hdri, 1024);
}
```

The conversion is real work — do it once during initialization, never per frame. Bigger faces mean a sharper sky and more VRAM.

## Six-face cube skybox

The easiest overload takes six file names:

```csharp
var cube = TextureLoader.LoadCubeTexture(new List<string>
{
    "px.png", "nx.png", "py.png", "ny.png", "pz.png", "nz.png",   // +X -X +Y -Y +Z -Z
});

view.Scene.Background = cube;
```

Loading from memory or a resource package means building a `List<Stream>` yourself — in that case **you own the streams and must dispose them**:

```csharp
var faces = new[] { "px.png", "nx.png", "py.png", "ny.png", "pz.png", "nz.png" };
var streams = new List<Stream>();

foreach (var face in faces)
{
    streams.Add(File.OpenRead(face));
}

try
{
    view.Scene.Background = TextureLoader.LoadCubeTexture(streams);
}
finally
{
    foreach (var s in streams) s.Dispose();
}
```

Hard requirements: **exactly 6 images**, all six with the **same dimensions**, and the **same channel layout** (all RGB or all RGBA). Any violation throws. Cube maps loaded from files are marked as gamma space (`IsGammaSpace = true`) — the expected treatment for LDR images.

## Turning the background off per camera

The background is a **per-camera** switch:

```csharp
camera.IsRenderBackground = false;   // default true
```

In multi-camera or minimap setups the secondary view (the one you added with `scene.AddNode(secondCamera)`) usually switches it off so the sky isn't drawn twice. Skyboxes render under both perspective and orthographic projection (there is a dedicated ortho path). Camera parameters themselves are in [Cameras and View Control](./camera.md).

## When a texture looks wrong: sampling

The loader's defaults are usable, but two symptoms need manual tuning: **stretched edges at seams** and **washed-out or oversaturated color**. These knobs live on `Texture` / `CubeTexture` and work as fluent calls or plain properties:

```csharp
var texture = TextureLoader.LoadTexture(stream)
    .SetWrapS(TextureWrapMode.Repeat)         // behavior past the U edge, default ClampToEdge
    .SetWrapT(TextureWrapMode.MirroredRepeat) // V axis, default ClampToEdge
    .SetMinFilter(TextureFilterMode.Linear)   // when minifying, default Linear
    .SetMagFilter(TextureFilterMode.Nearest)  // when magnifying, default Linear
    .SetColorFormat(ColorFormat.RGBA)         // channel count: RGB or RGBA
    .SetIsGammaSpace(true);                   // color space, default false
```

| Symptom | Adjustment |
|---|---|
| Edge pixels smeared into a streak where the tile repeats | `SetWrapS/SetWrapT` = `Repeat` (tiling) or `MirroredRepeat` (less visible seams) |
| Pixel-art look turns to mush | `SetMagFilter(TextureFilterMode.Nearest)` |
| Color washed out or oversaturated vs the source | `SetIsGammaSpace(true)` for LDR images — or the reverse, `false`, for linear data |
| Upload errors / shifted colors | `SetColorFormat(ColorFormat.RGB \| RGBA)` must match the actual channel count of the pixel data |

> [!NOTE]
> Three frequent misunderstandings:
> 1. Filters only come in `Nearest` / `Linear` — the engine generates no mipmaps for `Texture`/`CubeTexture`, so there are no mipmap options to pick.
> 2. `ColorFormat` describes the **channel count**, not the color space; the color space is `IsGammaSpace` (when `true`, the texture uploads with an sRGB internal format).
> 3. Cube maps have a third wrap dimension, set it through the property: `cube.WrapR = TextureWrapMode.ClampToEdge;`

## Can the background light the scene?

- **Default BlinnPhong pipeline**: no. The background is pixels only. Shadow-area brightness comes from `PipelineSettings.AmbientIntensity` (default 0.1; 0 makes shadows fully black).
- **PBR pipelines**: yes. `Scene.Background` is the single entry point for environment lighting — the engine bakes it into an irradiance map and a prefiltered reflection map (IBL), which is where metal balls get their reflections.

> [!TIP]
> Swapping the background at runtime under a PBR pipeline can leave IBL baked from the old image, because the two maps are cached as GPU state on the camera node. To force a rebake, invalidate the camera's cached states keyed `IrradianceMap` and `PrefilteredEnvironmentMap` — copy `InvalidateIblCaches()` from the Environment demo linked below. Pipeline and IBL configuration: [Choosing and Configuring Pipelines](./pipelines.md).

## Common pitfalls

| Symptom | Cause and fix |
|---|---|
| Background is set but objects are still black | The background lights nothing. Add a light source under the default pipeline, see [Lighting and Shadows](./lighting.md) |
| The "skybox" behaves like wallpaper and doesn't move when you turn | You assigned an ordinary `Texture` (the stretched branch); a surrounding sky needs a `CubeTexture` |
| Background distorted with the window | The 2D-image branch doesn't preserve aspect ratio — use a skybox, or pre-crop the source image |
| Loading a cube map throws | Not exactly 6 faces, mismatched dimensions, or mixed channel layouts |
| Image memory never released | `LoadCubeTexture(List<Stream>)` does not close your streams — `Dispose` them (or use the file-name overload) |
| HDR sky washed out, metal reflections glaring | Panoramas must be linear: `SetIsGammaSpace(false)` |
| Ground texture smeared at the edges | Default wrap is `ClampToEdge`; tiling materials want `Repeat` |
| The second view draws the sky again | Set that camera's `IsRenderBackground = false` |

## See it running

- **Environment**: procedurally generated equirectangular panoramas baked into cube skyboxes, plus PBR irradiance/prefiltered reflections and a manual cache rebake: [EnvironmentDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Environment/EnvironmentDemo.axaml.cs)
- **Background**: four background sources side by side (engine default solid color, cube map, HDR conversion, flat stretched image), switchable between perspective and orthographic cameras: [SkyboxBackgroundDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Background/SkyboxBackgroundDemo.axaml.cs)
- Sampling settings for material channels: [Materials and Textures](./material.md).
