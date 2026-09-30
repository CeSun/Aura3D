---
section: basics
order: 4
---

# Lighting and Shadows

Under the default pipeline, **nothing is visible without a light**. This page covers the three light types, brightness and range, shadows, and two common traps: extra lights that silently do nothing, and shadows missing a chunk.

## Shortest working setup: one sun + one bulb

Add two lights and one lit object inside `SceneInitialized`:

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;
    var scene = args.Scene;

    scene.Background = Texture.CreateFromColor(Color.Gray);

    // Directional light = the sun. It has no position; its orientation is where the light comes from
    var sun = new DirectionalLight
    {
        LightColor = Color.White,
        Irradiance = 80000,                       // lux, the default
        RotationDegrees = new Vector3(-35, -20, 0),
    };
    scene.AddNode(sun);

    // Point light = a bulb. Position + attenuation radius decide how far it reaches
    var lamp = new PointLight
    {
        LightColor = Color.White,
        LuminousIntensity = 1500,                 // candela (cd), default 1000
        AttenuationRadius = 8f,
        Position = new Vector3(2f, 2.5f, 1f),
    };
    scene.AddNode(lamp);

    var box = new Mesh { Geometry = new BoxGeometry(), Material = new Material() };
    box.Material.BaseColor = Texture.CreateFromColor(Color.White);
    box.Position = new Vector3(0, 0.5f, 0);
    scene.AddNode(box);

    var ground = new Mesh { Geometry = new PlaneGeometry(60f, 60f), Material = new Material() };
    ground.Material.BaseColor = Texture.CreateFromColor(Color.Gray);
    scene.AddNode(ground);
}
```

## Choosing between the three light types

| Light | Simulates | How it is placed | Unique parameters |
|---|---|---|---|
| `DirectionalLight` | Sun, moon, parallel light | Orientation only: light travels along the node's `Forward` (set via `RotationDegrees`) | `Irradiance` (lux) |
| `PointLight` | Bulb, torch, explosion | `Position`, emits in all directions | `AttenuationRadius`, `LuminousIntensity` (cd), `SoftRatio` |
| `SpotLight` | Flashlight, stage spot, headlight | `Position` + `RotationDegrees` for the cone axis | `InnerConeAngleDegree`, `OuterConeAngleDegree`, plus the three above |

Shared by all three: `LightColor`, `CastShadow`, and every transform property from `Node` (a light can be parented to a moving node — see [The Scene Graph and Nodes](./scene-and-nodes.md)).

> [!NOTE]
> `LightColor` and `Texture.CreateFromColor(...)` take a `System.Drawing.Color` (0..255 channels), not Avalonia's `Color`. That is why the gallery demos write `System.Drawing.Color.White` fully qualified. If your file imports both namespaces, qualify the name.

### Directional light: orientation is the direction

```csharp
var dl = new DirectionalLight();
dl.LightColor = Color.White;
dl.RotationDegrees = new Vector3(-30, -15, 0);   // light shoots out along Forward
```

The more negative the pitch (first component), the closer the light is to shining straight down. To make the sun move, update `RotationDegrees` per frame:

```csharp
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    sun.RotationDegrees = sun.RotationDegrees with { Y = sun.RotationDegrees.Y + 30f * (float)e.DeltaTime };
}
```

### Point light: the attenuation radius is its size

```csharp
var pl = new PointLight();
pl.LightColor = Color.Red;
pl.AttenuationRadius = 5f;      // default 10: light falls to zero at this distance
pl.Position = new Vector3(2, 3, 0);
```

`AttenuationRadius` is the only "how big is this bulb" knob — set it too small and the lamp only lights a patch under its feet. To switch a light off temporarily, set `Enable = false` (it stays in the scene but stops participating).

### Spot light: inner/outer cone = the penumbra

```csharp
var sp = new SpotLight();
sp.LightColor = Color.Blue;
sp.AttenuationRadius = 10f;
sp.InnerConeAngleDegree = 10f;   // default 10: fully bright inside
sp.OuterConeAngleDegree = 25f;   // default 15: nothing outside the outer cone
sp.Position = new Vector3(0, 5, 4);
sp.RotationDegrees = new Vector3(-40, 0, 0);
```

Between the inner and outer cone brightness ramps from 1.0 down to 0.0 — that gap is the soft edge (penumbra). Making the two angles equal gives a hard cut. The outer angle also defines how wide this light's shadow cone is.

## Brightness: use physical units, not a raw multiplier

The engine takes physical lighting values and converts them internally into the `Intensity` the shader uses (read-only — never set it yourself):

| Property | Unit | Default | Conversion |
|---|---|---|---|
| `DirectionalLight.Irradiance` | lux (irradiance) | 80000 | `Intensity = Irradiance * 0.00001` |
| `PointLight.LuminousIntensity`<br>`SpotLight.LuminousIntensity` | cd (candela) | 1000 | `Intensity = LuminousIntensity * 0.001` |

`LightColor` only supplies the hue (RGB channels, 0..255) and is multiplied by that intensity.

```csharp
sun.Irradiance = 120000f;        // a harsher sun
lamp.LuminousIntensity = 2000f;  // a brighter bulb
```

If the whole image is too dark or blown out, don't touch the lights first — that's tone mapping. Adjust `PipelineSettings.ToneMappingExposure` (default 0.7) and `BrightnessClamp` (default 4.0), see [Choosing and Configuring Pipelines](./pipelines.md).

`SoftRatio` (point and spot lights, default 0.9) controls **shadow softness**: lower = blurrier edges, higher = harder.

```csharp
lamp.SoftRatio = 0.7f;   // softer shadow edges
```

## Only 4 lights per type by default: trap number one

> [!WARNING]
> Under the default pipeline, directional, point and spot lights are each capped at **4 active lights**. The 5th light of a kind contributes neither illumination nor shadows — it behaves as if it were never added.

The ones that count are simply the first N in scene insertion order. To support more, raise the limits **before the pipeline is created** (valid range `1..10`):

```csharp
var view = new Aura3DView
{
    PipelineSettings = new PipelineSettings
    {
        DirectionalLightLimit = 2,   // two suns is enough; save budget for point lights
        PointLightLimit = 8,
        SpotLightLimit = 4,
    },
};
```

> [!TIP]
> Changing the limits after the pipeline exists has no effect, because they size the shader loops. With dozens of lights, keep shadow casting on only a few, or rethink the pipeline — see [Choosing and Configuring Pipelines](./pipelines.md).

## Shadows

Every light type has `CastShadow` (default `false`). When enabled, that light renders one extra depth pass "from its point of view" each frame, and the main pass uses it to decide which points are in shadow.

```csharp
sun.CastShadow = true;
lamp.CastShadow = true;
```

### What casts and what receives

**Every** mesh goes into the shadow map — opaque (`Opaque`) and alpha-cutout (`Masked`) static, skinned and instanced meshes all count.

> [!NOTE]
> `BlendMode.Translucent` materials cast no shadows. To make a leaf card cast one, use `Masked` + `AlphaCutoff`, see [Materials and Textures](./material.md).

### Clip planes and coverage

A directional light draws its shadow with an orthographic camera, and `ShadowConfig` is that box — note that `Width`/`Height` are **coverage extents in world units, not map resolution**:

```csharp
sun.ShadowConfig.Width = 50;       // default 50: how much of the scene is covered sideways
sun.ShadowConfig.Height = 50;      // default 50
sun.ShadowConfig.NearPlane = 0.1f; // default 0.1
sun.ShadowConfig.FarPlane = 50f;   // default 50
```

Anything outside that box casts nothing (which reads as a shadow clipped in half). Point and spot lights only have `NearPlane` (default 1) and `FarPlane` (default 100) in `ShadowConfig`.

### Resolution

- The CSM main directional light: resolution = `PipelineSettings.CsmShadowMapResolution` (default 1024).
- All other shadow casters (point, spot, non-main directional lights): fixed 1024×1024, currently not configurable.

> [!WARNING]
> Shadows cost real frames: one shadow-casting **point light** renders six cube-map faces, a shadow-casting **spot light** one pass, a directional light one. When FPS drops, turn off `CastShadow` on distant or off-screen objects before you raise resolution.

<a id="csm"></a>

## CSM: cascaded shadow maps stop far-field aliasing

A single directional shadow map spread over a large area gives visibly blocky shadows near the camera. CSM (Cascaded Shadow Maps) splits the camera frustum into slices, each with its own shadow map — high precision nearby, lower precision far away.

```csharp
scene.AddNode(sun);

