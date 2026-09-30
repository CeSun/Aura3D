# Rendering Topics

Covers shadows, point clouds, primitive rendering, and node operations — advanced rendering-related topics.

## Shadows

All three light types support shadow casting, enabled via the `CastShadow` property.

### Directional Light Shadows

```csharp
var dl = new DirectionalLight();
dl.CastShadow = true;
dl.ShadowConfig.FarPlane = 500;    // Far clip plane
dl.ShadowConfig.NearPlane = 1;     // Near clip plane
dl.ShadowConfig.Width = 2048;      // Shadow map width
dl.ShadowConfig.Height = 2048;     // Shadow map height
```

### Point Light Shadows

```csharp
var pl = new PointLight();
pl.CastShadow = true;
pl.AttenuationRadius = 10f;  // Affects both light range and shadow range
```

### Spot Light Shadows

```csharp
var sp = new SpotLight();
sp.CastShadow = true;
```

> **Note**: Shadow rendering incurs additional performance cost. Enable only when needed, and set shadow map resolution appropriately.

### Cascaded Shadow Maps (CSM)

Directional light shadows can show aliasing at long distances. CSM solves this by splitting the view frustum into multiple cascades, each with its own shadow map.

```csharp
var dl = new DirectionalLight();
dl.CastShadow = true;
view.AddNode(dl);

// Set as main directional light → uses CSM; other directional lights fall back to a single shadow map
view.Scene.MainDirectionalLight = dl;
```

