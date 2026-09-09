# Particle System

Aura3D's particle system uses **CPU simulation + GPU instanced rendering**, supporting two rendering modes per emitter: billboard mode and mesh mode.

Each `ParticleEmitter` owns its own particle array, GPU buffer, and rendering resources (texture, mesh, blend mode). A `ParticleSystem` can host multiple emitters with different visual configurations — for example, an explosion with opaque debris and translucent smoke in the same system.

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                     ParticleSystem (Node)                     │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                    Emitters[]                            │ │
│  │  ┌──────────────────────┐  ┌──────────────────────────┐ │ │
│  │  │  ParticleEmitter[0]  │  │  ParticleEmitter[1]      │ │ │
│  │  │  ├── Texture         │  │  ├── Mesh                │ │ │
│  │  │  ├── FlipbookTiles   │  │  ├── Material            │ │ │
│  │  │  ├── BlendMode       │  │  ├── BlendMode (Opaque)  │ │ │
│  │  │  ├── Particles[]     │  │  ├── Particles[]         │ │ │
│  │  │  ├── GpuBuffer       │  │  ├── InstancedMesh       │ │ │
│  │  │  └── (sim params)    │  │  └── (sim params)        │ │ │
│  │  └──────────────────────┘  └──────────────────────────┘ │ │
│  └─────────────────────────────────────────────────────────┘ │
│  System-level: Position, VisibilityCulling, Play/Stop/Pause  │
└──────────────────────────────────────────────────────────────┘
```

- **CPU Simulation**: Each emitter's particles run through `ParticleSimulation.Update()` independently.
- **GPU Instanced Rendering**: Each emitter owns its own `ParticleGpuBuffer` (billboard) or `InstancedMesh` child node (mesh).
- **Per-emitter resources**: Texture, flipbook, mesh, material, and blend mode are all per-emitter — different emitters in the same system can use completely different rendering.

---

## Quick Start

### Billboard Mode (Default)

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

### Mesh Mode

```csharp
private ParticleSystem? _debris;

private void CreateDebrisSystem(Aura3DView view)
{
    _debris = new ParticleSystem
    {
        Name = "Debris",
        Position = new Vector3(0, 3, 0),
        EnableVisibilityCulling = true,       // Optional: skip sim when off-screen
    };

    var emitter = new ParticleEmitter
    {
        MaxParticles = 2000,
        BlendMode = BlendMode.Opaque,
        Mesh = Mesh.FromFile("debris.glb"),       // Mesh to instance (per-emitter)
        // Material = someMaterial,               // Optional: override material

        EmissionRate = 100f,
        Shape = EmissionShape.Hemisphere,
        ShapeSize = new Vector3(2, 2, 2),

        Lifetime = new RangeFloat(2f, 5f),
        StartSize = new RangeFloat(0.1f, 0.3f),
        EndSize = new RangeFloat(0.05f, 0.1f),

        Velocity = new RangeVector3(
            new Vector3(-2, 5, -2),
            new Vector3(2, 10, 2)),

        StartColor = Color.Gray,
        EndColor = Color.DarkGray,

        Gravity = new Vector3(0, -12f, 0),
        Damping = 2f,
        MeshScale = 1.5f,
    };
    _debris.Emitters.Add(emitter);

    view.AddNode(_debris);
    _debris.Play();
}
```

---

## Core Classes

### ParticleSystem

The main scene node. Inherits from `Node`. Manages lifecycle and shared resources — rendering properties are on `ParticleEmitter`.

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxParticles` | `int` | `10000` | System-level capacity hint. Only settable when not playing. |
| `Emitters` | `List<ParticleEmitter>` | `new()` | Emitter configurations and runtime state. |
| `CustomBoundingBox` | `BoundingBox?` | `null` | Custom world-space bounding box override. |
| `EnableVisibilityCulling` | `bool` | `false` | Skip simulation when outside camera frustum. |

| Read-only Property | Type | Description |
|---|---|---|
| `IsPlaying` | `bool` | Whether the system is currently playing. |
| `ActiveCount` | `int` | Sum of alive particles across all emitters. |
| `WorldBoundingBox` | `BoundingBox?` | Current world-space bounding box. |

| Method | Description |
|---|---|
| `Play()` | Allocates per-emitter particle arrays, creates GPU buffers and InstancedMeshes, starts simulation. |
| `Stop()` | Stops simulation, releases all per-emitter resources and child nodes. |
| `Pause()` | Toggles pause state. |

### ParticleEmitter

Each emitter owns its particles, rendering resources, and runtime state.

