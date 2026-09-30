---
section: advanced
order: 1
---

# Instanced Rendering

One geometry + one material + a list of matrices = **tens of thousands of objects drawn in a single draw call**. That is instancing: it collapses "draw this mesh N times" into "draw it once, N times over". Aura3D gives you two options:

- **`InstancedMesh`** — a flat table of instances. You fill the matrices, you change them, whenever you like.
- **`InstancedMeshGroup`** — the HISM equivalent (Hierarchical Instanced Static Mesh, as in Unreal): an octree splits the instances into leaf blocks, and each leaf block is internally an `InstancedMesh`, so culling works per block.

Both are ordinary nodes: `view.AddNode(...)` puts them in the scene, and both get the current pipeline's lighting, shadows and material channels.

## Pick a path first

| What you are drawing | Use this | Why |
|---|---|---|
| Same mesh, tens to a few thousand, mostly all in view at once | `InstancedMesh` | One draw call, free-form editing of the instance table, no extra structure to maintain |
| Same mesh, tens of thousands, spread over a large area, camera sees only a corner | `InstancedMeshGroup` | Culling runs per octree block, so invisible blocks never enter the draw list; the price is `Build()` and conditional incremental updates |
| Dynamic data refreshed wholesale every frame (waves, scans, particles) | `InstancedMesh` + `SetInstances` | Positions all change anyway, so spatial grouping buys nothing |
| Many **different** meshes | Neither | Instancing requires one shared geometry and material; different meshes means one `Mesh` each |

> [!TIP]
> There is really only one test: **do these objects share one geometry and one material?** If yes, instance them. Then ask a second question — does the camera usually see only part of them? If yes, go HISM.

## Shortest working setup: one InstancedMesh field

10×10×10 = 1000 cubes, one draw call:

```csharp
private InstancedMesh? field;
private readonly List<Vector3> positions = [];
private readonly List<float> angles = [];

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // 1. Prepare a regular mesh to act as the source
    var source = new Mesh
    {
        Geometry = new BoxGeometry(),
        Material = new Material { BlendMode = BlendMode.Opaque },
    };
    source.Material.SetTexture("BaseColor", Texture.CreateFromColor(Color.White));

    // 2. Derive the instanced mesh from it
    field = InstancedMesh.FromMesh(source);

    const int gridSize = 10;
    const float spacing = 2.5f;
    var offset = (gridSize - 1) * spacing / 2f;

    // 3. Add instances, one transform matrix each
    for (var x = 0; x < gridSize; x++)
    {
        for (var y = 0; y < gridSize; y++)
        {
            for (var z = 0; z < gridSize; z++)
            {
                var pos = new Vector3(
                    x * spacing - offset,
                    y * spacing - offset,
                    z * spacing - offset);

                field.AddInstance(Matrix4x4.CreateTranslation(pos));
                positions.Add(pos);
                angles.Add(0f);
            }
        }
    }

    view.AddNode(field);
}
```

`AddInstance` returns the new instance's index and `field.InstanceCount` reports how many there are. If you know the count up front and replace the whole set later, `SetInstances(list)` is simpler.

> [!WARNING]
> A large instance field easily blows past the camera's default far plane (`FarPlane` defaults to 100); the symptom is "the near half renders, the far half simply isn't there". When you pull the camera back or grow the field, raise it — e.g. `camera.FarPlane = 120f;`. Both Gallery instancing demos do exactly that.

## Animate the instances

```csharp
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    if (field == null) return;

    var dt = (float)e.DeltaTime;

    for (var i = 0; i < field.InstanceCount; i++)
    {
        angles[i] += 0.6f * dt;

        var transform = Matrix4x4.CreateRotationY(angles[i])
                      * Matrix4x4.CreateTranslation(positions[i]);

        field.UpdateInstance(i, transform);
    }

    ((Aura3DView)sender).RequestNextFrameRendering();
}
```

The three calls you will use constantly:

```csharp
field.UpdateInstance(i, transform);   // change one instance
field.SetInstances(allTransforms);    // replace the whole table: preferred for dynamic data
field.RemoveInstance(0);              // remove one; every later index shifts down by one
```

