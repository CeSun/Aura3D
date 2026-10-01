---
section: notes
order: 3
---

# Node and Scene Reference

**Pure quick reference**: node types, `Node` / `Scene` / `PickResult` members, debug layers, and enums. For usage see [The Scene Graph and Nodes](./scene-and-nodes.md).

> [!NOTE]
> All renderable node types live in the `Aura3D.Core.Nodes` namespace; `Scene`, `Grid`, `AxisGizmo`, and `PickResult` live in `Aura3D.Core.Scenes`.

## Node type overview

| Type | What it does | Key members (a subset, not exhaustive) | Namespace | Tutorial |
|---|---|---|---|---|
| `Camera` | Sets viewpoint and projection | `ProjectionType`, `FieldOfView`, `NearPlane`/`FarPlane`, `OrthographicSize`, `IsRenderBackground`, `View`/`Projection`/`ViewProjection`, `LookAt(Vector3)`, `FitToBoundingBox(bbox, padding)`, `SetClippingPlanes(near, far)`, `WorldToScreen(Vector3)` | `Aura3D.Core.Nodes` | [Cameras and View Control](./camera.md) |
| `Light` (abstract base) | Common part of the three lights | `LightColor`, `CastShadow` | `Aura3D.Core.Nodes` | [Lighting and Shadows](./lighting.md) |
| `DirectionalLight` | Parallel light / sun; direction from `RotationDegrees` | `Irradiance` (lux, default 80000), `Intensity` (read-only), `ShadowConfig` (`Width`/`Height`/`NearPlane`/`FarPlane`) | `Aura3D.Core.Nodes` | [Lighting and Shadows](./lighting.md) |
| `PointLight` | Point source, glows in all directions | `AttenuationRadius` (default 10f), `LuminousIntensity` (cd, default 1000), `Intensity` (read-only), `SoftRatio` (default 0.9), `ShadowConfig` | `Aura3D.Core.Nodes` | [Lighting and Shadows](./lighting.md) |
| `SpotLight` | Spotlight, a cone beam | `InnerConeAngleDegree` (default 10), `OuterConeAngleDegree` (default 15), `AttenuationRadius`, `LuminousIntensity`, `SoftRatio`, `ShadowConfig` | `Aura3D.Core.Nodes` | [Lighting and Shadows](./lighting.md) |
| `Mesh` | A single renderable shape (geometry + material) | `Geometry`, `Material`, `BoundingBox`, `LocalBoundingBox`, `Model`, `IsSkinnedMesh`/`IsStaticMesh` | `Aura3D.Core.Nodes` | [Loading and Placing Models](./models.md) |
| `Model` | The whole subtree of an imported model | `Meshes`, `Skeleton`, `AnimationSampler`, `BoundingBox`, `BoundingBoxPadding`, `CustomBoundingBox`, `Clone(CopyType)`, `IsSkinnedModel` | `Aura3D.Core.Nodes` | [Loading and Placing Models](./models.md) |
| `InstancedMesh` | GPU instancing; one geometry drawn many times | `FromMesh(mesh)` (static), `AddInstance(Matrix4x4)`, `UpdateInstance(i, transform)`, `RemoveInstance(i)`, `SetInstances(list)`, `InstanceCount`, `Material`, `SetAttributeEnabled(name, bool)`, `SetInstanceAttribute<T>(attr, count, data)`, `EnableFrustumCulling` | `Aura3D.Core.Nodes` | [Instanced Rendering](./instanced-rendering.md) |
| `InstancedMeshGroup` | HISM: octree grouping + frustum culling | ctor `InstancedMeshGroup(sourceMesh)`, `SourceMesh`, `MaxInstancesPerGroup` (default 1024), `MaxDepth` (default 6), `SetInstances(list)`, `AddInstance`/`AddInstances`, `UpdateInstance(i, transform)`, `Build()`, `InstanceCount`/`GroupCount`/`InPlaceUpdateCount`/`RebuildCount`, `IsBuilding` | `Aura3D.Core.Nodes` | [Instanced Rendering](./instanced-rendering.md) |
| `ParticleSystem` | A set of CPU-simulated emitters | `Emitters`, `Play()`, `Stop()`, `Pause()`, `IsPlaying`, `ActiveCount`, `MaxParticles`, `CustomBoundingBox`, `EnableVisibilityCulling` | `Aura3D.Core.Nodes` | [Particle System](./particle-system.md) |
| `BoneAttachment` | Pins a node to a specific bone | `Mesh` (must be skinned), `BoneName`, `LocalOffset`, `NormalizeScale` | `Aura3D.Core.Nodes` | [Animation System](./animation.md) |

## Node base members

`Mesh`, `Model`, `Camera`, `Light`, `InstancedMesh`, `InstancedMeshGroup`, `ParticleSystem`, and `BoneAttachment` all derive from `Node` and share these members.

