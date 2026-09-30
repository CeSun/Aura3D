# Aura3D Documentation

Welcome to Aura3D. This documentation covers everything from installation to custom rendering pipelines.

## Documents

| Document | Contents |
|---|---|
| **[Get Started](./get-started.md)** | Install NuGet packages → XAML control declaration → Initialize scene → Camera setup → Load models → Configure lights → Scene background → Render loop |
| **[Rendering Pipelines](./pipelines.md)** | Built-in BlinnPhong / NoLight / PBR / CelShading → Custom RenderPipeline + RenderPass → Shader macro system → Lifecycle hooks → Multi-camera |
| **[Animation System](./animation.md)** | Skeletal animation / Loop modes / External animation import → 2D blend space → Animation graph → Bone manipulation |
| **[Particle System](./particle-system.md)** | CPU simulation + GPU instanced rendering → core classes and emission shapes → configuration guide / mesh mode → lifecycle and performance → debug visualization → ParticlePass global settings → troubleshooting |
| **[Instanced Rendering](./instanced-rendering.md)** | GPU instancing (InstancedMesh) → Per-instance attributes / Transform updates → Hierarchical instancing (HISM) → Incremental updates |
| **[Rendering Topics](./rendering.md)** | Shadows / Point clouds / Primitive rendering → Advanced materials → Node operations (bounding box / clone / batch transform / find children) |
| **[GPU Resource Lifecycle](./gpu-resource-lifecycle.md)** | GPU-state ownership → Idempotent destruction → Context loss and recovery → Custom-state contract |
| **[Platforms and Render Backends](./platform-render-backends.md)** | Which path each platform takes → iOS ANGLE(Metal) backend and framework setup → GLES 3.0 subset constraints → Frame scheduling and thread semantics |