> [!NOTE]
> These calls only touch the CPU-side instance buffer and bump the resource's version. At render time the pipeline compares `Version` with `SyncedVersion` and re-uploads the whole buffer only when they differ. So **changing 1 instance and changing 1000 in the same frame cost the same on the GPU side** — do all of a frame's edits in that frame instead of spreading them out to "save bandwidth". If you turned auto-rendering off (`AutoRequestNextFrameRendering = false`), remember to drive frames yourself, as `RequestNextFrameRendering()` above does.

## Instance matrices are world matrices

An `InstancedMesh` node's own `Position` / `Rotation` / `Scale` do **not** apply to its instances: each instance's slots 8–11 are used as its `modelMatrix` directly. To move a whole field, fold the offset into every instance matrix (or split the field across several `InstancedMesh` nodes and give each a constant offset).

This is also why instanced culling behaves differently: a regular `Mesh` is culled by its own bounding box, while an `InstancedMesh` is culled with **the merged box of all its instances** — a field spread over a wide area is essentially always inside the frustum, which means it is never culled. That limitation is precisely why HISM exists.

## Per-instance custom attributes

Beyond transforms, each instance can carry its own data (color being the most common). You push it into a free vertex attribute slot and let your shader read it:

```csharp
var colors = new List<Vector4>();

for (var i = 0; i < field.InstanceCount; i++)
{
    var t = i / (float)Math.Max(field.InstanceCount - 1, 1);
    colors.Add(new Vector4(t, 1f - t, 0.35f + 0.5f * MathF.Sin(t * 14f), 1f));
}

// Order matters: instances first, then the per-instance attribute, whose length must equal InstanceCount
field.SetInstanceAttribute<Vector4>(BuildInVertexAttribute.TexCoord_1, 4, colors);
```

Slots are **assigned straight from the enum's numeric value**, so pick one that is not taken:

| Slots | Contents |
|---|---|
| 0–7 | Per-vertex attributes: 0 position, 1 UV, 2 vertex color, 3 normal, 4 tangent, 5 bitangent, 6 joint indices, 7 joint weights |
| 8–11 | Per-instance `modelMatrix` (`InstancedTransformColumn0..3`) |
| 12–15 | Per-instance `normalMatrix` (`InstancedNormalTransformColumn0..3`) |
| 16 up | Your own per-instance attributes: `TexCoord_1 = 16`, `TexCoord_2 = 17`, … |

Built-in pass shaders know nothing about the slot you just added, so consuming it means overriding that pass's vertex shader — the full material-level shader workflow is in [Custom Materials and Shaders](./custom-material.md). Two things matter here: **the declared `location` must match the slot table**, and **once you take over the vertex stage you have taken over the instance transform**, so you multiply it yourself:

```glsl
#version 300 es
precision highp float;
//{{defines}}

layout(location = 0) in vec3 position;
layout(location = 16) in vec4 instanceColor;

#ifdef INSTANCED_MESH
layout(location = 8) in mat4 modelMatrix;
#else
uniform mat4 modelMatrix;
#endif

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

out vec4 vColor;

void main()
{
    vColor = instanceColor;
    gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(position, 1.0);
}
```

> [!TIP]
> The instance-aware shader pair used by the Gallery (per-instance color plus the `#ifdef INSTANCED_MESH` branch) is `InstancedColorVertex` in [Kit/Shaders.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Kit/Shaders.cs). It puts the per-instance color in slot 2 (`BuildInVertexAttribute.Color_0`) — when the source geometry has no vertex colors, that slot is free too and can be borrowed.

## Turn off the instance attributes you don't use

By default every instance carries two `mat4`s: the transform (slots 8–11) and its normal transform matrix (slots 12–15) — 128 bytes per instance. That adds up quickly at tens of thousands of instances. If you don't need the per-instance normal matrix, stop shipping it:

```csharp
field.SetAttributeEnabled("InstanceNormalTransform", false);
```

Once disabled, the engine does not allocate a buffer for that attribute and never enables slots 12–15, saving VRAM and the bandwidth of every re-upload.

