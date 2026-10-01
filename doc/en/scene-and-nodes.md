---
section: basics
order: 1
---

# The Scene Graph and Nodes

Everything on screen — cameras, lights, models, particles — is a **node** hanging on one tree, and that tree is the **scene graph**. This page covers how to build, change and search that tree, then introduces each node type.

## The scene graph: a Node tree

Every node has its own **local transform** (`Position`, rotation via `Rotation`/`RotationDegrees`/`RotationQuaternion`, and `Scale`) plus a **world transform** computed from the whole tree. The rule is a single sentence:

> A child's world transform = its own local transform × its parent's world transform.

In other words, **moving or rotating a parent drags every descendant along**. This is the basis of anything like "a robot that moves" or "a planet orbiting" — you use an empty `Node` as a pivot, attach things under it, and then only rotate the pivot.

```csharp
// An empty node acts as a "pivot"; attach two lights and rotate only the pivot and they orbit.
var orbit = new Node { Name = "LightOrbit" };
view.AddNode(orbit);

var point = new PointLight { LightColor = Color.Red, AttenuationRadius = 9f };
var spot  = new SpotLight  { LightColor = Color.Blue, AttenuationRadius = 12f };

// KeepLocal: preserve their position relative to the pivot (they keep their own local coords)
orbit.AddChild(point, AttachToParentRule.KeepLocal);
orbit.AddChild(spot,  AttachToParentRule.KeepLocal);

// From now on, rotating just the orbit node makes both lights circle around it each frame.
orbit.RotationDegrees = new Vector3(0, orbit.RotationDegrees.Y + 1f, 0);
```

### Adding to and removing from the scene

Top-level nodes enter the scene via `AddNode` and leave via `Remove`. Once a parent is added, its entire subtree is registered with the scene — you don't `AddNode` each child.

```csharp
view.AddNode(mesh);      // add to the scene (same as view.Scene.AddNode(mesh))
view.Remove(mesh);       // remove from the scene (takes its subtree with it)
```

### Attaching children: KeepWorld vs KeepLocal

The second argument to `AddChild(child, rule)` / `RemoveChild(child, rule)` decides **where the child appears at the instant you attach/detach it**:

- `AttachToParentRule.KeepWorld`: the child's **world position stays put** — the engine recomputes its local transform to fit the parent. Use it when you placed something in world space and then parented it without wanting it to jump.
- `AttachToParentRule.KeepLocal`: the child's **local transform stays put** — it immediately follows the parent. Use it when you intended the offset to be relative to the parent all along.

```csharp
parent.AddChild(child, AttachToParentRule.KeepWorld);   // looks unmoved; local coords get rewritten
parent.AddChild(child, AttachToParentRule.KeepLocal);   // local coords fixed; world position follows the parent

// Same idea when detaching: KeepWorld leaves the child in place, KeepLocal just drops the parent link.
parent.RemoveChild(child, AttachToParentRule.KeepWorld);
```

> [!NOTE]
> `AddChild` validates: a node can't become its own child, cycles are rejected, a node can't have two parents, and parent/child can't belong to different scenes. Any violation throws.

### Batching transform changes: BeginTransformUpdate

Setting `Position`, `Rotation`, or `Scale` one at a time recomputes the world matrix on every assignment and refreshes the whole subtree downward. When you change several properties together, wrap them in `BeginTransformUpdate()` so the recompute happens once at the end of the `using`:

```csharp
using (node.BeginTransformUpdate(UpdateTransformMode.All))
{
    node.Position = new Vector3(10, 0, 5);
    node.RotationDegrees = new Vector3(0, 90, 0);
    node.Scale = new Vector3(2f);
}   // world matrix recomputed once when the using block ends
```

`UpdateTransformMode` is a flags enum with values `Local` / `World` / `ChildrenWorld` (plus the combined `All`, the default); pass just the subset you need to refresh a part of it. **Always use `using` (or call `Dispose` manually)** — without it the recompute never fires.

### Finding nodes in the subtree: GetNodesInChildren

`GetNodesInChildren<T>()` recursively collects every node in the subtree rooted at `this` that is a `T` (the node itself is included if it matches). It's how you find a model's parts, child lights, and child meshes:

```csharp
var allMeshes = model.GetNodesInChildren<Mesh>();     // every mesh in the model
var allLights = scene.MainCamera.GetNodesInChildren<Light>();
```

### Grouping with Tags

`Tags` is a string set on every node, meant for tagging and grouping nodes; combine it with LINQ to filter:

```csharp
mesh.Tags.Add("pickable");

var pickables = model.GetNodesInChildren<Node>()
    .Where(n => n.Tags.Contains("pickable"));
```

