# Get Started

## Installation

Aura3D provides multiple NuGet packages. Install what you need. The main entry point is `Aura3D.Avalonia`.

### Base Installation

```shell
dotnet add package Aura3D.Avalonia
```

This automatically pulls in `Aura3D.Core`, which includes the default rendering pipeline (BlinnPhong) and base functionality.

### Optional Extension Packages

```shell
# glTF/GLB model loading
dotnet add package Aura3D.Model.GltfLoader

# Assimp multi-format loading (FBX, OBJ, 3DS, etc. — 50+ formats)
dotnet add package Aura3D.Model.AssimpLoader

# PBR deferred pipeline
dotnet add package Aura3D.Pipeline.PBR

# Cel shading pipeline
dotnet add package Aura3D.Pipeline.CelShading
```

## Basic Usage

### Declare the Control in XAML

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    ...>
    <a:Aura3DView
        x:Name="aura3Dview"
        SceneInitialized="OnSceneInitialized"
        SceneUpdated="OnSceneUpdated"/>
</Window>
```

- `SceneInitialized` — Fires after OpenGL initialization. Build your scene here.
- `SceneUpdated` — Fires before each frame render. Args include `DeltaTime` (seconds). Update logic here.

### iOS / macOS Configuration

On iOS and macOS, specify OpenGL rendering mode in `AppBuilder`:

```csharp
// Program.cs or App.axaml.cs
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions()
        {
            RenderingMode = new[] { AvaloniaNativeRenderingMode.OpenGl }
        });
```

### Initialize the Scene

```csharp
public void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;
    var scene = args.Scene;

    // Set background color
    scene.Background = Texture.CreateFromColor(Color.Gray);

    // Build your scene...
}
```

## Camera

A camera is required to see anything. `Aura3DView.MainCamera` is the default camera.

```csharp
var camera = view.MainCamera;

// Projection type
camera.ProjectionType = ProjectionType.Perspective;   // Perspective (default)
// camera.ProjectionType = ProjectionType.Orthographic; // Orthographic

// Position the camera
camera.Position = new Vector3(0, 5, 10);
camera.RotationDegrees = new Vector3(-20, 0, 0);

// Whether to render the background/skybox
camera.IsRenderBackground = true;  // Enabled by default

// Fit view to a bounding box
camera.FitToBoundingBox(model.BoundingBox, padding: 0.5f);
```

### Camera Controller

`CameraController` provides out-of-the-box camera controls (WASD move, right-click rotate, scroll zoom, middle-click pan):

```csharp
private CameraController _cameraController;

public void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;

    _cameraController = new CameraController(view)
    {
        MoveSpeed = 30f,          // Movement speed
        MouseSensitivity = 20f,   // Mouse sensitivity
        ZoomSpeed = 5f,           // Zoom speed
    };
}
```

Configurable properties:

| Property | Type | Default | Description |
|---|---|---|---|
| `MoveSpeed` | `float` | `10f` | Keyboard movement speed |
| `MouseSensitivity` | `float` | `20f` | Mouse look sensitivity |
| `PanSpeed` | `float` | `10f` | Pan speed |
| `ZoomSpeed` | `float` | `5f` | Zoom speed |
| `Enabled` | `bool` | `true` | Master on/off |
| `EnableLook` | `bool` | `true` | Enable rotation (right-drag) |
| `EnableMovement` | `bool` | `true` | Enable WASD/QE movement |
| `EnableZoom` | `bool` | `true` | Enable scroll zoom |
| `EnablePan` | `bool` | `true` | Enable middle-click pan |

> **Note**: `CameraController` implements `IDisposable`. Call `Dispose()` when no longer needed.

### Advanced Camera Parameters

```csharp
var camera = view.MainCamera;

// Perspective projection parameters
camera.ProjectionType = ProjectionType.Perspective;
camera.FieldOfView = 60f;         // FOV in degrees, default 75
camera.NearPlane = 0.1f;          // Near clip plane, default 1
camera.FarPlane = 1000f;          // Far clip plane, default 100

// Orthographic projection parameters
camera.ProjectionType = ProjectionType.Orthographic;
camera.OrthographicSize = 10f;    // Ortho view size, default 5

// Look at a target point
camera.LookAt(new Vector3(0, 0, 0));

