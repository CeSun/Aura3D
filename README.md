<div id="header" align="center">
    <img width="200px" src="./logo.svg" ></img> 
    <h4><i>A lightweight, extensible, high-performance Avalonia 3D control library</i></h4>
    <div id="link">
        <span>English</span> | 
        <a href="./README_CN.md">中文</a> |
        <a href="https://cesun.github.io/Aura3D-Docs/en/">Documentation</a> |
        <a href="https://cesun.github.io/Aura3D-Gallery/">Online Demo</a>
    </div>
</div>
<br/>

![demo](./doc/images/example_debugtest.png)

**Online gallery:** [Aura3D-Gallery](https://cesun.github.io/Aura3D-Gallery/) — run every engine feature (pipelines, IBL, animation blend space, materials) directly in your browser.

**Demo project:** [TowerDefense3D](https://github.com/CeSun/TowerDefense3D) — a tower defense game built with Aura3D, showcasing real-world usage of the engine.

> [!IMPORTANT]
> The project is under active development. Feedback and suggestions are welcome via [Issues](https://github.com/CeSun/Aura3d/issues).

## Overview

Aura3D is a lightweight, high-performance and extensible Avalonia 3D control library. It covers the full path from model loading, scene management and lighting/shadows to custom rendering pipelines, so you can integrate 3D content into .NET desktop applications.

## Features

| Area | Capabilities |
|---|---|
| Scene & models | Scene graph with a hierarchical node tree; native glTF/GLB loading plus FBX, OBJ, 3DS and 50+ formats via Assimp; built-in box / sphere / cylinder / plane |
| Camera | Perspective and orthographic projection, look-at and auto-framing, `CameraController` mouse/keyboard interaction, multi-camera split views, render-to-texture |
| Lighting & shadows | Directional, point and spot lights; CSM cascaded shadows for the main directional light (cascade count and split scheme configurable); Blinn-Phong forward pipeline by default |
| Environment & IBL | Solid-color, image and cubemap backgrounds; HDR panoramic skybox; PBR pipelines bake irradiance and prefiltered environment maps |
| Materials & shaders | Material channels and textures with sampling options; material-level custom shaders and shader macros; per-instance custom attributes |
| Animation | glTF skinning and external Assimp animations; 2D animation blend space; condition-based animation state graph; bone attachment |
| Particles | CPU-simulated particle system with emission shapes, mesh particles and flipbooks, full playback lifecycle |
| Pipelines | Built-in BlinnPhong / NoLight / PBR deferred / PBR forward / cel shading (default pipeline runs on OpenGL ES 3.0); custom pipelines composed freely from RenderPass with no VAO/VBO handling |
| Instancing & picking | `InstancedMesh` GPU instancing, `InstancedMeshGroup` hierarchical instancing (HISM with incremental updates and auto-grouping), point clouds, all primitive topologies; triangle-precise picking via `Scene.Pick` |
| Performance & debug | Frustum culling, octree spatial index, debug drawing for bounds, lights, bones and camera frustums |
| GPU resources | On-demand upload and release, automatic recovery from context loss |
| Platforms | Avalonia on Windows / Linux / macOS / Android / iOS; .NET 8.0 and .NET 10.0, including browser (WebGL2) |

## Quick Start

### 1. Install

```shell
dotnet add package Aura3D.Avalonia
dotnet add package Aura3D.Model.GltfLoader
```

> [!IMPORTANT]
> **Browser (`net10.0-browser`) projects:** .NET 10 Release/static publishing requires three extra
> MSBuild properties in the application `.csproj` — see
> [Browser Release/static publishing](https://cesun.github.io/Aura3D-Docs/en/platform-render-backends.html#browser-net10-release-config).

> At minimum you need one model loader. `Aura3D.Model.GltfLoader` handles glTF/GLB.
> For FBX, OBJ, 3DS and 50+ other formats, also add `Aura3D.Model.AssimpLoader`.

### 2. Use in XAML

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    ...>
    <a:Aura3DView x:Name="aura3Dview" SceneInitialized="OnSceneInitialized"/>
</Window>
```

### 3. Load a model

```csharp
public void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;
    var camera = view.MainCamera;

    // Set background color
    view.Scene.Background = Texture.CreateFromColor(Color.Gray);

    // Load glTF/GLB model
    var model = ModelLoader.LoadGlbModel("model.glb");
    model.Position = camera.Forward * 3;
    view.AddNode(model);

    // Add a directional light (required by the default pipeline)
    var dl = new DirectionalLight();
    dl.RotationDegrees = new Vector3(-30, 0, 0);
    dl.LightColor = Color.White;
    view.AddNode(dl);
}
```

> See the [documentation](https://cesun.github.io/Aura3D-Docs/en/) for more features.

## NuGet Packages

| Package | Description |
|---|---|
| [Aura3D.Avalonia](https://www.nuget.org/packages/Aura3D.Avalonia) | Avalonia 3D rendering control (depends on Aura3D.Core) |
| [Aura3D.Avalonia.Browser](https://www.nuget.org/packages/Aura3D.Avalonia.Browser) | Build-time wiring for the browser (WebGL2) render path |
| [Aura3D.Core](https://www.nuget.org/packages/Aura3D.Core) | Core engine: scene graph, nodes, resources, default pipeline |
| [Aura3D.Model.GltfLoader](https://www.nuget.org/packages/Aura3D.Model.GltfLoader) | glTF/GLB model loader |
| [Aura3D.Model.AssimpLoader](https://www.nuget.org/packages/Aura3D.Model.AssimpLoader) | Assimp model loader (50+ formats) |
| [Aura3D.Pipeline.PBR](https://www.nuget.org/packages/Aura3D.Pipeline.PBR) | PBR deferred rendering pipeline |
| [Aura3D.Pipeline.PBRForward](https://www.nuget.org/packages/Aura3D.Pipeline.PBRForward) | PBR forward rendering pipeline |
| [Aura3D.Pipeline.CelShading](https://www.nuget.org/packages/Aura3D.Pipeline.CelShading) | Cel shading rendering pipeline |
| [Aura3D.Angle.iOS](https://www.nuget.org/packages/Aura3D.Angle.iOS) | ANGLE (Metal) native frameworks for the iOS render path |

## License

[MIT](LICENSE)