#### Rendering Settings

| Property | Type | Default | Description |
|---|---|---|---|
| `BlendMode` | `BlendMode` | `Translucent` | Rendering blend mode for this emitter. |
| `Texture` | `ITexture?` | `null` | Billboard texture. If null, a procedural circle is drawn. |
| `FlipbookTiles` | `Vector2` | `(1,1)` | Flipbook grid dimensions, e.g. `(8,8)` for 64 frames. |
| `Mesh` | `Mesh?` | `null` | When set, activates mesh mode for this emitter. |
| `Material` | `Material?` | `null` | Optional material override for mesh mode. |
| `MaxParticles` | `int` | `1000` | Max particle count for this emitter. |

#### Emission Settings

| Property | Type | Default | Description |
|---|---|---|---|
| `EmissionRate` | `float` | `100` | Particles emitted per second. |
| `Shape` | `EmissionShape` | `Point` | Emission shape. See [Emission Shapes](#emission-shapes). |
| `ShapeSize` | `Vector3` | `(1,1,1)` | Size of the emission shape (scale per axis). |
| `ConeAngle` | `float` | `30` | Cone spread angle in degrees (only for `Cone` shape). |
| `Looping` | `bool` | `true` | When `false`, emission stops after `Duration`. |
| `Duration` | `float` | `0` | Emission duration in seconds (only when `Looping = false`). |

#### Particle Properties

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

#### Physics

| Property | Type | Default | Description |
|---|---|---|---|
| `Gravity` | `Vector3` | `(0, -9.8, 0)` | Gravity applied to particles. Positive Y = upward. |
| `Damping` | `float` | `0` | Velocity damping factor. |

Values are validated when assigned: rates, duration, damping, and shape dimensions must be finite and non-negative; lifetime must be positive; ranges must be finite with `Min <= Max`; `ConeAngle` accepts `[0, 90)` degrees; `MaxParticles` and `MeshScale` must be positive; and both `FlipbookTiles` components must be positive integers. Unknown enum values are rejected as well.

#### Mesh Mode Only

| Property | Type | Default | Description |
|---|---|---|---|
| `MeshScale` | `float` | `1` | Scale multiplier applied on top of per-particle size. |

#### Runtime State (read-only)

| Property | Type | Description |
|---|---|---|
| `ElapsedTime` | `float` | Time since Play. |
| `IsFinished` | `bool` | `true` when non-looping and elapsed >= duration. |
| `UseMeshRenderer` | `bool` | `true` if `Mesh` is set. |

### Range Types

```csharp
// Float range
new RangeFloat(min, max);

// Vector3 range (per-component random)
new RangeVector3(min, max);
new RangeVector3(minX, minY, minZ, maxX, maxY, maxZ);
```

---

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

### Shape Selection Guide

```
Point          → Precise single-point emission (bullets, sparks from fixed point)
Sphere         → Volumetric emission (explosions, magical auras)
SphereSurface  → Shell emission (expanding shockwaves)
Box            → Rectangular area emission (rain, snow)
Cone           → Directional spray (flamethrower, water spray)
Circle         → Flat disc emission (campfire base, fountain)
Hemisphere     → Upward burst (debris explosion, dust kick-up)
```

---

## Configuration Guide

### Multi-Emitter Systems with Different Rendering

Each emitter can have its own texture, mesh, and blend mode. This enables mixed effects in a single system:

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

### Color Interpolation

Colors are interpolated linearly over the particle's lifetime:

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

### Size Over Lifetime

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

### Physics Tuning

Each emitter has independent gravity and damping:

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

### Flipbook Textures

```csharp
emitter.Texture = Texture.CreateFromFile("fire_flipbook.png");
emitter.FlipbookTiles = new Vector2(8, 8);   // 8 columns × 8 rows = 64 frames
```

The fragment shader selects frames based on `AgeRatio`: 0% → frame 0, 50% → frame 32, 99% → frame 63.

---

## Mesh Mode

When `emitter.Mesh` is set, that emitter's particles render as 3D mesh instances.

### How It Works

1. On `Play()`, each mesh-mode emitter creates its own child `InstancedMesh`.
2. Each frame, particle transforms are computed as `Scale × Rotation × Translation`.
3. The child `InstancedMesh` receives all transforms via `SetInstances()`.
4. Rendering flows through the normal mesh pipeline.

### Particle Orientation in Mesh Mode

```csharp
// Spin around Y axis
emitter.Rotation = new RangeFloat(0, MathF.PI * 2);
emitter.AngularVelocity = new RangeFloat(-2f, 2f);
```

Final rotation = system world rotation ∘ particle Y-axis spin.

### Mesh Scale

```csharp
emitter.MeshScale = 2f;   // Mesh is twice as large
```

Final scale = `particle.CurrentSize × emitter.MeshScale`.

---

## Rendering Details

### Blend Modes (Per-Emitter)

Each emitter specifies its own `BlendMode`. The `ParticlePass` renders emitters in order:

| BlendMode | Use Case | Behavior |
|---|---|---|
| `Opaque` | Solid particles (debris, mesh mode) | Depth write on, no blending |
| `Masked` | Particles with hard edges | Depth write on, alpha test |
| `Translucent` | Soft particles (fire, smoke) | Premultiplied alpha, depth write off, back-to-front sorted |

### Rendering Order (Billboard Mode)

1. **Opaque** emitters rendered first.
2. **Masked** emitters rendered next.
3. **Translucent** emitters rendered last, sorted back-to-front by system center distance.
   - Within each translucent emitter, particles are sorted back-to-front by distance to camera.

### Shader Variants (Per-Emitter)

Each emitter selects its own shader variant:

| Config | Defines |
|---|---|
| No texture | (none) — procedural circle |
| Texture only | `PARTICLE_TEXTURE` |
| Texture + Flipbook | `PARTICLE_TEXTURE`, `PARTICLE_FLIPBOOK` |

### Procedural Circle (No Texture)

When `emitter.Texture` is null, the fragment shader draws a soft circle using `smoothstep`.

---

## Lifecycle Management

```csharp
// Start
ps.Play();

// Pause / Resume
ps.Pause();

// Stop
ps.Stop();

// Runtime parameter changes (allowed while playing)
ps.Emitters[0].EmissionRate = 500f;
ps.Emitters[0].StartColor = Color.Red;
ps.Emitters[0].Texture = newTexture;   // Takes effect next frame
```

---

## Performance

### Optimization Tips

1. **Set appropriate per-emitter MaxParticles** — arrays are allocated on `Play()`.
2. **Prefer billboard mode** for high-count systems — lighter GPU load.
3. **Use visibility culling** — `ps.EnableVisibilityCulling = true`.
4. **Set CustomBoundingBox** for tightly constrained systems.
5. **Use short lifetimes** — lower steady-state active count.
6. **Batch emitters with shared textures/meshes** where possible.

### Performance Monitoring

```csharp
int total = ps.ActiveCount;  // Sum across all emitters
foreach (var em in ps.Emitters)
    Console.WriteLine($"{em.ActiveCount} / {em.MaxParticles}");
```

---

## Debug Visualization

```csharp
debugSettings.ShowParticleBounds = true;
```

Draws orange wireframe bounding boxes around all active particle systems.

---

## Complete Example: Fire with Sparks (Multi-Texture)

Two emitters sharing the same flipbook texture, with different simulation parameters:

```csharp
private ParticleSystem? _fire;

private void CreateFire(Aura3DView view)
{
    var fireTex = Texture.CreateFromFile("Assets/fire_8x8.png");

    _fire = new ParticleSystem
    {
        Name = "Fire",
        Position = new Vector3(0, 0, 0),
    };

    // Main flame
    _fire.Emitters.Add(new ParticleEmitter
    {
        MaxParticles = 4000,
        BlendMode = BlendMode.Translucent,
        Texture = fireTex,
        FlipbookTiles = new Vector2(8, 8),

        EmissionRate = 300f,
        Shape = EmissionShape.Circle,
        ShapeSize = new Vector3(1.5f, 0, 1.5f),
        Lifetime = new RangeFloat(0.5f, 1.5f),
        StartSize = new RangeFloat(0.3f, 0.6f),
        EndSize = new RangeFloat(0.01f, 0.1f),
        Velocity = new RangeVector3(new(-0.3f, 3f, -0.3f), new(0.3f, 6f, 0.3f)),
        StartColor = Color.Orange,
        EndColor = Color.Transparent,
        Gravity = new Vector3(0, 2f, 0),
        Damping = 0.3f,
    });

    // Sparks
    _fire.Emitters.Add(new ParticleEmitter
    {
        MaxParticles = 1000,
        BlendMode = BlendMode.Translucent,
        Texture = fireTex,
        FlipbookTiles = new Vector2(8, 8),

        EmissionRate = 60f,
        Shape = EmissionShape.Cone,
        ShapeSize = new Vector3(0.3f, 2f, 0.3f),
        ConeAngle = 15f,
        Lifetime = new RangeFloat(0.8f, 2.5f),
        StartSize = new RangeFloat(0.05f, 0.12f),
        EndSize = new RangeFloat(0.01f, 0.03f),
        Velocity = new RangeVector3(new(-1f, 5f, -1f), new(1f, 10f, 1f)),
        StartColor = Color.Yellow,
        EndColor = Color.Red,
        Gravity = new Vector3(0, -1f, 0),
        Damping = 0.1f,
    });

    view.AddNode(_fire);
    _fire.Play();
    view.AutoRequestNextFrameRendering = true;
}
```

---

## Complete Example: Explosion (Mixed Opaque Mesh + Translucent Billboard)

One system, two emitters with completely different rendering:

```csharp
private ParticleSystem? _explosion;

private void CreateExplosion(Aura3DView view, Vector3 position)
{
    _explosion = new ParticleSystem
    {
        Name = "Explosion",
        Position = position,
    };

    // Opaque debris (mesh mode) — one-shot burst
    _explosion.Emitters.Add(new ParticleEmitter
    {
        MaxParticles = 500,
        BlendMode = BlendMode.Opaque,
        Mesh = Mesh.FromFile("Assets/debris.glb"),

        EmissionRate = 800f,
        Looping = false,
        Duration = 0.2f,
        Shape = EmissionShape.Hemisphere,
        ShapeSize = new Vector3(1, 1, 1),
        Lifetime = new RangeFloat(2f, 4f),
        StartSize = new RangeFloat(0.1f, 0.4f),
        EndSize = new RangeFloat(0.05f, 0.1f),
        Velocity = new RangeVector3(new(-5, 8, -5), new(5, 15, 5)),
        StartColor = Color.LightGray,
        EndColor = Color.DarkGray,
        Gravity = new Vector3(0, -15f, 0),
        Damping = 1.5f,
        MeshScale = 1.2f,
        Rotation = new RangeFloat(0, MathF.PI * 2),
        AngularVelocity = new RangeFloat(-3f, 3f),
    });

    // Translucent smoke (billboard) — looping
    var smokeTex = Texture.CreateFromFile("Assets/smoke.png");
    _explosion.Emitters.Add(new ParticleEmitter
    {
        MaxParticles = 300,
        BlendMode = BlendMode.Translucent,
        Texture = smokeTex,

        EmissionRate = 50f,
        Shape = EmissionShape.Circle,
        ShapeSize = new Vector3(3, 0, 3),
        Lifetime = new RangeFloat(2f, 6f),
        StartSize = new RangeFloat(0.5f, 1.5f),
        EndSize = new RangeFloat(0.01f, 0.1f),
        Velocity = new RangeVector3(new(-1f, 1f, -1f), new(1f, 3f, 1f)),
        StartColor = Color.FromArgb(128, 180, 160, 140),
        EndColor = Color.Transparent,
        Gravity = new Vector3(0, -1f, 0),
        Damping = 2f,
    });

    view.AddNode(_explosion);
    _explosion.Play();
}
```

---

## ParticlePass Global Settings

| Property | Type | Default | Description |
|---|---|---|---|
| `DefaultParticleSize` | `float` | `1.0` | Reserved (not used in current shader). |
| `GlobalAlpha` | `float` | `1.0` | Global alpha multiplier for all billboard particles. |

```csharp
var particlePass = renderPipeline.FindPass<ParticlePass>();
particlePass.GlobalAlpha = 0.5f;
```

---

## Troubleshooting

| Problem | Likely Cause | Solution |
|---|---|---|
| No particles visible | `Play()` not called | Call `Play()` after setup. |
| Particles not animating | `AutoRequestNextFrameRendering` not set | Set `view.AutoRequestNextFrameRendering = true`. |
| All particles at origin | `ShapeSize` too small | Set meaningful `ShapeSize` for the chosen shape. |
| Particles don't move | `Velocity` set to zero | Set non-zero velocity or use `Gravity`. |
| Mesh mode: black meshes | Material missing | Check `emitter.Mesh.Material` or set `emitter.Material`. |
| Translucent artifacts | Wrong blend mode | Set `emitter.BlendMode = BlendMode.Translucent`. |
| Flipbook not animated | `FlipbookTiles` not set | Set both `emitter.Texture` and `emitter.FlipbookTiles`. |
| Culling not working | `EnableVisibilityCulling = false` | Set to `true` on the ParticleSystem. |
| Wrong mesh scale | `MeshScale` not set | Set `emitter.MeshScale` to desired multiplier. |