// Read-only matrices (for custom shaders)
Matrix4x4 viewMatrix = camera.View;
Matrix4x4 projMatrix = camera.Projection;
Matrix4x4 vpMatrix = camera.ViewProjection;
```

Projection values are validated on assignment: `NearPlane > 0`, `FarPlane > NearPlane`, `FieldOfView` is in `(0, 180)` degrees, `OrthographicSize > 0`, and every value must be finite. Use `camera.SetClippingPlanes(near, far)` to update both planes atomically when a large change would otherwise create an invalid intermediate state.

## Models

### Loading glTF/GLB Models

```csharp
// From file path (static model only)
var model = ModelLoader.LoadGlbModel("model.glb");

// From file path (with animations)
var (model, animations) = ModelLoader.LoadGlbModelAndAnimations("model.glb");

// From Stream
using (var stream = File.OpenRead("model.glb"))
{
    var model = ModelLoader.LoadGlbModel(stream);
}

// Load .gltf text format
var (model, animations) = ModelLoader.LoadGltfModelAndAnimations("model.gltf");
```

### Loading via Assimp (50+ Formats)

```csharp
// From file (auto-detect format)
var (model, animations) = AssimpLoader.LoadModelAndAnimations("model.fbx");

// From Stream (must specify format)
using (var stream = File.OpenRead("model.obj"))
{
    var model = AssimpLoader.Load(stream, "obj");
}

// Load animations only (apply to existing skeleton)
using (var stream = File.OpenRead("walk.fbx"))
{
    var animations = AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx");
}
```

Assimp supports: FBX, OBJ, 3DS, DAE, PLY, STL, DXF, MD5, LWO, MS3D, and 40+ more formats.

### Placing Models in the Scene

```csharp
var model = ModelLoader.LoadGlbModel("model.glb");

// Set position, rotation, scale
model.Position = camera.Forward * 3;
model.RotationDegrees = new Vector3(0, 180, 0);
model.Scale = new Vector3(2f);

// Direction vectors (read-only)
// model.Forward / model.Right / model.Up / model.Backward / model.Left / model.Down

view.AddNode(model);
```

### Cloning Models

```csharp
// Share underlying resource data (geometry, textures not copied). Ideal for mass instancing.
var cloned = original.Clone(CopyType.SharedResourceData);
```

### Accessing Mesh Parts

```csharp
// Models consist of multiple Meshes, findable by name
var specificPart = model.Meshes.First(mesh => mesh.Name == "item1");
specificPart.RotationDegrees = specificPart.RotationDegrees with { Y = 45f };
```

## Built-in Geometries

Create basic shapes without external model files:

```csharp
var mesh = new Mesh();

// Built-in geometry types
mesh.Geometry = new BoxGeometry();       // Box
// mesh.Geometry = new SphereGeometry(); // Sphere
// mesh.Geometry = new CylinderGeometry(); // Cylinder
// mesh.Geometry = new PlaneGeometry();  // Plane (1x1)
// mesh.Geometry = new PlaneGeometry(2f, 3f); // Custom-size plane

mesh.Material = new Material();
mesh.Material.BaseColor = Texture.CreateFromColor(Color.White);
mesh.Material.BlendMode = BlendMode.Opaque;
mesh.Material.DoubleSided = true;   // Two-sided rendering

mesh.Position = view.MainCamera.Forward * 3;
view.AddNode(mesh);
```

### Custom Geometry

Build arbitrary shapes manually:

```csharp
var geometry = new Geometry();

// Set primitive type
geometry.PrimitiveType = PrimitiveType.Triangles;

// Set vertex attributes (Position = slot 0, 3 components)
geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, new List<float>
{
    // First triangle
    -0.5f, -0.5f, 0,  // bottom-left
     0.5f, -0.5f, 0,  // bottom-right
     0.0f,  0.5f, 0,  // top
});

// Optional: set indices (without indices, vertices are grouped by 3)
geometry.SetIndices(new List<uint> { 0, 1, 2 });

// Enable/disable vertex attributes
geometry.SetAttributeEnabled(BuildInVertexAttribute.TexCoord_1, false);
```

## Lights

Aura3D uses the Blinn-Phong lighting model by default. **A light source is required to see models.**

> [!WARNING]
> Under the default forward pipeline, each light type supports a maximum of 4 lights.

### Directional Light

Simulates distant light sources with parallel rays (e.g., sunlight).

```csharp
var dl = new DirectionalLight();
dl.LightColor = Color.White;
dl.RotationDegrees = new Vector3(-30, -15, 0);  // Light direction
dl.CastShadow = true;                            // Cast shadows

// Shadow configuration
dl.ShadowConfig.FarPlane = 1000;
dl.ShadowConfig.NearPlane = 10;
dl.ShadowConfig.Width = 1024;
dl.ShadowConfig.Height = 1024;

