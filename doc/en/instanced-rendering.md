---
section: advanced
order: 1
---

# Instanced Rendering

Aura3D provides two instancing solutions: the lightweight `InstancedMesh` and the hierarchical `InstancedMeshGroup` (similar to UE's HISM).

## GPU Instancing — InstancedMesh

For scenes requiring large numbers of identical meshes (particles, vegetation, buildings), `InstancedMesh` dramatically improves performance.

### Basic Usage

```csharp
private InstancedMesh? instancedMesh;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;
    view.AutoRequestNextFrameRendering = false;

    // Create the source mesh
    var sourceMesh = new Mesh();
    sourceMesh.Geometry = new BoxGeometry();
    sourceMesh.Material = new Material
    {
        BlendMode = BlendMode.Opaque,
    };
    sourceMesh.Material.BaseColor = Texture.CreateFromColor(Color.White);

    // Create the instanced mesh from the source
    instancedMesh = InstancedMesh.FromMesh(sourceMesh);

    // Batch-add instances
    var rand = new Random(42);
    float spacing = 2.5f;
    int gridSize = 10;
    float offset = (gridSize - 1) * spacing / 2f;

    for (int x = 0; x < gridSize; x++)
    {
        for (int y = 0; y < gridSize; y++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                var pos = new Vector3(
                    x * spacing - offset,
                    y * spacing - offset,
                    z * spacing - offset);
                var transform = Matrix4x4.CreateTranslation(pos);
                instancedMesh.AddInstance(transform);
            }
        }
    }

    view.AddNode(instancedMesh);
    view.RequestNextFrameRendering();
}
```

### Updating Instance Transforms

```csharp
// Update each instance's transform per frame (e.g., rotation)
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    if (instancedMesh == null) return;

    for (int i = 0; i < instancedMesh.InstanceCount; i++)
    {
        rotationAngles[i] += rotationSpeeds[i] * (float)e.DeltaTime;
        var rotation = Matrix4x4.CreateFromYawPitchRoll(
            rotationAngles[i],
            rotationAngles[i] * 0.7f,
            rotationAngles[i] * 0.3f);
        var transform = rotation * Matrix4x4.CreateTranslation(positions[i]);
        instancedMesh.UpdateInstance(i, transform);
    }

    (sender as Aura3DView)?.RequestNextFrameRendering();
}
```

### Per-Instance Attributes

Beyond transform matrices, you can pass custom attributes per instance (e.g., colors):

```csharp
// Prepare per-instance color data
var colors = new List<Vector4>();
for (int i = 0; i < totalInstances; i++)
{
    colors.Add(new Vector4(
        (float)rand.NextDouble(),
        (float)rand.NextDouble(),
        (float)rand.NextDouble(),
        1.0f));
}

// Pass via vertex attribute (TexCoord_1 → location=15)
instancedMesh.SetInstanceAttribute<Vector4>(
    BuildInVertexAttribute.TexCoord_1, 4, colors);

// Optional: disable unneeded instance attributes to save bandwidth
instancedMesh.SetAttributeEnabled("InstanceNormalTransform", false);
```

### Custom Instance Shaders

To use per-instance colors, write custom shaders for the material's corresponding Pass and declare the instance color attribute in the vertex shader (`layout(location = 15) in vec4 instanceColor`). See [Custom Material Shaders](./pipelines.md#custom-material-shaders).

## Hierarchical Instancing — InstancedMeshGroup (HISM)

`InstancedMeshGroup` is similar to Unreal Engine's HISM (Hierarchical Instanced Static Mesh), automatically organizing large numbers of instances into a hierarchical spatial grouping.

### Advantages

- **Auto-grouping** — Automatically builds a hierarchical structure by spatial division
- **Incremental updates** — Updating a single instance only affects its group, no full rebuild needed
- **Frustum culling** — Leverages the hierarchy for efficient culling

### Basic Usage

```csharp
private InstancedMeshGroup? group;

private void BuildScene(Aura3DView view)
{
    // Clear old group
    if (group != null) { view.Remove(group); }

    // Source mesh
    var sourceMesh = new Mesh();
    sourceMesh.Geometry = new BoxGeometry();
    sourceMesh.Material = new Material
    {
        BlendMode = BlendMode.Opaque,
    };
    sourceMesh.Material.BaseColor = Texture.CreateFromColor(Color.White);

    // Create HISM group
    group = new InstancedMeshGroup(sourceMesh)
    {
        MaxInstancesPerGroup = 64,   // Max instances per leaf group
        MaxDepth = 6                 // Max octree depth
    };

    // Generate instance transform matrices
    int gridSize = 100;
    float spacing = 3f;
    var transforms = new List<Matrix4x4>();
    float offset = (gridSize - 1) * spacing / 2f;

    for (int x = 0; x < gridSize; x++)
    {
        for (int z = 0; z < gridSize; z++)
        {
            float px = x * spacing - offset;
            float pz = z * spacing - offset;
            float py = (float)(Math.Sin(x * 0.3) * Math.Cos(z * 0.3) * 2.0);
            var t = Matrix4x4.CreateTranslation(px, py, pz);
            transforms.Add(t);
        }
    }

    // Set instances and build the groups
    group.SetInstances(transforms);
    group.Build();

    view.AddNode(group);
}
```

### Incremental Updates

```csharp
// Update a single instance's position
var newTransform = Matrix4x4.CreateTranslation(new Vector3(10, 5, 10));
group.UpdateInstance(index, newTransform);

// If the new position stays within the original group → In-place update (efficient)
// If the new position falls outside → Triggers rebuild (auto-reassignment)
```

### Monitoring Statistics

```csharp
// Check statistics in SceneUpdated
int instanceCount = group.InstanceCount;       // Total instances
int groupCount = group.GroupCount;             // Number of groups
int inPlaceUpdates = group.InPlaceUpdateCount; // Incremental update count
int rebuildCount = group.RebuildCount;         // Rebuild count
```