CSM parameters are configured via `PipelineSettings` (see [Rendering Pipelines → Pipeline Settings](./pipelines.md#pipeline-settings)):

| Parameter | Default | Description |
|---|---|---|
| `CsmCascadeCount` | 3 | Number of cascades; set to 1 to fall back to a single shadow map |
| `CsmSplitLambda` | 0.5 | Split parameter (0=uniform, 1=logarithmic) |
| `CsmShadowMapResolution` | 1024 | Shadow map resolution per cascade |

## Click Picking

Pick objects in the scene by screen coordinates with triangle-level precision:

```csharp
// Call in a mouse click event handler after obtaining screen coordinates
List<PickResult> results = view.Scene.Pick(screenX, screenY, view.MainCamera);

// Or get only the closest result
PickResult? closest = view.Scene.PickClosest(screenX, screenY, view.MainCamera);

if (closest != null)
{
    var node = closest.Value.Node;              // The hit node
    var worldPos = closest.Value.WorldPosition; // World position of the hit point
    var distance = closest.Value.Distance;      // Distance to camera
    var instanceIndex = closest.Value.InstanceIndex; // InstancedMesh instance index (null for regular Mesh)
}
```

Supports precise picking of regular Mesh, InstancedMesh, and CPU-skinned skeletal meshes.

## Point Cloud Rendering

High-performance point clouds using `InstancedMesh` + single-vertex geometry + custom shaders:

```csharp
private void BuildPointCloud(Aura3DView view, int pointCount)
{
    // Create single-point geometry
    var geometry = new Geometry();
    geometry.PrimitiveType = PrimitiveType.Points;
    geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3,
        new List<float> { 0, 0, 0 });

    var sourceMesh = new Mesh { Geometry = geometry };

    var material = new Material { BlendMode = BlendMode.Opaque };

    // Custom shaders
    material.SetShaderSource("LightPass", ShaderType.Vertex, pointCloudVertShader);
    material.SetShaderSource("LightPass", ShaderType.Fragment, pointCloudFragShader);
    sourceMesh.Material = material;

    // Create instances
    var instancedMesh = InstancedMesh.FromMesh(sourceMesh);
    instancedMesh.SetAttributeEnabled("InstanceNormalTransform", false);

    // Generate random points within a sphere
    var rand = new Random(42);
    float radius = 10f;
    var colors = new List<Vector4>();

    for (int i = 0; i < pointCount; i++)
    {
        // Uniform volume distribution in sphere
        float theta = (float)(rand.NextDouble() * Math.PI * 2);
        float phi = (float)(Math.Acos(2 * rand.NextDouble() - 1));
        float r = (float)(radius * Math.Cbrt(rand.NextDouble()));

        float x = r * (float)(Math.Sin(phi) * Math.Cos(theta));
        float y = r * (float)(Math.Sin(phi) * Math.Sin(theta));
        float z = r * (float)(Math.Cos(phi));

        instancedMesh.AddInstance(Matrix4x4.CreateTranslation(x, y, z));
        colors.Add(new Vector4(
            (x / radius + 1f) / 2f,
            (y / radius + 1f) / 2f,
            (z / radius + 1f) / 2f,
            1.0f));
    }

    instancedMesh.SetInstanceAttribute<Vector4>(
        BuildInVertexAttribute.TexCoord_1, 4, colors);

    view.AddNode(instancedMesh);
}
```

Point cloud shader essentials:
- Set `gl_PointSize` in the vertex shader
- Clip to a circle using `gl_PointCoord` in the fragment shader to avoid square points

## Primitive Rendering

Beyond standard triangle rendering, Aura3D supports all OpenGL primitive types:

```csharp
var geometry = new Geometry();

// Choose primitive type
geometry.PrimitiveType = PrimitiveType.Triangles;      // Triangles (default)
// geometry.PrimitiveType = PrimitiveType.Points;       // Points
// geometry.PrimitiveType = PrimitiveType.Lines;        // Independent lines
// geometry.PrimitiveType = PrimitiveType.LineStrip;    // Polyline
// geometry.PrimitiveType = PrimitiveType.LineLoop;     // Closed polyline
// geometry.PrimitiveType = PrimitiveType.TriangleStrip;// Triangle strip
// geometry.PrimitiveType = PrimitiveType.TriangleFan;  // Triangle fan

// Set vertex data
geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, new List<float>
{
    -0.5f, -0.5f, 0,
     0.5f, -0.5f, 0,
     0.0f,  0.5f, 0,
});

// Optional: set indices (recommended for Triangles type)
geometry.SetIndices(new List<uint> { 0, 1, 2 });

// Use custom shader
var material = new Material { BlendMode = BlendMode.Opaque };
material.SetShaderSource("LightPass", ShaderType.Vertex, solidColorVertShader);
material.SetShaderSource("LightPass", ShaderType.Fragment, solidColorFragShader);
material.SetShaderPassParametersCallback("LightPass", pass =>
{
    pass.UniformVector4("uColor", new Vector4(1, 0, 0, 1));
});

var mesh = new Mesh { Geometry = geometry, Material = material };
view.AddNode(mesh);
```

## Advanced Materials

### Material Channels

```csharp
var material = new Material
{
    BlendMode = BlendMode.Opaque,
    DoubleSided = false,
};

// Set base color channel
material.Channels = new List<Channel>
{
    new Channel
    {
        Name = "BaseColor",
        Texture = texture,
    }
};

// Or quickly set BaseColor
material.BaseColor = texture;
```

### Blend Modes

```csharp
material.BlendMode = BlendMode.Opaque;       // Opaque (default)
// material.BlendMode = BlendMode.Masked;     // Alpha cutout
// material.BlendMode = BlendMode.Translucent; // Translucent
```

## Node Operations

### Model Bounding Box

```csharp
// Get the model's bounding box
var bbox = model.BoundingBox;  // BoundingBox { Min, Max }

// Auto-fit the camera to the bounding box
camera.FitToBoundingBox(bbox, padding: 0.5f);
```

### Cloning and Sharing

```csharp
// Share geometry and texture data (recommended for mass instancing)
var cloned = model.Clone(CopyType.SharedResourceData);
```

### Direct Child Part Manipulation

Find and independently control parts of a loaded model by name:

```csharp
var part = model.Meshes.First(m => m.Name == "wheel_front_left");
part.RotationDegrees = new Vector3(rotationAngle, 0, 0);
```

### Batch Transform Updates

Repeatedly modifying Position, Rotation, and Scale triggers multiple world matrix recalculations. Use `BeginTransformUpdate` to batch changes:

```csharp
using (node.BeginTransformUpdate(UpdateTransformMode.LocalOnly))
{
    node.Position = new Vector3(10, 0, 5);
    node.RotationDegrees = new Vector3(0, 90, 0);
    node.Scale = new Vector3(2f);
}
// World matrix recalculated once when the using block ends
```

### Finding Child Nodes

```csharp
// Recursively find all child nodes of a given type
var allMeshes = model.GetNodesInChildren<Mesh>();
var allLights = view.Scene.MainCamera.GetNodesInChildren<Light>();

// Filter specific nodes
var specificNodes = model.GetNodesInChildren<Node>()
    .Where(n => n.Tags.Contains("pickable"));
```

## Multi-Camera & Resource Management

For multi-camera rendering and GPU resource lifecycle management, see the [Rendering Pipeline documentation](./pipelines.md#multi-camera-rendering).