### Hiding temporarily: Enable

`node.Enable = false` stops that node **and its whole subtree** from rendering and updating; set it back to `true` to restore. Use it for show/hide toggles instead of adding and removing nodes.

### Duplicating: cloning

There is no generic clone on the node tree itself; the thing you can clone wholesale is a **model**. `Model.Clone(CopyType)` copies the hierarchy, and `CopyType.SharedResourceData` shares the underlying geometry and textures — the cheapest option, good for "place many copies of the same model":

```csharp
var another = model.Clone(CopyType.SharedResourceData);
view.AddNode(another);
```

Sharing boundaries of clones and the difference from `FullCopy` are covered in [Loading and Placing Models](./models.md).

## Scene: the container for everything

The `Scene` you receive in the `SceneInitialized` event owns the scene graph. It holds the top-level node set and exposes a few members you'll touch almost every time:

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view  = (Aura3DView)sender;
    var scene = args.Scene;

    // Background: solid color or HDR cube map, both assigned to the same property
    scene.Background = Texture.CreateFromColor(Color.Gray);

    // Main camera: every Scene ships one — no need to construct it yourself
    scene.MainCamera.Position = new Vector3(0, 4.2f, 12f);
    scene.MainCamera.LookAt(new Vector3(0, 1.6f, 0));

    // Main directional light: CSM only turns on for the light you designate here
    var sun = new DirectionalLight { LightColor = Color.White, CastShadow = true };
    sun.RotationDegrees = new Vector3(-52, -34, 0);
    scene.AddNode(sun);
    scene.MainDirectionalLight = sun;

    // Picking: cast screen coordinates onto objects in the scene
    var hits = scene.Pick(screenX, screenY, scene.MainCamera);
}
```

| Scene member | What it does |
|---|---|
| `MainCamera` | Default camera, created automatically with the Scene; use it directly |
| `MainDirectionalLight` | The directional light designated as the CSM key light; no cascades if unset |
| `Background` | Scene background — a solid `Texture` or a `CubeTexture` (skybox) |
| `Nodes` | The set of top-level nodes currently registered in the scene |
| `AddNode` / `RemoveNode` | Add / remove a top-level node |
| `Pick` / `PickClosest` | Screen-coordinate picking; returns `PickResult` (hit node, world position, distance, instance index) |
| `ShowGrid` / `ShowAxisGizmo` | Turn on the debug ground grid and axis gizmo (see below) |

Full background/skybox setup is in [Environment and Background](./environment.md); camera, lights, and picking have their own deep-dives below.

## I want to do this → which node?

| What you want | Use this node | Go deeper |
|---|---|---|
| Decide where the view is and perspective vs orthographic | `Camera` | [Cameras and View Control](./camera.md) |
| Add a sun / parallel light to light the whole scene | `DirectionalLight` | [Lighting and Shadows](./lighting.md) |
| Add a bulb / torch that glows in all directions | `PointLight` | [Lighting and Shadows](./lighting.md) |
| Add a flashlight / stage spotlight beam | `SpotLight` | [Lighting and Shadows](./lighting.md) |
| Place a basic shape (box / sphere / cylinder / plane) | `Mesh` | [Loading and Placing Models](./models.md) |
| Place an imported model file (glb / fbx / obj…) | `Model` | [Loading and Placing Models](./models.md) |
| Hundreds to thousands copies of one object | `InstancedMesh` | [Instanced Rendering](./instanced-rendering.md) |
| Massive instances with view-based culling | `InstancedMeshGroup` (HISM) | [Instanced Rendering](./instanced-rendering.md) |
| Fire, smoke, rain/snow particle effects | `ParticleSystem` | [Particle System](./particle-system.md) |
| Make a sword follow a hand bone in an animation | `BoneAttachment` | [Animation System](./animation.md) |

Each node below gets 2–4 sentences on what it does plus a minimal snippet.

## Camera: decides how the view looks

The camera chooses the viewpoint and projection that render the scene to screen; a scene needs at least one camera to show anything. `Scene.MainCamera` is already built for you — just change its transform.

```csharp
var camera = scene.MainCamera;
camera.ProjectionType = ProjectionType.Perspective; // or Orthographic
camera.Position = new Vector3(0, 5, 10);
camera.LookAt(new Vector3(0, 0, 0));
camera.FitToBoundingBox(model.BoundingBox, padding: 0.5f); // auto-frame the model
```

Mouse/keyboard roaming, projection switching, multiple cameras, and rendering to a texture are in [Cameras and View Control](./camera.md).

## DirectionalLight / PointLight / SpotLight: light the scene

All three lights descend from the abstract `Light` base and share `LightColor` and `CastShadow` (off by default). Under the default forward pipeline **each light type is capped at 4**, and "camera but no light" usually means invisible models — place a directional light before going further.

**`DirectionalLight`** — parallel rays, used for the sun. It has no notion of position; direction comes from `RotationDegrees`. Setting it as `Scene.MainDirectionalLight` enables cascaded shadow maps (CSM).

```csharp
var sun = new DirectionalLight { LightColor = Color.White, CastShadow = true };
sun.RotationDegrees = new Vector3(-52, -34, 0);
sun.Irradiance = 80000;                     // lux, physical irradiance
view.AddNode(sun);
```

**`PointLight`** — glows from a point in all directions, like a bulb. Use `Position` to place it and `AttenuationRadius` to control reach.

```csharp
var bulb = new PointLight { LightColor = Color.Red, AttenuationRadius = 5f };
bulb.Position = new Vector3(2, 3, 0);
view.AddNode(bulb);
```

**`SpotLight`** — a cone beam, like a flashlight. `InnerConeAngleDegree`/`OuterConeAngleDegree` set the cone; brightness ramps between them for a soft edge.

```csharp
var torch = new SpotLight { LightColor = Color.Blue, AttenuationRadius = 10f };
torch.InnerConeAngleDegree = 15f;
torch.OuterConeAngleDegree = 30f;
view.AddNode(torch);
```

Shadow config, CSM parameters, and physical light-unit conversions live in [Lighting and Shadows](./lighting.md).

## Mesh: a single renderable shape

A `Mesh` is the smallest renderable unit: a geometry plus a material, placed in world space. Built-in geometries are box, sphere, cylinder, and plane — swap `Geometry` to change shape, swap `Material` to change appearance.

```csharp
var mesh = new Mesh { Geometry = new BoxGeometry() };
mesh.Material = new Material();
mesh.Material.BaseColor = Texture.CreateFromColor(Color.White);
mesh.Position = scene.MainCamera.Forward * 3;
view.AddNode(mesh);
```

Built-in geometry constructor parameters, hand-written custom geometry, and the seven primitive types are in [Loading and Placing Models](./models.md); material channels and textures are in [Materials and Textures](./material.md).

## Model: an imported model tree

A `Model` is itself a node, but it's a subtree made of several `Mesh` nodes (one loaded model file). A loader builds the whole tree; `Meshes` is the set of meshes under it — look up parts by `Name` and drive them individually.

```csharp
var model = ModelLoader.LoadGlbModel("robot.glb");
model.Position = new Vector3(0, 0, 0);
model.Scale = new Vector3(2f);
view.AddNode(model);

