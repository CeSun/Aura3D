---
section: basics
order: 8
---

# Particle System

This page answers one question: **how to build fire, smoke, rain/snow, debris and other particle effects in your scene**. Aura3D's particle system uses CPU simulation + GPU instanced rendering, and each emitter supports two rendering modes: billboard (default) and mesh mode. The hierarchy has only two levels: `ParticleSystem` is a scene node managing position and play/stop; under it sit `ParticleEmitter`s managing emission shape, particle properties, and rendering appearance.

> [!IMPORTANT]
> **Texture, flipbook, mesh, material, and blend mode are all per-emitter, not per-system.** Different emitters in the same `ParticleSystem` can render completely differently — for example, an explosion where opaque debris (mesh + `Opaque`) and translucent smoke (texture + `Translucent`) coexist in one system.

## Creating a Particle Effect (Shortest Path)

A fire example: create the system node → add an emitter with its own texture and blend mode → add to the scene and `Play()`.

```csharp
private ParticleSystem? _particles;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // 1. Create the particle system (no rendering resources here)
    _particles = new ParticleSystem
    {
        Name = "Fire",
        Position = new Vector3(0, 0, 0),
    };

    // 2. Add an emitter with its own texture and blend mode
    var emitter = new ParticleEmitter
    {
        MaxParticles = 5000,
        BlendMode = BlendMode.Translucent,
        Texture = Texture.CreateFromFile("fire.png"),

        EmissionRate = 200f,
        Shape = EmissionShape.Circle,
        ShapeSize = new Vector3(2, 0, 2),

        Lifetime = new RangeFloat(1f, 3f),
        StartSize = new RangeFloat(0.3f, 0.6f),
        EndSize = new RangeFloat(0.01f, 0.05f),

        Velocity = new RangeVector3(
            new Vector3(-0.5f, 3f, -0.5f),
            new Vector3(0.5f, 8f, 0.5f)),

        StartColor = Color.Orange,
        EndColor = Color.Transparent,

        Gravity = new Vector3(0, 2f, 0),
        Damping = 0.5f,
    };
    _particles.Emitters.Add(emitter);

    // 3. Add to scene and play
    view.AddNode(_particles);
    _particles.Play();

    view.AutoRequestNextFrameRendering = true;
}
```

> [!NOTE]
> Particles simulate every frame, so keep the view rendering continuously: `view.AutoRequestNextFrameRendering = true` (or call `RequestNextFrameRendering()` each frame).

With no `Texture` set, the fragment shader draws a soft procedural circle via `smoothstep` — you can iterate on parameters without any texture file.

## Emission Shapes

Seven built-in emission shapes. All shapes are defined in **local space** (relative to the `ParticleSystem` node position), and emitted positions/velocities are transformed by the node's world rotation.

| Shape | Description | ShapeSize Meaning |
|---|---|---|
| `Point` | Single point at origin. | Ignored |
| `Sphere` | Uniform volume inside a sphere. | `(X,Y,Z)` = sphere radii |
| `SphereSurface` | Uniform on sphere surface. | `(X,Y,Z)` = sphere radii |
| `Box` | Uniform volume inside an axis-aligned box. | `(X,Y,Z)` = box extents |
| `Cone` | Cone along +Y axis, with spread angle. | `X` = base radius, `Y` = height |
| `Circle` | Uniform on an XZ disc (Y=0 plane). | `(X,0,Z)` = disc radii |
| `Hemisphere` | Uniform volume in upper hemisphere (Y ≥ 0). | `(X,Y,Z)` = hemisphere radii |

Picking a shape by effect:

```
Point          → Precise single-point emission (bullets, sparks from fixed point)
Sphere         → Volumetric emission (explosions, magical auras)
SphereSurface  → Shell emission (expanding shockwaves)
Box            → Rectangular area emission (rain, snow)
Cone           → Directional spray (flamethrower, fountain)
Circle         → Flat disc emission (campfire base, fountain)
Hemisphere     → Upward burst (debris explosion, dust kick-up)
```

## Common Parameters at a Glance

