---
section: basics
order: 3
---

# Cameras and View Control

Without a camera, a scene shows nothing. This page covers: seeing your scene through the default camera, switching between perspective and orthographic projection, aiming at targets, letting the mouse and keyboard drive the view with `CameraController`, and multi-camera rendering plus render-to-texture.

## Shortest runnable path: place the camera and see the scene

Every scene ships with a default camera, `Scene.MainCamera` (the control's `view.MainCamera` is exactly that) — no need to create one:

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;

    var camera = view.MainCamera;
    camera.Position = new Vector3(0, 5, 10);
    camera.LookAt(new Vector3(0, 0, 0));   // aim at the world origin
}
```

For mouse/keyboard interaction, add a controller (see the CameraController section below).

## Projection type and parameters

`ProjectionType` switches between perspective and orthographic; each has its own parameters:

```csharp
var camera = view.MainCamera;

// Perspective (default)
camera.ProjectionType = ProjectionType.Perspective;
camera.FieldOfView = 60f;      // field of view in degrees
camera.NearPlane = 0.1f;       // near clip plane
camera.FarPlane = 1000f;       // far clip plane

// Orthographic (no perspective falloff; good for 2D views and top-down editors)
camera.ProjectionType = ProjectionType.Orthographic;
camera.OrthographicSize = 10f; // orthographic view size
```

| Parameter | Type | Default | Projection | Description |
|---|---|---|---|---|
| `ProjectionType` | `Perspective` / `Orthographic` | `Perspective` | — | Projection mode |
| `FieldOfView` | `float` | `75` | Perspective | Vertical field of view (degrees) |
| `NearPlane` | `float` | `1` | Both | Near clip plane |
| `FarPlane` | `float` | `100` | Both | Far clip plane — anything beyond is not rendered |
| `OrthographicSize` | `float` | `5` | Orthographic | Orthographic view size |
| `IsRenderBackground` | `bool` | `true` | — | Whether this camera renders the background/skybox |

> [!IMPORTANT]
> Projection parameters are validated **at assignment time**: `NearPlane > 0`, `FarPlane > NearPlane`, `FieldOfView` within `(0, 180)` degrees, `OrthographicSize > 0`, and every value must be finite — violating any rule raises an error immediately. So when both clip planes must change substantially together (e.g. from `Near=1, Far=100` to `Near=200, Far=400`), assigning them one by one passes through an invalid intermediate state where `Far < Near`. Use the atomic update instead:
>
> ```csharp
> camera.SetClippingPlanes(200f, 400f);
> ```

## Aiming at targets and auto-framing

When you don't want to compute rotation angles by hand, use these two methods instead of setting `RotationDegrees`:

```csharp
// Look at a world-space point
camera.LookAt(new Vector3(0, 1f, 0));

// Move and orient automatically so the bounding box fits the view (padding = margin ratio)
camera.FitToBoundingBox(model.BoundingBox, padding: 0.5f);
```

`FitToBoundingBox` pairs with a [model's BoundingBox](./models.md#bounding-box) and is the standard way to "see the model I just loaded" immediately.

The camera's own pose still uses the regular node properties (`Position`, `RotationDegrees`) — see [The Scene Graph and Nodes](./scene-and-nodes.md). Custom shaders can read the camera matrices through the read-only properties:

```csharp
Matrix4x4 viewMatrix = camera.View;
Matrix4x4 projMatrix = camera.Projection;
Matrix4x4 vpMatrix = camera.ViewProjection;
```

## Mouse and keyboard control: CameraController

`CameraController` subscribes to the control's input and drives `MainCamera`. Default bindings:

- **WASD / QE**: move forward/back/left/right/up/down
- **Right-drag**: rotate the view
- **Scroll wheel**: zoom
- **Middle-drag**: pan

```csharp
private CameraController _cameraController;

public void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;

    _cameraController = new CameraController(view)
    {
        MoveSpeed = 30f,          // movement speed
        MouseSensitivity = 20f,   // mouse sensitivity
        ZoomSpeed = 5f,           // zoom speed
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
| `EnableLook` | `bool` | `true` | Rotation (right-drag) |
| `EnableMovement` | `bool` | `true` | WASD/QE movement |
| `EnableZoom` | `bool` | `true` | Scroll zoom |
| `EnablePan` | `bool` | `true` | Middle-click pan |

> [!NOTE]
> `CameraController` implements `IDisposable`. It hooks the control's input events, so call `Dispose()` when you no longer need it (page unload, mode switches).

## Multi-camera and split views

**All** `Camera` nodes in the scene are automatically discovered by the render pipeline and rendered one by one: every pass registered in `RenderPassGroup.EveryCamera` runs once per camera. Adding a second camera is just a normal node:

```csharp
var secondCamera = new Camera
{
    Position = new Vector3(10, 5, 0),
    IsRenderBackground = false,  // key: don't render the skybox twice
};
secondCamera.LookAt(Vector3.Zero);

scene.AddNode(secondCamera);
```

> [!TIP]
> Only one camera should own the background (default `IsRenderBackground = true`). Turn the property off for the rest, or the skybox gets drawn multiple times — wasteful and prone to overwriting each other.

## Render to texture: minimaps and monitor views

Assigning a `RenderTarget` to a camera sends its image into a texture instead of the main screen — typical uses are minimaps, rear-view mirrors and surveillance monitors:

```csharp
// Create an off-screen render target
var renderTarget = new ControlRenderTarget(width, height);
secondCamera.RenderTarget = renderTarget;

// After rendering, read the texture in SceneUpdated and feed it as a material input —
// e.g. map it onto a PlaneGeometry and you have a minimap inside the scene
```

Pipeline-level details — `CameraOutput`, render target registration, multi-pass orchestration — are in [Choosing and Configuring Pipelines](./pipelines.md).

## Common pitfalls

- **The scene is completely empty**: first check that a camera exists and is aimed at the subject — if the model isn't in view, use `LookAt` or `FitToBoundingBox` to frame it, then check for a light source (see [Lighting and Shadows](./lighting.md)).
- **Distant ground/models get clipped**: `FarPlane` defaults to just 100; raise it for large scenes (e.g. `camera.FarPlane = 250f`). Conversely, pushing clip planes too far apart hurts depth precision and worsens z-fighting.
- **Assigning clip planes throws**: setting them individually passes through an invalid intermediate state (`Far <= Near`). When both change, use `SetClippingPlanes(near, far)` — see the validation note above.
- **The second camera draws the skybox again**: set `IsRenderBackground = false` on it.
- **Keyboard/mouse still control the old scene after navigating away**: the `CameraController` was never disposed and its handlers are still attached to the control.

## Runnable examples

- [Camera demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Camera/CameraDemo.axaml.cs) — perspective/orthographic switching, FOV and clip-plane sliders, `LookAt`, `FitToBoundingBox`, and world-to-screen readout verification.

## Next steps

- [Choosing and Configuring Pipelines](./pipelines.md) — how multi-camera rendering works at the pipeline level and what `CameraOutput` is.
- [Environment and Background](./environment.md) — give the `IsRenderBackground = true` camera a skybox.