var head = model.Meshes.First(m => m.Name == "Head");
head.RotationDegrees = new Vector3(0, 45, 0);   // rotate just the head
```

glb/gltf and Assimp multi-format loading, part lookup, bounding boxes, and cloning are in [Loading and Placing Models](./models.md).

## InstancedMesh: draw one geometry many times

`InstancedMesh` uses GPU instancing to draw "the same mesh, placed differently" in a single submission — thousands of trees, tens of thousands of stars, one geometry and material submitted once. Derive it from a `Mesh` and then add only instance matrices.

```csharp
var source = new Mesh { Geometry = new BoxGeometry() };
var instanced = InstancedMesh.FromMesh(source);

instanced.AddInstance(Matrix4x4.CreateTranslation(0, 0, 0));
instanced.AddInstance(Matrix4x4.CreateTranslation(3, 0, 0));
view.AddNode(instanced);
```

Per-instance add/update/remove uses `AddInstance`/`UpdateInstance`/`RemoveInstance`; replacing the whole batch each frame uses `SetInstances(...)`; per-instance color and custom attributes are in [Instanced Rendering](./instanced-rendering.md).

## InstancedMeshGroup: culling massive instance sets (HISM)

`InstancedMeshGroup` (hierarchical instanced mesh) builds an octree on top of `InstancedMesh`, splitting instances into groups and culling by frustum — good for "tens of thousands to hundreds of thousands, only part of which is visible." Hand it all the instance matrices and it groups and rebuilds asynchronously.

```csharp
var source = new Mesh { Geometry = new BoxGeometry() };
var group = new InstancedMeshGroup(source)
{
    MaxInstancesPerGroup = 64,
    MaxDepth = 6,
};