view.AddNode(dl);
```

### Point Light

Light emitted from a single point in all directions (e.g., bulb, torch).

```csharp
var pl = new PointLight();
pl.LightColor = Color.Red;
pl.AttenuationRadius = 5f;   // Attenuation radius
pl.CastShadow = true;        // Cast shadows
pl.Position = new Vector3(2, 3, 0);

view.AddNode(pl);
```

### Spot Light

Cone-shaped beam (e.g., flashlight, stage spotlight).

```csharp
var sp = new SpotLight();
sp.LightColor = Color.Blue;
sp.AttenuationRadius = 10f;
sp.InnerConeAngleDegree = 15f;   // Inner cone angle
sp.OuterConeAngleDegree = 30f;   // Outer cone angle (soft edge transition)
sp.CastShadow = true;

view.AddNode(sp);
```

### Advanced Light Properties

```csharp
// Directional light — Irradiance (physical lighting)
var dl = new DirectionalLight();
dl.Irradiance = 120000f;   // lux, default 80000. Intensity = Irradiance * 0.00001

// Point light — Luminous intensity and shadow softness
var pl = new PointLight();
pl.LuminousIntensity = 2000f;   // candela (cd), default 1000. Intensity = LuminousIntensity * 0.001
pl.SoftRatio = 0.7f;            // Shadow softness ratio, default 0.9 (higher = harder)

// Spot light — Cone penumbra
var sp = new SpotLight();
sp.InnerConeAngleDegree = 10f;   // Inner cone (full brightness)
sp.OuterConeAngleDegree = 25f;   // Outer cone (penumbra transition)
// Between inner and outer cone: brightness fades from 1.0 to 0.0
sp.LuminousIntensity = 2000f;    // cd, same as point light
sp.SoftRatio = 0.8f;             // Shadow softness ratio
```

> **Note**: `Irradiance` / `LuminousIntensity` provide physical lighting values. The engine internally converts them to `Intensity` (read-only) for the shader. Set the former and the latter syncs automatically.

## Scene Background

### Solid Color Background

```csharp
view.Scene.Background = Texture.CreateFromColor(Color.Gray);
```

### HDR Environment Map / Skybox

```csharp
using (var stream = File.OpenRead("environment.hdr"))
{
    var hdri = TextureLoader.LoadHdrTexture(stream);
    var cubemap = HDRIToCubeTextureConverter.ConvertFromTexture(hdri, 1024);
    view.Scene.Background = cubemap;
}
```

### Cube Map Skybox

```csharp
var streams = new List<Stream>();
foreach (var face in new[] { "px.png", "nx.png", "py.png", "ny.png", "pz.png", "nz.png" })
{
    streams.Add(File.OpenRead(face));
}
var cubeTexture = TextureLoader.LoadCubeTexture(streams);
view.Scene.Background = cubeTexture;

// Remember to close streams
foreach (var s in streams) s.Dispose();
```

### Texture Sampling Configuration

Configure sampling parameters on `Texture` or `CubeTexture` via the fluent API. These settings directly affect rendering quality:

```csharp
var texture = TextureLoader.LoadTexture(stream)
    .SetWarpS(TextureWrapMode.Repeat)         // S-axis (U) wrap mode, default ClampToEdge
    .SetWarpT(TextureWrapMode.Repeat)         // T-axis (V) wrap mode
    .SetMinFilter(TextureFilterMode.LinearMipmapLinear)  // Minification filter, default Linear
    .SetMagFilter(TextureFilterMode.Linear)               // Magnification filter, default Linear
    .SetColorFormat(ColorFormat.Srgb)         // Color space, default none
    .SetIsGammaSpace(true);                   // Gamma space, default false
```

Cube maps additionally support WrapR (third dimension wrap):
```csharp
var cubemap = TextureLoader.LoadCubeTexture(streams)
    .SetWarpR(TextureWrapMode.ClampToEdge);
```

## Node Hierarchy

Aura3D uses a scene graph to organize nodes. Child nodes inherit their parent's transform.

```csharp
var parent = new Node();
parent.Position = new Vector3(0, 5, 0);
view.AddNode(parent);

var child = new Mesh();
child.Geometry = new BoxGeometry();

// Add as child
parent.AddChild(child, AttachToParentRule.KeepWorld);  // Preserve world position
// parent.AddChild(child, AttachToParentRule.KeepLocal); // Preserve local position

// Remove child
parent.RemoveChild(child, AttachToParentRule.KeepWorld);