`ParticleSystem` (system level: position and lifecycle):

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxParticles` | `int` | `10000` | System-level capacity hint. Only settable when not playing. |
| `Emitters` | `List<ParticleEmitter>` | `new()` | Emitter configurations and runtime state. |
| `CustomBoundingBox` | `BoundingBox?` | `null` | Custom world-space bounding box override. |
| `EnableVisibilityCulling` | `bool` | `false` | Skip simulation when outside camera frustum. |

Read-only: `IsPlaying` (currently playing), `ActiveCount` (sum of alive particles across all emitters), `WorldBoundingBox` (current world-space bounding box).

`ParticleEmitter` (emitter level: appearance and simulation) — rendering settings:

| Property | Type | Default | Description |
|---|---|---|---|
| `BlendMode` | `BlendMode` | `Translucent` | Rendering blend mode for this emitter. |
| `Texture` | `ITexture?` | `null` | Billboard texture. If null, a procedural circle is drawn. |
| `FlipbookTiles` | `Vector2` | `(1,1)` | Flipbook grid dimensions, e.g. `(8,8)` for 64 frames. |
| `Mesh` | `Mesh?` | `null` | When set, activates mesh mode for this emitter. |
| `Material` | `Material?` | `null` | Optional material override for mesh mode. |
| `MaxParticles` | `int` | `1000` | Max particle count for this emitter. |

Emission settings:

| Property | Type | Default | Description |
|---|---|---|---|
| `EmissionRate` | `float` | `100` | Particles emitted per second. |
| `Shape` | `EmissionShape` | `Point` | Emission shape, see [Emission Shapes](#emission-shapes). |
| `ShapeSize` | `Vector3` | `(1,1,1)` | Size of the emission shape (scale per axis). |
| `ConeAngle` | `float` | `30` | Cone spread angle in degrees (only for `Cone` shape). |
| `Looping` | `bool` | `true` | When `false`, emission stops after `Duration`. |
| `Duration` | `float` | `0` | Emission duration in seconds (only when `Looping = false`). |

Particle properties:

| Property | Type | Default | Description |
|---|---|---|---|
| `Lifetime` | `RangeFloat` | `(1, 3)` | Particle lifetime range in seconds. |
| `Velocity` | `RangeVector3` | `(0,5,0)~(0,10,0)` | Initial velocity range (local space). |
| `StartSize` | `RangeFloat` | `(0.1, 0.3)` | Initial size range. |
| `EndSize` | `RangeFloat` | `(0.01, 0.05)` | Final size range (lerped over lifetime). |
| `StartColor` | `Color` | `White` | Initial color. |
| `EndColor` | `Color` | `Transparent` | Final color (lerped over lifetime). |
| `Rotation` | `RangeFloat` | `(0, 2π)` | Initial rotation range (radians). |
| `AngularVelocity` | `RangeFloat` | `(-1, 1)` | Angular velocity range (radians/sec). |

Physics:

| Property | Type | Default | Description |
|---|---|---|---|
| `Gravity` | `Vector3` | `(0, -9.8, 0)` | Gravity applied to particles. Positive Y = upward. |
| `Damping` | `float` | `0` | Velocity damping factor. |

Mesh mode only: `MeshScale` (`float`, default `1`, scale multiplier applied on top of per-particle size). Runtime read-only state: `ElapsedTime` (time since Play), `IsFinished` (`true` when non-looping and elapsed >= duration), `UseMeshRenderer` (`true` if `Mesh` is set).

Values are validated when assigned: rates, duration, damping, and shape dimensions must be finite and non-negative; lifetime must be positive; ranges must be finite with `Min <= Max`; `ConeAngle` accepts `[0, 90)` degrees; `MaxParticles` and `MeshScale` must be positive; and both `FlipbookTiles` components must be positive integers. Unknown enum values are rejected as well.

### Range Types

Most particle properties accept random ranges rather than fixed values:

```csharp
// Float range
new RangeFloat(min, max);

// Vector3 range (per-component random)
new RangeVector3(min, max);
new RangeVector3(minX, minY, minZ, maxX, maxY, maxZ);
```

## Tuning Appearance: Color, Size and Physics

Colors are interpolated linearly over the particle's lifetime from `StartColor` to `EndColor`:

```csharp
// Fade out (most common)
emitter.StartColor = Color.White;
emitter.EndColor = Color.Transparent;

// Color shift (fire: orange → red)
emitter.StartColor = Color.Orange;
emitter.EndColor = Color.Red;

// Constant color
emitter.StartColor = Color.Cyan;
emitter.EndColor = Color.Cyan;
```

Size likewise lerps from `StartSize` to `EndSize`:

```csharp
// Shrinking (fire/smoke)
emitter.StartSize = new RangeFloat(0.3f, 0.6f);
emitter.EndSize = new RangeFloat(0.01f, 0.05f);

