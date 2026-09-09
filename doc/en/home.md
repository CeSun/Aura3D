# Aura3D Documentation

Welcome to Aura3D. This documentation covers everything from installation to custom rendering pipelines.

## Documents

| Document | Contents |
|---|---|
| **[Get Started](./get-started.md)** | Install NuGet packages → XAML control declaration → Initialize scene → Camera setup → Load models → Configure lights → Scene background → Render loop |
| **[Rendering Pipelines](./pipelines.md)** | Built-in BlinnPhong / NoLight / PBR / CelShading → Custom RenderPipeline + RenderPass → Shader macro system → Lifecycle hooks → Multi-camera |
| **[Animation System](./animation.md)** | Skeletal animation / Loop modes / External animation import → 2D blend space → Animation graph → Bone manipulation |
| **[Instanced Rendering](./instanced-rendering.md)** | GPU instancing (InstancedMesh) → Per-instance attributes / Transform updates → Hierarchical instancing (HISM) → Incremental updates |
| **[Rendering Topics](./rendering.md)** | Shadows / Point clouds / Primitive rendering → Advanced materials → Node operations (bounding box / clone / batch transform / find children) |
| **[GPU Resource Lifecycle](./gpu-resource-lifecycle.md)** | GPU-state ownership → Idempotent destruction → Context loss and recovery → Custom-state contract |

## Example Project

Clone the repository and run `example/Example.Desktop` to see interactive demos of all features:

```shell
git clone https://github.com/CeSun/Aura3d.git
cd Aura3d
dotnet restore
dotnet run --project example/Example.Desktop
```

Controls: WASD to move, right-click drag to rotate, scroll to zoom, middle-click to pan. Use the left sidebar to switch between feature demo pages.
