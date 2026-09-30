---
section: start
order: 2
---

# Your First Full App

[Quick Start](./quickstart.md) got a first frame on screen. This page strings the loose blocks into one runnable app: load a glTF model, place it, light it, frame the camera, spin it every frame in `SceneUpdated`, then click to pick a part and highlight it. Type it out once and you'll own the backbone of an Aura3D app.

When you're done you'll have the skeleton of a model viewer: auto-framed, slowly rotating, mouse-orbitable, and any part you click turns red.

## Install

Model loading is not in the base package — add the glTF loader:

```shell
dotnet add package Aura3D.Avalonia
dotnet add package Aura3D.Model.GltfLoader
```

Grab a `.glb` model (call it `model.glb`), drop it in the app's working directory, or replace the path below with an absolute one.

## XAML: one view, three callbacks

```xaml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:a="https://github.com/CeSun/Aura3D"
    x:Class="MyApp.MainWindow">
    <a:Aura3DView
        x:Name="view"
        SceneInitialized="OnSceneInitialized"
        SceneUpdated="OnSceneUpdated"
        PointerPressed="OnPointerPressed"/>
</Window>
```

- `SceneInitialized` — GL is ready and `Scene` exists; build the scene, load the model, frame the camera here.
- `SceneUpdated` — fires before every frame with `DeltaTime` (seconds) in the args; per-frame animation goes here.
- `PointerPressed` — the standard Avalonia pointer event. `Aura3DView` is a control, so use it directly to get the click position for picking.

## Full code

`MainWindow.axaml.cs`:

```csharp
using System;
using System.Numerics;
using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia.Controls;
using Avalonia.Input;

public partial class MainWindow : Window
{
    private const string ModelPath = "model.glb";   // point this at your model

    private CameraController? _controller;
    private Model? _model;

    // Tracks the last highlighted part so the next click restores it
    private Mesh? _highlighted;
    private Texture? _savedBaseColor;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
    {
        var view = (Aura3DView)sender;
        var scene = args.Scene;

        scene.Background = Texture.CreateFromColor(System.Drawing.Color.Gray);

        // 1) Load the model
        var model = ModelLoader.LoadGlbModel(ModelPath);
        model.Name = "Hero";

        // 2) Place it: at the world origin, upright facing the camera
        model.Position = new Vector3(0, 0, 0);

        // 3) One directional light — the default BlinnPhong pipeline is pitch black without it
        var light = new DirectionalLight();
        light.LightColor = System.Drawing.Color.White;
        light.RotationDegrees = new Vector3(-30, -20, 0);
        view.AddNode(light);

        // 4) Frame it: fit to the model's bounding box, leave a little breathing room
        view.MainCamera.FitToBoundingBox(model.BoundingBox, padding: 0.5f);

        view.AddNode(model);
        _model = model;

        // 5) Mouse orbit
        _controller = new CameraController(view) { MoveSpeed = 20f };
    }

    private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
    {
        if (_model == null)
            return;

        // Spin a little every frame around Y: 30 deg/sec × this frame's duration
        _model.RotationDegrees += new Vector3(0, 30, 0) * (float)e.DeltaTime;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var view = (Aura3DView)sender!;
        var scene = view.Scene;
        if (scene == null)
            return;

        // Pick only on left click so we don't fight the right-drag orbit
        if (!e.GetCurrentPoint(view).Properties.IsLeftButtonPressed)
            return;

        var position = e.GetPosition(view);
        var hit = scene.PickClosest((float)position.X, (float)position.Y, view.MainCamera);
        if (hit == null)
            return;

        if (hit.Value.Node is not Mesh mesh || mesh.Material == null)
            return;

        // Restore the previous highlight first
        if (_highlighted?.Material != null)
            _highlighted.Material.SetTexture("BaseColor", _savedBaseColor);

        // Remember the original color, then tint the hit part red
        _highlighted = mesh;
        _savedBaseColor = mesh.Material.GetTexture("BaseColor");
        mesh.Material.SetTexture("BaseColor", Texture.CreateFromColor(System.Drawing.Color.Red));

        view.RequestNextFrameRendering();
    }
}
```