> [!WARNING]
> The built-in `base.vert` (used by `LightPass` in BlinnPhong / PBR, among others) **does** declare and use `layout(location = 12) in mat4 normalMatrix` to build the TBN basis. So disable it only when your custom shader never reads slots 12–15 — for example an unlit path that only outputs a per-instance color, or uniform scaling where you rotate normals with `mat3(modelMatrix)` instead. Turning it off while instances use non-uniform scale makes lighting visibly wrong.

## Tens of thousands of scattered instances: HISM grouping with InstancedMeshGroup

`InstancedMeshGroup` hands the instance table to an octree that splits it into leaf blocks, each leaf block being a real `InstancedMesh` (parented under the group node and visible through `Groups`). Blocks the camera cannot see never enter the draw list, which is what makes "10,000 grass blades, only a corner in view" affordable.

```csharp
private InstancedMeshGroup? group;

private void BuildField(Aura3DView view)
{
    var source = new Mesh
    {
        Name = "Stalk",
        Geometry = new BoxGeometry(0.32f, 2.6f, 0.32f),
        Material = new Material(),
    };
    source.Material.SetTexture("BaseColor", Texture.CreateFromColor(Color.White));

    group = new InstancedMeshGroup(source)
    {
        Name = "HISM",
        MaxInstancesPerGroup = 512,   // capacity of one leaf block
        MaxDepth = 6,                 // how deep the octree subdivides
    };

    var transforms = new List<Matrix4x4>();

    for (var i = 0; i < 12000; i++)
    {
        var angle = i * 2.399963f;
        var radius = 6f + 44f * MathF.Sqrt((i % 9973) / 9973f);

        transforms.Add(Matrix4x4.CreateTranslation(
            MathF.Cos(angle) * radius, 0.6f, MathF.Sin(angle) * radius));
    }

    group.SetInstances(transforms);
    group.Build();          // start one grouping pass

    view.AddNode(group);    // add the group only; leaf blocks are its business
}
```

The knobs you will touch:

| Parameter / member | Default | What it does and how to tune it |
|---|---|---|
| `MaxInstancesPerGroup` | 1024 | Leaf block capacity. Smaller → more blocks, sharper culling, more draw calls |
| `MaxDepth` | 6 | Deepest octree subdivision. Raise it for dense, far-flung distributions; too deep shatters into dust-sized blocks |
| `SetInstances(list)` | — | Replace all instances (invalidates the current grouping) |
| `AddInstance(t)` / `AddInstances(list)` | — | Append instances (also invalidates) |
| `RemoveInstance(i)` / `ClearInstances()` | — | Remove one / remove all |
| `Build()` | — | Explicitly start a grouping pass; needed again after changing the two parameters above |
| `Groups` | — | Current leaf blocks (`InstancedMesh`), each with its own `EnableFrustumCulling` |
| `InstanceCount` / `GroupCount` | — | Total instances / current number of blocks |
| `InPlaceUpdateCount` / `RebuildCount` | 0 | In-place updates / rebuilds (the former resets on every rebuild) |
| `IsBuilding` | — | Whether a background build is still running |

### Incremental updates: when it is cheap and when it is not

```csharp
group.UpdateInstance(index, newTransform);
```

- The new position **still falls inside the same leaf block** → in-place update: one slot rewritten, `InPlaceUpdateCount++`, no tree work.
- The new position **lands in a different block** → the whole tree is invalidated and rebuilt asynchronously (`RebuildCount++`).
- While a build is **in flight**, any `UpdateInstance` invalidates and restarts it — an update cannot cut into a half-finished tree.

So the rule of thumb is simple: **small nudges are cheap, big teleports are not.** Verify it in the Gallery HISM demo, which exposes "nudge" and "teleport" as two modes; the readout tells you directly how much `InPlace` and `Rebuild` each gained.

```csharp
// Watch the statistics every frame to see whether your update pattern keeps rebuilding
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    if (group == null) return;

    Console.WriteLine($"instances {group.InstanceCount} · groups {group.GroupCount}" +
                      $" · in-place {group.InPlaceUpdateCount} · rebuilds {group.RebuildCount}" +
                      $" · building {group.IsBuilding}");
}
```