// This directional light uses CSM; other directional lights stay on a single shadow map
scene.MainDirectionalLight = sun;
```

You can also leave it unset: if `MainDirectionalLight` is `null`, the engine auto-picks the **first light with `Enable && CastShadow`** as the main one. So clearing `MainDirectionalLight` does not disable cascading — it just hands the choice back to the auto-pick. To actually go back to a single shadow map, set `CsmCascadeCount` to 1 or stop that light from casting (`CastShadow = false`).

All CSM parameters live in `PipelineSettings`:

```csharp
var settings = new PipelineSettings
{
    CsmCascadeCount = 4,            // default 3, range 1..4; 1 = fall back to a single shadow map
    CsmSplitLambda = 0.5f,          // 0 = uniform splits, 1 = logarithmic (more precision nearby)
    CsmShadowMapResolution = 2048,  // default 1024, per cascade
};
```

| Parameter | Default | When to set it |
|---|---|---|
| `CsmCascadeCount` | 3 | Before the pipeline is created |
| `CsmShadowMapResolution` | 1024 | Before the pipeline is created |
| `CsmSplitLambda` | 0.5 | Anytime, takes effect immediately |

> [!IMPORTANT]
> CSM only applies to pipelines that declare `SupportsCSM = true`: the default BlinnPhong pipeline and both PBR pipelines (deferred and forward) do; Cel Shading, `NoLightPipeline` and the point-cloud pipeline leave it at the base default `false`, so they always use a single shadow map. On the Browser (WebGL) host cascading is disabled regardless, falling back to a single shadow map.
>
> Also: when CSM is active, the main light's coverage comes from the **camera frustum splits** — `ShadowConfig.Width/Height` only matter on the single-map path.

Runtime tweaks work too:

```csharp
view.Scene.RenderPipeline.Settings.CsmSplitLambda = 0.8f;
```

For pipeline selection, the rest of `PipelineSettings`, and debug visualization, continue the chain in [Choosing and Configuring Pipelines](./pipelines.md).

## When you cannot see where a light is: debug drawing

```csharp
var debug = view.Scene.RenderPipeline.Settings.Debug;
debug.Enable = true;
debug.ShowDirectionalLight = true;   // directional light direction lines
debug.ShowPointLight = true;         // point light range spheres (instantly shows a too-small AttenuationRadius)
debug.ShowSpotLight = true;          // spot light cones (see inner/outer angles)
debug.ShowBoundingBox = true;        // mesh bounding boxes
```

This costs extra frames — keep it on during development only.

## Common pitfalls

| Symptom | Cause and fix |
|---|---|
| Everything is black, or you only see the background | Under the default pipeline **objects without light are not shown**. Add a `DirectionalLight` first. The background ignores lighting — see [Environment and Background](./environment.md) |
| A 5th light of the same kind does nothing | 4 per type by default; extras get no light and no shadow. Raise `*LightLimit` before pipeline creation |
| Shadows clipped in half / nothing shadows in the distance | Directional `ShadowConfig.Width/Height/FarPlane` don't cover the scene; or that light isn't on the CSM path (check `MainDirectionalLight` and `CastShadow`) |
| Stepped/jagged shadow edges close to the camera | Raise `CsmShadowMapResolution` and `CsmCascadeCount`, or push `CsmSplitLambda` up to move precision nearer |
| Changing `ShadowConfig` does nothing once CSM is on | Expected: cascade coverage follows the camera frustum; `Width/Height` serve the single-map path only |
| Translucent objects cast no shadows | By design: the shadow map collects `Opaque` / `Masked` materials only |
| FPS drops as soon as shadows are on | Audit `CastShadow` per light; shadow-casting point lights are the most expensive (six faces) |
| Light-limit changes have no effect | `*LightLimit`, `CsmCascadeCount` and `CsmShadowMapResolution` must be set before the pipeline is created |

## See it running

- The **Shadows** demo shows both shadow paths side by side (a CSM main light plus a single-map directional light), exposes every parameter live, and reads back the resolution and cascade count actually in effect from GPU state: [ShadowsDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Shadows/ShadowsDemo.axaml.cs)
- Black models have other causes too — see [Common Pitfalls and Troubleshooting](./troubleshooting.md).