// Remove from scene
view.Remove(node);
```

## Scene Update & Render Loop

### Automatic Rendering

By default, the control automatically requests rendering each frame. Handle per-frame logic in `SceneUpdated`:

```csharp
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    // e.DeltaTime — time since last frame (seconds)
    // e.Scene — current scene

    // Rotate a light
    dl.RotationDegrees += new Vector3(0, 30, 0) * (float)e.DeltaTime;
}
```

### Manual Rendering

To render on demand (e.g., save resources when the scene is static), disable automatic rendering:

```csharp
view.AutoRequestNextFrameRendering = false;
```

Then manually request a frame when needed:

```csharp
view.RequestNextFrameRendering();
```

> Calling `RequestNextFrameRendering()` in `SceneUpdated` enables continuous rendering (common for real-time animation scenes).

## Click Picking

Aura3D supports picking objects in the scene by screen coordinates with triangle-level precision:

```csharp
// Get all hit results at the screen coordinate (sorted by distance)
List<PickResult> results = view.Scene.Pick(screenX, screenY, view.MainCamera);

// Get the closest hit result
PickResult? closest = view.Scene.PickClosest(screenX, screenY, view.MainCamera);

if (closest != null)
{
    var node = closest.Value.Node;              // The hit node
    var worldPos = closest.Value.WorldPosition; // World position of the hit point
    var distance = closest.Value.Distance;      // Distance to camera
    var instanceIndex = closest.Value.InstanceIndex; // InstancedMesh instance index (null for regular Mesh)
}
```

> Typically called in a mouse click event handler after obtaining screen coordinates. Supports regular Mesh, InstancedMesh, and CPU-skinned skeletal meshes.

## Cascaded Shadow Maps (CSM)

CSM solves directional light shadow aliasing at long distances. Specify the main directional light via `Scene.MainDirectionalLight`:

```csharp
var dl = new DirectionalLight();
dl.LightColor = Color.White;
dl.RotationDegrees = new Vector3(-30, 0, 0);
dl.CastShadow = true;
view.AddNode(dl);

// Set as main directional light → uses CSM; other directional lights fall back to a single shadow map
view.Scene.MainDirectionalLight = dl;
```

CSM parameters are configured via `PipelineSettings`:

```csharp
var settings = new PipelineSettings
{
    CsmCascadeCount = 4,           // Cascade count (default 3, set to 1 to fall back to single shadow map)
    CsmSplitLambda = 0.5f,         // Split parameter (0=uniform, 1=logarithmic, default 0.5)
    CsmShadowMapResolution = 2048, // Per-cascade resolution (default 1024)
};
```

> Only pipelines that declare `SupportsCSM = true` (e.g., BlinnPhong) will enable CSM.

## Debug Visualization

Enable built-in debug drawing via `PipelineSettings.Debug`:

```csharp
var debug = view.Scene.RenderPipeline.Settings.Debug;
debug.Enable = true;                // Master switch
debug.ShowBoundingBox = true;       // Show bounding boxes for all meshes
debug.ShowDirectionalLight = true;  // Show directional light direction lines
debug.ShowPointLight = true;        // Show point light range spheres
debug.ShowSpotLight = true;         // Show spot light cones
debug.ShowCamera = true;            // Show camera frustums
debug.ShowBone = true;              // Show bone hierarchy
```

> Can also be configured in XAML via `PipelineSettings`. Debug drawing has additional performance overhead; recommended for development only.

## Specifying a Rendering Pipeline

Specify a pipeline in XAML via generic type parameter:

```xaml
<!-- PBR deferred pipeline -->
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:pbr="clr-namespace:Aura3D.Pipeline.PBR;assembly=Aura3D.Pipeline.PBR"
    ...>
    <a:Aura3DView x:TypeArguments="pbr:PBRDeferredPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

```xaml
<!-- Cel shading pipeline -->
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:cel="clr-namespace:Aura3D.Pipeline.CelShading;assembly=Aura3D.Pipeline.CelShading"
    ...>
    <a:Aura3DView x:TypeArguments="cel:CelShadingPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

Or set dynamically in code via the `CreateRenderPipeline` property:

```csharp
view.CreateRenderPipeline = scene => new NoLightPipeline(scene);
```

> Note: `CreateRenderPipeline` must be assigned before OpenGL initialization (i.e., before the control is loaded).

## Next Steps

- [Rendering Pipelines](./pipelines.md) — Understand and customize rendering pipelines
- [Animation System](./animation.md) — Skeletal animation, blend spaces, state graphs
- [Instanced Rendering](./instanced-rendering.md) — GPU instancing and HISM
- [Rendering Topics](./rendering.md) — Shadows, point clouds, primitives, materials, node operations
