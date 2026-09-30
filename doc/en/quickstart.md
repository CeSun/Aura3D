---
section: start
order: 1
---

# Quick Start

Drop an `Aura3DView` into your Avalonia app: one box, one light, mouse orbit, run it and see the picture. Platform and browser setup is not on this page — see [Platforms and Render Backends](./platform-render-backends.md).

## Install

```shell
dotnet add package Aura3D.Avalonia
```

`Aura3D.Avalonia` pulls in `Aura3D.Core` and the default BlinnPhong forward pipeline — boxes and lighting work out of the box, nothing else to install for this step.

## Drop the control

Add the namespace and declare the control in your window XAML, wiring the post-initialization callback to `SceneInitialized`:

```xaml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:a="https://github.com/CeSun/Aura3D"
    x:Class="MyApp.MainWindow">
    <a:Aura3DView
        x:Name="view"
        SceneInitialized="OnSceneInitialized"/>
</Window>
```

`SceneInitialized` fires once OpenGL is ready and the `Scene` exists — build your scene here; touching GPU resources before this point is too early.

## Build the scene: one box + one light

In the init callback create a box, add a directional light, and enable mouse orbit:

```csharp
using System.Numerics;
using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Controls;

public partial class MainWindow : Window
{
    private CameraController? _controller;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
    {
        var view = (Aura3DView)sender;

        // A box placed in front of the default camera
        var box = new Mesh { Geometry = new BoxGeometry(), Material = new Material() };
        box.Material.BaseColor = Texture.CreateFromColor(System.Drawing.Color.White);
        box.Position = view.MainCamera.Forward * 3;
        view.AddNode(box);

        // A directional light — without it the screen is pure black (see below)
        var light = new DirectionalLight();
        light.LightColor = System.Drawing.Color.White;
        light.RotationDegrees = new Vector3(-30, -20, 0);   // rotation defines the light direction
        view.AddNode(light);

        // Mouse orbit: right-drag rotate, scroll zoom, WASD move
        _controller = new CameraController(view);
    }
}
```

> [!IMPORTANT]
> **No light means nothing is visible.** The default BlinnPhong forward pipeline shades surfaces with a lighting model: with zero lights in the scene every surface computes to black, so the box is on screen but you can't see it. This is the single most common first trap — add a `DirectionalLight` before you debug anything else.

`Material`'s `BaseColor` is an extension property: use it on an assignment (call `new Material()` first, then `material.BaseColor = ...`). It cannot go inside an object initializer. Note that `LightColor` and `Texture.CreateFromColor(...)` take a `System.Drawing.Color` (not Avalonia's `Color`), which is why the sample writes `System.Drawing.Color.White`.

## Run

Press F5 (or `dotnet run`). When the window appears you should see a gray box; hold the right mouse button to orbit, scroll to dolly, WASD to move.

Common `CameraController` options (all set at construction):

| Property | Default | Effect |
|---|---|---|
| `MoveSpeed` | `10f` | WASD movement speed |
| `MouseSensitivity` | `20f` | Right-drag look sensitivity |
| `ZoomSpeed` | `5f` | Scroll zoom speed |
| `EnableLook` / `EnableZoom` / `EnablePan` / `EnableMovement` | `true` | Toggle each operation independently |
| `LookButton` / `PanButton` | `Right` / `Middle` | Which mouse buttons drive look and pan; reassignable |

> `CameraController` implements `IDisposable`. Call `Dispose()` when you no longer need it.

## Platform configuration

A macOS desktop app must pin the host renderer to OpenGL (check this first when the viewport shows nothing); iOS/Android/Browser each have their own backend constraints, and `net10.0-browser` Release publishing needs three trimming settings together. None of this affects the shortest path above — the details live in [Platforms and Render Backends](./platform-render-backends.md).

## Black or blank screen?

Nine times out of ten it's a missing light, a light turned off via `Enable`, or the camera not aimed at the object. Work through the checklist in [Common Pitfalls and Troubleshooting](./troubleshooting.md).

## Next steps

- [Your First Full App](./first-app.md) — load a model, light it, frame the camera, animate per frame, and click to pick, wired into one runnable app
- [The Scene Graph and Nodes](./scene-and-nodes.md) · [Lighting and Shadows](./lighting.md) · [Cameras and View Control](./camera.md)