// Growing (expanding effects)
emitter.StartSize = new RangeFloat(0.01f, 0.03f);
emitter.EndSize = new RangeFloat(0.3f, 0.5f);

// Constant size
emitter.StartSize = new RangeFloat(0.2f, 0.2f);
emitter.EndSize = new RangeFloat(0.2f, 0.2f);
```

Each emitter has **independent** gravity and damping:

```csharp
// Lightweight floating particles (smoke)
emitter.Gravity = new Vector3(0, 0.5f, 0);
emitter.Damping = 0.8f;

// Heavy debris
emitter.Gravity = new Vector3(0, -15f, 0);
emitter.Damping = 0.2f;

// Zero-G space particles
emitter.Gravity = Vector3.Zero;
emitter.Damping = 0f;
```

### Looping vs One-Shot

```csharp
// Looping (default) — emits continuously
emitter.Looping = true;

// One-shot burst — emits for Duration then stops
emitter.Looping = false;
emitter.Duration = 2.0f;
emitter.EmissionRate = 500f;   // 1000 particles total

// Check if finished
if (emitter.IsFinished) { /* ... */ }
```

### Flipbook Textures

```csharp
emitter.Texture = Texture.CreateFromFile("fire_flipbook.png");
emitter.FlipbookTiles = new Vector2(8, 8);   // 8 columns × 8 rows = 64 frames
```

The fragment shader selects frames based on the particle's age ratio (`AgeRatio`): 0% → frame 0, 50% → frame 32, 99% → frame 63. For a single-frame texture keep `FlipbookTiles = (1,1)`, otherwise the image gets sliced into tiles.

### Multi-Emitter Mixed Looks

Each emitter can have its own texture, mesh, and blend mode, so one system can compose a mixed effect (explosion = opaque debris + translucent smoke):

```csharp
var ps = new ParticleSystem { Position = new Vector3(0, 0, 0) };

// Opaque debris emitter (mesh mode)
ps.Emitters.Add(new ParticleEmitter
{
    MaxParticles = 500,
    BlendMode = BlendMode.Opaque,
    Mesh = Mesh.FromFile("debris.glb"),
    Shape = EmissionShape.Hemisphere,
    ShapeSize = new Vector3(1, 1, 1),
    EmissionRate = 200,
    Looping = false,
    Duration = 0.3f,
    Lifetime = new RangeFloat(1f, 3f),
    StartSize = new RangeFloat(0.2f, 0.5f),
    EndSize = new RangeFloat(0.1f, 0.3f),
    Velocity = new RangeVector3(new(-5, 8, -5), new(5, 15, 5)),
    Gravity = new Vector3(0, -15f, 0),
    Damping = 1.5f,
});

// Translucent smoke emitter (billboard mode)
ps.Emitters.Add(new ParticleEmitter
{
    MaxParticles = 300,
    BlendMode = BlendMode.Translucent,
    Texture = Texture.CreateFromFile("smoke.png"),
    Shape = EmissionShape.Circle,
    ShapeSize = new Vector3(2, 0, 2),
    EmissionRate = 50,
    Lifetime = new RangeFloat(2f, 6f),
    StartSize = new RangeFloat(0.5f, 1.5f),
    EndSize = new RangeFloat(0.01f, 0.1f),
    Velocity = new RangeVector3(new(-1, 1, -1), new(1, 3, 1)),
    StartColor = Color.FromArgb(128, 180, 180, 180),
    EndColor = Color.Transparent,
    Gravity = new Vector3(0, -1f, 0),
    Damping = 2f,
});
```

## Mesh Mode

When an emitter's `Mesh` is non-null, that emitter's particles render as 3D mesh instances (the engine creates a child `InstancedMesh` for it, and rendering flows through the normal mesh pipeline). No model file on hand? Inline a primitive-geometry mesh:

```csharp
var emitter = new ParticleEmitter
{
    Mesh = new Mesh
    {
        Name = "DebrisChunk",
        Geometry = new BoxGeometry(0.3f, 0.3f, 0.3f),
        Material = someMaterial,
    },
    MeshScale = 1f,
    // ...
};
```

Mesh particle orientation is controlled by the rotation parameters:

```csharp
// Spin around Y axis
emitter.Rotation = new RangeFloat(0, MathF.PI * 2);
emitter.AngularVelocity = new RangeFloat(-2f, 2f);
```

Final rotation = system world rotation ∘ particle Y-axis spin; final scale = `particle.CurrentSize × emitter.MeshScale`.

## Playback Lifecycle

```csharp
// Start: allocates per-emitter particle arrays, creates GPU buffers and InstancedMeshes, begins simulation
ps.Play();