> [!IMPORTANT]
> Grouping runs on a background thread, and the result is adopted by **subsequent frames of main-thread updates**. With default continuous rendering you can ignore this. With auto-rendering off, request a few frames after each change (`RequestNextFrameRendering()`), otherwise you get the classic "the readout says `GroupCount` is 0 and nothing appears on screen".

## Point clouds are just a special case

Swap the source geometry for one with a **single vertex** and `PrimitiveType = PrimitiveType.Points`, and the instance matrix degenerates into "the position of that point". Add a custom shader that only writes `gl_PointSize` and you have a high-performance point cloud. Core also ships a built-in `PointCloudPipeline` that wires up the passes, point size and color attributes for this path (see [Choosing and Configuring Pipelines](./pipelines.md)); per-instance color and custom point primitives are covered in [Custom Materials and Shaders](./custom-material.md).

## Common pitfalls

- **Editing the source mesh's material does nothing**: `FromMesh` clones a material for the instanced mesh. Change `instancedMesh.Material` — colors, blend mode, custom shaders.
- **`new Mesh{...}` with `Material` inline**: `BaseColor` is an extension property and is not legal in an object initializer; write `var m = new Material();` then `m.BaseColor = ...`.
- **All instances stack at the origin / nothing moves**: you overrode the vertex shader but never multiplied by the per-instance matrix — the engine will not do it for you; see the `#ifdef INSTANCED_MESH` snippet above.
- **`SetInstanceAttribute` throws**: the data length must **equal the current `InstanceCount`**. Add instances first, attach the attribute second.
- **The per-instance attribute appears ignored**: the GLSL `location` does not match the slot table (custom attributes start at 16), or the `#ifdef INSTANCED_MESH` branch is missing.
- **Lighting goes grey or black after non-uniform scaling**: `InstanceNormalTransform` was disabled while the built-in pass still needs slots 12–15.
- **A wide-spread field is never culled**: `InstancedMesh` culls with the merged bounding box of all instances. That distribution is HISM's job.
- **HISM renders only a corner, or shows nothing at all**: the build had not been adopted yet (`IsBuilding`) when rendering stopped, or you changed `MaxInstancesPerGroup` / `MaxDepth` / `SetInstances` and forgot to call `Build()` again.
- **`Rebuild` climbs endlessly after updates**: you are teleporting instances. Reduce the step, or accept rebuilds and update less often.
- **Picked indices do not line up with HISM**: `Scene.Pick` returns a `PickResult.Node` that is one of the leaf `InstancedMesh` objects in `Groups`, and `InstanceIndex` is the index **within that leaf**, not the global index you passed to `SetInstances`. Keep your own per-leaf mapping if you need the global one.
- **Logic breaks after `RemoveInstance`**: every instance after it shifts down by one index; stop using stale indices.

## Runnable examples

- InstancedMesh (a 1200-instance wave field; left half uses built-in lighting, right half per-instance color with a custom shader, batch re-upload vs. per-instance update switchable): [InstancingDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Instancing/InstancingDemo.axaml.cs)
- HISM (12,000 instances, octree grouping, `InPlace` / `Rebuild` readout with "nudge" and "teleport" modes): [HismDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Hism/HismDemo.axaml.cs)
- Instance-aware shader pair and per-instance vertex-color material: [Kit/Shaders.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Kit/Shaders.cs)
- Node sources: [InstancedMesh.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/InstancedMesh.cs), [InstancedMeshGroup.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/InstancedMeshGroup.cs)

## Next steps

- Actually draw with per-instance attributes: [Custom Materials and Shaders](./custom-material.md)
- Instance data driven by particles: [Particle System](./particle-system.md)
- Pick a pipeline and configure `PipelineSettings`: [Choosing and Configuring Pipelines](./pipelines.md)
- How the instance buffers' VRAM gets handed back: [GPU Resource Lifecycle](./gpu-resource-lifecycle.md)