group.SetInstances(transforms);   // List<Matrix4x4>, provide all at once
group.Build();                    // trigger octree grouping (background async, finalized automatically)
view.AddNode(group);
```

Grouping parameters, when to `Build`, incremental updates, and the statistics fields are in [Instanced Rendering](./instanced-rendering.md).

## ParticleSystem: CPU-simulated particle effects

`ParticleSystem` is a node holding several emitters (`Emitters`) for fire, smoke, rain/snow, and magic effects that spawn and die over time. Configure the emitters, then call `Play()`; the system advances the simulation each frame automatically.

```csharp
var ps = new ParticleSystem();
ps.Emitters.Add(myEmitter);   // see the particle tutorial for building a ParticleEmitter
ps.Position = new Vector3(0, 1, 0);
view.AddNode(ps);
ps.Play();
```

Every `ParticleEmitter` parameter, textured billboard vs mesh mode, and performance/visibility culling are in [Particle System](./particle-system.md).

## BoneAttachment: lock an object to a bone

`BoneAttachment` is a special node: tell it which skinned mesh (`Mesh`) and which bone (`BoneName`) to follow, and each frame it snaps itself to that bone's world position — exactly how a sword "grows" out of a character's hand.

```csharp
var sword = new BoneAttachment
{
    Mesh = skinnedMesh,        // must be a skinned mesh with a skeleton
    BoneName = "Hand_R",
    LocalOffset = Matrix4x4.CreateTranslation(0, 0.2f, 0),
};
view.AddNode(sword);
```

By default `LocalOffset` and anything attached below are interpreted in model-local units; if the model's root node carries a global scale (common for centimeter-authored glTF), set `NormalizeScale = true` to attach props in world units — see [Animation System](./animation.md).

The skeleton system, `Skeleton`, and animation sampling are in [Animation System](./animation.md).

## Grid and AxisGizmo: debug aids, not nodes

`Scene` ships a `Grid` (ground grid) and an `AxisGizmo` (origin axis) as **debug layers** — they are scene configuration objects, not `Node`s on the tree. Turn them on when you need to see axis orientation or align objects.

```csharp
scene.ShowGrid = true;
scene.Grid.Size = 12f;
scene.Grid.Divisions = 12;

scene.ShowAxisGizmo = true;
scene.AxisGizmo.AxisLength = 2.5f;
```

For the light range spheres, camera frustums, and bounding-box overlays, use `PipelineSettings.Debug.*` (see [Choosing and Configuring Pipelines](./pipelines.md)).

## Common pitfalls

> [!WARNING]
> - **Forgot a camera or a light**: no camera means no picture; under the default forward pipeline, a camera without a light often shows a black model. Confirm `Scene.MainCamera` is placed and at least one light is added.
> - **Picking the wrong KeepWorld / KeepLocal**: an object "jumping" on attach usually means the rule was flipped — use `KeepWorld` to keep it in place, `KeepLocal` to place it at a parent-relative offset.
> - **Re-adding or cross-scene parenting**: a node belongs to one scene; `AddNode` on an already-added node, or parenting nodes from two different scenes, throws.
> - **BeginTransformUpdate without using**: if the scope never ends (no `Dispose`), the world matrix is never recomputed and nothing appears to move.
> - **Calling view.Remove on a child**: `Remove`/`RemoveNode` only target top-level nodes; detach a child with `parent.RemoveChild(...)`.
> - **Enable's cascade**: `Enable = false` shuts off the entire subtree; when chasing a "thing disappeared" bug, check whether an ancestor got disabled.
> - **Adding/removing InstancedMesh one by one each frame**: to move a large batch per frame, use `SetInstances(...)` to replace them all at once — far more efficient than looping `AddInstance`/`RemoveInstance`.

## Runnable examples

- [SceneGizmos demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/SceneGizmos/SceneGizmosDemo.axaml.cs) — `ShowGrid`/`ShowAxisGizmo`, `Grid`/`AxisGizmo` parameters, hiding nodes with `Enable`, lights orbiting a pivot node.
- [ModelViewer demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/ModelViewer/ModelViewerDemo.axaml.cs) — model tree, part lookup, `Clone(CopyType)`.
- [Geometries demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Geometries/GeometriesDemo.axaml.cs) — `Mesh` with built-in geometries.
- [Instancing demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Instancing/InstancingDemo.axaml.cs) / [Hism demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Hism/HismDemo.axaml.cs) — instanced and hierarchical instanced nodes.
- [Particles demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Particles/ParticlesDemo.axaml.cs) — the particle system node.

The full quick-reference tables for node and scene members are in [Node and Scene Reference](./reference-nodes.md). Source: [Node.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/Node.cs), [Scene.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Scenes/Scene.cs).