// Pause / resume (toggles pause state)
ps.Pause();

// Stop: halts simulation, releases all per-emitter resources and child nodes
ps.Stop();

// Runtime parameter changes (allowed while playing)
ps.Emitters[0].EmissionRate = 500f;
ps.Emitters[0].StartColor = Color.Red;
ps.Emitters[0].Texture = newTexture;   // Takes effect next frame

// Replay a one-shot burst: Stop then Play
burst.Stop();
burst.Play();
```

Note: the system-level `MaxParticles` is only settable while not playing; emitter-level parameters (rate, color, texture, ...) can be changed at any time during playback.

## Rendering Behavior (Good to Know)

`ParticlePass` renders emitters grouped by each emitter's own `BlendMode`:

| BlendMode | Use Case | Behavior |
|---|---|---|
| `Opaque` | Solid particles (debris, mesh mode) | Depth write on, no blending |
| `Masked` | Particles with hard edges | Depth write on, alpha test |
| `Translucent` | Soft particles (fire, smoke) | Premultiplied alpha, depth write off, back-to-front sorted |

In billboard mode the rendering order is Opaque → Masked → Translucent (translucent emitters sorted back-to-front by system center distance; within each translucent emitter, particles sorted by distance to camera). Each emitter also selects its own shader variant independently: no texture (procedural circle) / texture only (`PARTICLE_TEXTURE`) / texture + flipbook (`PARTICLE_TEXTURE` + `PARTICLE_FLIPBOOK`).

## Performance

1. **Set appropriate per-emitter MaxParticles** — arrays are allocated on `Play()`.
2. **Prefer billboard mode** for high-count systems — lighter GPU load.
3. **Use visibility culling** — `ps.EnableVisibilityCulling = true` skips simulation when off-screen.
4. **Set CustomBoundingBox** for tightly constrained systems.
5. **Use short lifetimes** — lower steady-state active count.
6. **Share textures/meshes across emitters** where possible.

Monitoring active particle counts:

```csharp
int total = ps.ActiveCount;  // Sum across all emitters
foreach (var em in ps.Emitters)
    Console.WriteLine($"{em.ActiveCount} / {em.MaxParticles}");
```

For debugging, draw orange wireframe bounding boxes around all active particle systems:

```csharp
view.Scene.RenderPipeline.Settings.Debug.ShowParticleBounds = true;
```

## ParticlePass Global Settings

| Property | Type | Default | Description |
|---|---|---|---|
| `DefaultParticleSize` | `float` | `1.0` | Reserved (not used in current shader). |
| `GlobalAlpha` | `float` | `1.0` | Global alpha multiplier for all billboard particles. |

```csharp
var particlePass = renderPipeline.FindPass<ParticlePass>();
particlePass.GlobalAlpha = 0.5f;
```

See [./pipelines.md](./pipelines.md) for pipeline selection and configuration.

## Common Issues

| Problem | Likely Cause | Solution |
|---|---|---|
| No particles visible | `Play()` not called | Call `Play()` after setup. |
| All particles crowded at origin | `ShapeSize` too small | Set meaningful `ShapeSize` for the chosen shape. |
| Particles don't move | `Velocity` set to zero | Set non-zero velocity or use `Gravity`. |
| Mesh mode: black meshes | Material missing | Check `emitter.Mesh.Material` or set `emitter.Material`. |
| Translucent artifacts | Wrong blend mode | Set `emitter.BlendMode = BlendMode.Translucent`. |
| Flipbook not animated | `FlipbookTiles` not set | Set both `emitter.Texture` and `emitter.FlipbookTiles`. |
| Culling not working | `EnableVisibilityCulling = false` | Set to `true` on the ParticleSystem. |
| Wrong mesh scale | `MeshScale` not set | Set `emitter.MeshScale` to desired multiplier. |

If the whole picture never refreshes (particles frozen on frame one), `AutoRequestNextFrameRendering` is usually off; for more general pitfalls see [./troubleshooting.md](./troubleshooting.md).

## Runnable Example

Three emitters, three rendering paths — a fountain (looping `Cone` + flipbook), a one-shot burst (`SphereSurface` + `Looping=false`), and mesh debris (instanced `Box` geometry):

- <https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Particles/ParticlesDemo.axaml.cs>

Core implementation sources: [ParticleSystem](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/ParticleSystem.cs), [ParticleEmitter](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Particles/ParticleEmitter.cs).