| Member | Type | Description |
|---|---|---|
| `Name` | `string` | Node name, default `"Node"` |
| `Tags` | `HashSet<string>` | Tag set used for grouped filtering |
| `Position` | `Vector3` | Local position |
| `Rotation` | `Vector3` | Local rotation (radians) |
| `RotationDegrees` | `Vector3` | Local rotation (degrees), synced with `Rotation` |
| `RotationQuaternion` | `Quaternion` | Local rotation (quaternion), synced with the two above |
| `Scale` | `Vector3` | Local scale, default `(1,1,1)` |
| `LocalTransform` | `Matrix4x4` | Local transform matrix, read/write |
| `WorldTransform` | `Matrix4x4` | World transform matrix, read/write (assigning back-solves the local) |
| `Forward`/`Backward`/`Up`/`Down`/`Right`/`Left` | `Vector3` | Read-only direction vectors |
| `Parent` | `Node?` | Parent node, read-only |
| `Children` | `IReadOnlySet<Node>` | Child set, read-only |
| `CurrentScene` | `Scene?` | Owning scene, read-only |
| `Enable` | `bool` | Toggle; `false` also disables the whole subtree |
| `AddChild(Node, AttachToParentRule)` | method | Attach as a child |
| `RemoveChild(Node, AttachToParentRule)` | method | Detach a child |
| `GetNodesInChildren<T>()` | `List<T>` | Recursively collect `T` nodes in the subtree (includes self) |
| `BeginTransformUpdate(UpdateTransformMode = All)` | `IDisposable` | Batch transform edits; recompute once at the end of `using` |
| `Update(double delta)` | virtual method | Per-frame callback; overridden by `Model`/`ParticleSystem`/`InstancedMeshGroup`/`BoneAttachment` |

## Scene members

| Member | Type | Description |
|---|---|---|
| `MainCamera` | `Camera` | Default camera, created automatically with the `Scene` |
| `MainDirectionalLight` | `DirectionalLight?` | CSM cascades only activate for the light set here |
| `Background` | `OneOf<CubeTexture, Texture>` | Background — solid texture or cube-map skybox |
| `Nodes` | `IReadOnlySet<Node>` | Set of top-level nodes |
| `RenderPipeline` | `RenderPipeline` | Current render pipeline (its `Settings` is readable) |
| `PipelineSettings` | `PipelineSettings` | Pipeline configuration object |
| `AddNode(Node)` | method | Add a top-level node (with its subtree) |
| `RemoveNode(Node)` | method | Remove a top-level node (root nodes only) |
| `Pick(float x, float y, Camera?)` | `List<PickResult>` | Screen-coordinate picking, sorted by distance; `camera` defaults to the main camera |
| `PickClosest(float x, float y, Camera?)` | `PickResult?` | The single closest hit |
| `ShowGrid` | `bool` | Show/hide the ground grid |
| `ShowAxisGizmo` | `bool` | Show/hide the axis gizmo |
| `Grid` | `Grid` | Ground-grid configuration object |
| `AxisGizmo` | `AxisGizmo` | Axis-gizmo configuration object |
| `MeshOctree` | `Octree<Mesh>` | Spatial mesh index used for picking/culling broad phase |

## PickResult members

Returned by `Scene.Pick` / `Scene.PickClosest`.

| Member | Type | Description |
|---|---|---|
| `Node` | `Node` | The hit node (a mesh part hit returns its owning `Model`) |
| `InstanceIndex` | `int?` | `InstancedMesh` instance index; `null` for a regular `Mesh` |
| `Distance` | `float` | Distance from the hit point to the camera |
| `WorldPosition` | `Vector3` | World-space position of the hit point |

## Debug layers: Grid and AxisGizmo

`Scene.Grid` and `Scene.AxisGizmo` are configuration objects (**not `Node`s**), toggled via `ShowGrid` / `ShowAxisGizmo`.

| Object | Member | Type | Description |
|---|---|---|---|
| `Grid` | `Enable` | `bool` | Whether shown (read/written via `Scene.ShowGrid`) |
| `Grid` | `Size` | `float` (default 10.0f) | Half-extent size |
| `Grid` | `Divisions` | `int` (default 10) | Number of divisions |
| `Grid` | `LineColor` | `Color` | Regular grid-line color |
| `Grid` | `CenterLineColor` | `Color` | Center axis-line color |
| `AxisGizmo` | `Enable` | `bool` | Whether shown (read/written via `Scene.ShowAxisGizmo`) |
| `AxisGizmo` | `AxisLength` | `float` (default 1.0f) | Axis length |
| `AxisGizmo` | `ArrowheadSize` | `float` (default 0.15f) | Arrowhead size |

> [!TIP]
> For overlays such as light range spheres, camera frustums, bounding boxes, and bone lines, use `PipelineSettings.Debug.*` — see [Choosing and Configuring Pipelines](./pipelines.md).

## Related enums

| Enum | Members | Use |
|---|---|---|
| `AttachToParentRule` | `KeepWorld`, `KeepLocal` | Decide whether world or local is preserved on `AddChild`/`RemoveChild` |
| `UpdateTransformMode` | `Local`, `World`, `ChildrenWorld`, `All` | Set the recompute scope for `BeginTransformUpdate` (bit flags) |
| `ProjectionType` | `Perspective`, `Orthographic` | `Camera.ProjectionType` |

## Cross-links

- Usage and per-node walkthrough: [The Scene Graph and Nodes](./scene-and-nodes.md)
- Geometry / custom geometry / material channels: [Loading and Placing Models](./models.md), [Materials and Textures](./material.md)
- Troubleshooting (black screen, invisible objects, transforms not refreshing): [Common Pitfalls and Troubleshooting](./troubleshooting.md)
- Source: [Node.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/Node.cs), [Scene.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Scenes/Scene.cs)