> `ModelLoader` comes from the `Aura3D.Model.GltfLoader` package, in namespace `Aura3D.Model` — if the compiler can't find it, you haven't installed that package yet.
>
> `LightColor` and `Texture.CreateFromColor(...)` take a `System.Drawing.Color`, not Avalonia's `Avalonia.Media.Color`. The samples write `System.Drawing.Color.White` to avoid clashing with Avalonia's same-named `Color`; if your file already has `using Avalonia.Media;`, keep the fully-qualified form.

## What each step does

1. **Load** — `ModelLoader.LoadGlbModel(path)` returns a `Model` node tree of several `Mesh` parts. Use `LoadGlbModelAndAnimations` when you also want animations.
2. **Place** — `Model` derives from `Node`, so it has the same `Position`/`RotationDegrees`/`Scale` as the box.
3. **Light** — one `DirectionalLight` is enough to see the model; `RotationDegrees` sets where the light comes from.
4. **Frame** — `FitToBoundingBox(model.BoundingBox, padding)` pulls the camera back to just enclose the whole model, so you don't hand-tune the distance. This matters most when the model's real scale is unknown.
5. **Spin per frame** — `SceneUpdated` fires each frame; scale the angular speed by `e.DeltaTime` so the rotation is frame-rate independent.
6. **Click to pick** — `Scene.PickClosest(x, y, camera)` returns the nearest `PickResult?` at that screen point; `hit.Value.Node` is the `Mesh` you clicked. Swap its color with `SetTexture("BaseColor", ...)` to highlight it, and don't forget to restore the previous one.

## Common options

- **Render on demand** (save power when idle): set `view.AutoRequestNextFrameRendering = false;`, then call `view.RequestNextFrameRendering()` to push one frame after an animation step or a click. This example uses the default continuous rendering.
- **All hits, not just the closest**: `scene.Pick(x, y, camera)` returns a distance-sorted `List<PickResult>`, so you can reach through a translucent panel to the part behind it; `PickClosest` returns only the nearest.
- **Orbit feel**: `CameraController`'s `MouseSensitivity`, `ZoomSpeed`, `PanSpeed`, or disable specific actions with `EnableZoom`/`EnablePan`.
- **Tighter/looser framing**: adjust `FitToBoundingBox`'s `padding`, or change `view.MainCamera.FieldOfView`.

> [!WARNING]
> The default forward pipeline supports at most 4 lights per type; extras are ignored. For more lights or physical lighting see [Choosing and Configuring Pipelines](./pipelines.md).

## Common pitfalls

- **Black screen**: nine times out of ten it's a missing light, a `RotationDegrees` that points the directional light away from the model, or an unaimed camera. Confirm a directional light exists first.
- **Model huge/tiny, camera far away**: normal — `FitToBoundingBox` pulls the camera back to the model's real size; scroll in to inspect detail.
- **Picking is offset (high DPI)**: `e.GetPosition(view)` returns logical pixels. If the hit point is offset from what you clicked, see the coordinate-scaling note in [Common Pitfalls and Troubleshooting](./troubleshooting.md).
- **`BaseColor` can't go in an initializer**: it's an extension property, so `mesh.Material = new Material { BaseColor = ... }` won't compile. Create the material first, then assign, or use `SetTexture("BaseColor", ...)`.
- **Highlight not restored**: each click puts the previous part's color back with `SetTexture`; otherwise every part you've clicked stays red.

## More real, runnable code

- [Picking demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Picking/PickingDemo.axaml.cs) — the `ObjectPicked` event, the full-hit list, and `InstanceIndex` from instanced picking
- [ModelViewer demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/ModelViewer/ModelViewerDemo.axaml.cs) — the model tree, bounding boxes, and what `Clone(CopyType)` shares
- [Camera demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Camera/CameraDemo.axaml.cs) — projection switching, `LookAt`, `FitToBoundingBox`, `WorldToScreen`

## Next steps

Go deeper by topic: [The Scene Graph and Nodes](./scene-and-nodes.md) · [Loading and Placing Models](./models.md) · [Cameras and View Control](./camera.md) · [Lighting and Shadows](./lighting.md) · [Materials and Textures](./material.md). For animation see [Animation System](./animation.md).
