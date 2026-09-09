<div id="header" align="center">
    <img width="200px" src="./logo.svg" ></img> 
    <h4><i>A lightweight, extensible, high-performance 3D rendering control</i></h4>
    <div id="link">
        <span>English</span> | 
        <a href="./README_CN.md">中文</a> |
        <a href="./doc/en/home.md">Documentation</a>
    </div>
</div>
<br/>

![demo](./doc/images/example_debugtest.png)

**Demo project:** [TowerDefense3D](https://github.com/CeSun/TowerDefense3D) — A tower defense game built with Aura3D, showcasing real-world usage of the engine.

> [!IMPORTANT]
> The project is under active development. Feedback and suggestions are welcome via [Issues](https://github.com/CeSun/Aura3d/issues).

## Overview

Aura3D is an Avalonia-based 3D rendering control built on OpenGL ES 3.0. It provides a complete set of capabilities from model loading, scene management, and lighting/shadows to custom rendering pipelines, suitable for integrating 3D content into .NET desktop applications.

## Features

### Scene & Models
- **Multi-format model loading** — Native glTF/GLB support, plus 50+ formats via Assimp extension (FBX, OBJ, 3DS, etc.)
- **Built-in geometries** — Box, sphere, cylinder, plane
- **Scene graph** — Hierarchical node tree with parent-child transform inheritance

### Lighting & Shadows
- **Directional / Point / Spot lights** — Three light types with color, attenuation radius, and shadow casting
- **CSM cascaded shadows** — Automatic cascaded shadow maps for the main directional light, configurable cascade count and split scheme
- **Blinn-Phong lighting model** — Default forward rendering pipeline
- **HDR environment maps** — Skybox / ambient background

### Animation System
- **Skeletal animation** — glTF skinning and Assimp-imported external animations
- **Animation blend space** — 2D blend space for smooth transitions between animations
- **Animation graph** — Condition-based state machine with blend transitions

### Rendering Pipelines
- **Replaceable pipelines** — Built-in BlinnPhong (realistic) and NoLight (unlit)
- **PBR deferred pipeline** — Physically-based rendering with Metallic-Roughness workflow
- **Cel shading pipeline** — Toon shading style
- **Custom pipelines** — Compose RenderPass freely without dealing with VAO/VBO

### Advanced Rendering
- **GPU instancing** — `InstancedMesh` for high-performance rendering of thousands of instances
- **Hierarchical instancing** — `InstancedMeshGroup` (similar to UE's HISM) with incremental updates and auto-grouping
- **Frustum culling** — Togglable, greatly reduces invisible draw calls
- **Octree spatial index** — Auto-expanding octree for efficient spatial queries and culling
- **Point cloud** — Built-in `PointCloudPipeline` for high-performance instancing-based point cloud rendering
- **Primitive rendering** — Triangles, Lines, LineStrip, LineLoop, Points, TriangleStrip, TriangleFan
- **Click picking** — `Scene.Pick` / `Scene.PickClosest` for triangle-precise picking of Mesh and InstancedMesh
- **Debug visualization** — Built-in debug drawing for bounding boxes, lights, bones, and camera frustums

### Platforms
- **Avalonia** — Windows, Linux, macOS, Android, iOS
- **.NET 8+** — Supports .NET 8.0 and .NET 10.0

## Quick Start

### 1. Install

```shell
dotnet add package Aura3D.Avalonia
dotnet add package Aura3D.Model.GltfLoader
```

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

> See the [documentation](doc/en/home.md) for more features.

## NuGet Packages

| Package | Description |
|---|---|
| [Aura3D.Avalonia](https://www.nuget.org/packages/Aura3D.Avalonia) | Avalonia 3D rendering control (depends on Aura3D.Core) |
| [Aura3D.Core](https://www.nuget.org/packages/Aura3D.Core) | Core engine: scene graph, nodes, resources, default pipeline |
| [Aura3D.Model.GltfLoader](https://www.nuget.org/packages/Aura3D.Model.GltfLoader) | glTF/GLB model loader |
| [Aura3D.Model.AssimpLoader](https://www.nuget.org/packages/Aura3D.Model.AssimpLoader) | Assimp model loader (50+ formats) |
| [Aura3D.Pipeline.PBR](https://www.nuget.org/packages/Aura3D.Pipeline.PBR) | PBR deferred rendering pipeline |
| Aura3D.Pipeline.PBR.Common | Shared resources required by the PBR pipelines |
| Aura3D.Pipeline.PBRForward | PBR forward rendering pipeline |
| [Aura3D.Pipeline.CelShading](https://www.nuget.org/packages/Aura3D.Pipeline.CelShading) | Cel shading rendering pipeline |

## License

[MIT](LICENSE)
