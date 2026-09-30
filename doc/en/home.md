# Aura3D Documentation

Welcome to Aura3D — an Avalonia 3D rendering control built on OpenGL ES 3.0. It comes with a full engine feature set: a scene graph with nodes, model loading (native glTF/GLB, plus FBX/OBJ and 50+ more formats via Assimp), three light types with CSM cascaded shadows, skeletal animation with blend spaces and state machines, a particle system, GPU instancing, and triangle-precise picking. The render pipeline is replaceable: Blinn-Phong forward is the default, PBR (forward and deferred) and cel shading are built in, and you can compose your own pipeline from RenderPasses. It runs on Windows, Linux, macOS, Android, iOS, and the browser (WebAssembly), targeting .NET 8+. The docs are organized around "the thing you want to accomplish": get something on screen first, then learn each capability one task at a time, and look up pitfalls and quick-reference tables when something goes wrong.

## Getting Started

| Document | Contents |
|---|---|
| **[Quick Start](./quickstart.md)** | Install the package → drop in the control → a box + a light → run and see a picture |
| **[Your First Full App](./first-app.md)** | Load a model → aim the camera → per-frame animation → click picking, wired into one working app |

## Scene Basics

| Document | Contents |
|---|---|
| **[Scene Graph and Nodes](./scene-and-nodes.md)** | The node tree and transforms → which node to use → batch edits and lookups |
| **[Loading and Placing Models](./models.md)** | glTF loading → scale / position / orientation → fitting the camera and bounding boxes |
| **[Cameras and View Control](./camera.md)** | Projection and LookAt → CameraController mouse orbit → multi-camera and render targets |
| **[Lighting and Shadows](./lighting.md)** | The three light types → per-type light limits → shadow configuration and CSM |
| **[Materials and Textures](./material.md)** | Material channels and textures → loading textures → sampling settings → custom material parameters |
| **[Environment and Background](./environment.md)** | Scene background (solid / texture / cube map) → HDR environments and IBL |
| **[Animation System](./animation.md)** | Playing and controlling skeletal animation → blend spaces and state machines → manual bone control |
| **[Particle System](./particle-system.md)** | Emitter configuration → emission shapes → mesh particles and flipbooks → debugging and performance |

## Advanced

| Document | Contents |
|---|---|
| **[Instanced Rendering](./instanced-rendering.md)** | InstancedMesh → per-instance attributes → HISM hierarchical instancing → incremental updates |
| **[Custom Materials and Shaders](./custom-material.md)** | Material-level shader replacement → the shader macro mechanism → per-instance custom attributes |
| **[Choosing and Configuring Pipelines](./pipelines.md)** | Built-in pipeline catalog and how to pick → PipelineSettings → lighting / shadow / tone-mapping parameters |
| **[Custom Render Pipelines](./custom-pipeline.md)** | Writing your own RenderPipeline / RenderPass → shader variants and compilation → the GLES 3.0 subset |
| **[GPU Resource Lifecycle](./gpu-resource-lifecycle.md)** | The IGpuState contract → upload-on-demand and reclamation → context loss and restoration |

## Pitfalls and Reference

| Document | Contents |
|---|---|
| **[Platforms and Render Backends](./platform-render-backends.md)** | Which rendering path each platform takes → macOS / iOS / Android / Browser configuration → the three required .NET 10 Release settings |
| **[Common Pitfalls and Troubleshooting](./troubleshooting.md)** | Indexed by symptom: black screen, platform silent failures, shaders, configuration timing, context and lifecycle |
| **[Node and Scene Reference](./reference-nodes.md)** | Every node class with members and namespaces — for looking up, not reading |
| **[Built-in Passes and Shader Macros](./reference-shaders.md)** | Built-in passes and their passKeys → vertex attribute location conventions → macro combinations |

## Where to Start

- First time: [Quick Start](./quickstart.md) alone is enough.
- Something looks wrong: open [Common Pitfalls and Troubleshooting](./troubleshooting.md) first.
- Shipping to a platform: [Platforms and Render Backends](./platform-render-backends.md).
