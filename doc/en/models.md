---
section: basics
order: 2
---

# Loading and Placing Models

Get things into your 3D scene: load glTF/GLB or any of the 50+ Assimp formats (FBX, OBJ, ...), or build shapes from built-in geometries or raw vertex data, then place, inspect and copy them.

## Shortest runnable path: load a GLB and put it in the scene

Install the glTF package first:

```shell
dotnet add package Aura3D.Model.GltfLoader
```

Then load, place, and add it inside `SceneInitialized`:

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs args)
{
    var view = (Aura3DView)sender;
    var scene = args.Scene;

    // With the default pipeline (Blinn-Phong) nothing is visible without a light
    var light = new DirectionalLight { LightColor = Color.White };
    light.RotationDegrees = new Vector3(-45f, -25f, 0);
    scene.AddNode(light);

    var model = ModelLoader.LoadGlbModel("assets/stool.glb");
    model.Position = new Vector3(0, 0, 0);
    model.Scale = new Vector3(2f);
    view.AddNode(model);

    // Let the camera frame the model (see ./camera.md)
    view.MainCamera.FitToBoundingBox(model.BoundingBox, padding: 0.5f);
}
```

## ModelLoader: glTF / GLB

The `Aura3D.Model.GltfLoader` package provides these entry points:

```csharp
// From a file path (static model only)
var model = ModelLoader.LoadGlbModel("model.glb");

// From a Stream (embedded resources, downloaded bytes, etc.)
using (var stream = File.OpenRead("model.glb"))
{
    var model = ModelLoader.LoadGlbModel(stream);
}

// From a file path, animations included
var (model, animations) = ModelLoader.LoadGlbModelAndAnimations("model.glb");

// .gltf text format (geometry/textures usually live in sidecar .bin and image files)
var (model, animations) = ModelLoader.LoadGltfModelAndAnimations("model.gltf");
```

The returned `animations` is the list of animation clips; see the [Animation System](./animation.md) for playing them.

## AssimpLoader: FBX, OBJ and 50+ more formats

With the `Aura3D.Model.AssimpLoader` package installed, load formats beyond glTF through Assimp:

```csharp
// From a file (format auto-detected)
var (model, animations) = AssimpLoader.LoadModelAndAnimations("model.fbx");

// From a Stream (format suffix required)
using (var stream = File.OpenRead("model.obj"))
{
    var model = AssimpLoader.Load(stream, "obj");
}

// Load animations only, attached to an existing model's skeleton (animation library workflow)
using (var stream = File.OpenRead("walk.fbx"))
{
    var animations = AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx");
}
```

Assimp supports FBX, OBJ, 3DS, DAE, PLY, STL, DXF, MD5, LWO, MS3D and 40+ more formats. Note that Assimp relies on a native library and is mainly used on desktop.

## Placing: position, rotation, scale

A model is a [scene node](./scene-and-nodes.md) like any other, so transforms work the same way:

```csharp
model.Position = view.MainCamera.Forward * 3;     // 3 units in front of the camera
model.RotationDegrees = new Vector3(0, 180, 0);   // turn 180 degrees around Y
model.Scale = new Vector3(2f);                    // twice the size
view.AddNode(model);
```

Nodes also expose six read-only direction vectors: `Forward` / `Backward` / `Left` / `Right` / `Up` / `Down`. They update as the node rotates but **cannot be assigned** — change orientation through `RotationDegrees` or `RotationQuaternion`.

### Bounding box

Every model has a `BoundingBox` (two corners, `Min` and `Max`), used for camera framing, coarse picking, and debug drawing:

```csharp
var bbox = model.BoundingBox;
view.MainCamera.FitToBoundingBox(bbox, padding: 0.5f);

model.BoundingBoxPadding = 0.12f;   // add a margin around the auto-computed box

// Override the auto box when it doesn't fit your needs
model.CustomBoundingBox = new BoundingBox(
    new Vector3(-2f, -0.2f, -2f),
    new Vector3(2f, 3.4f, 2f));
```

## Accessing model parts

A model consists of several `Mesh` nodes (`model.Meshes`). Look a part up by `Name` to control it individually:

```csharp
var wheel = model.Meshes.First(mesh => mesh.Name == "wheel_front_left");
wheel.RotationDegrees = wheel.RotationDegrees with { X = 45f };  // turn just this wheel
```

`Name` comes from the node/mesh name in your modeling tool; print `model.Meshes.Select(m => m.Name)` to check names after loading. To search the whole subtree, use `model.GetNodesInChildren<Mesh>()`.

## Cloning and sharing

When the same model appears multiple times, clone it instead of loading the file again:

```csharp
// Share underlying resource data (geometry, textures are not copied). Ideal for repeated placement.
var clone = model.Clone(CopyType.SharedResourceData);
clone.Name = "stool-clone1";
clone.Position = new Vector3(2.6f, 0, 0);
scene.AddNode(clone);
```

`Clone` takes one of three `CopyType` values — `SharedResource`, `SharedResourceData`, `FullCopy` — from most sharing to most independence. For ordinary repeated placement, `SharedResourceData` is the default choice: transforms are independent, geometry and textures are not copied.

> [!WARNING]
> Sharing goes both ways: a clone may reference the same materials as the original. Swapping a texture on the clone (e.g. `mesh.Material.SetTexture("BaseColor", ...)`) also changes the original's appearance. Verify the sharing boundary with the ModelViewer gallery demo first, or switch to `FullCopy`.

## No model files needed: built-in geometries

Box, sphere, cylinder and plane work out of the box — good for prototypes, ground planes and debug objects:

```csharp
var mesh = new Mesh();

// Built-in geometries (Geometry subclasses; each has a default-size and a parameterized form)
mesh.Geometry = new BoxGeometry();                          // Box
mesh.Geometry = new BoxGeometry(2f, 1f, 3f);                // explicit width/height/depth
mesh.Geometry = new SphereGeometry();                       // Sphere
mesh.Geometry = new CylinderGeometry();                     // Cylinder
mesh.Geometry = new PlaneGeometry();                        // Plane (1x1)
mesh.Geometry = new PlaneGeometry(40f, 40f);                // Plane with custom size

mesh.Material = new Material();
mesh.Material.BaseColor = Texture.CreateFromColor(Color.White);
mesh.Material.DoubleSided = true;   // a single-sided plane stays visible from behind

mesh.Position = view.MainCamera.Forward * 3;
view.AddNode(mesh);
```

For tessellation control use the parameterized constructors, e.g. a sphere `new SphereGeometry(1.4f, 48, 24, phiLength: MathF.PI * 2)` or a plane `new PlaneGeometry(3f, 3f, 8, 8)` (segment counts change vertex counts — you rarely need fine subdivision).

> [!IMPORTANT]
> `BaseColor` is an extension property and **cannot** appear in an object initializer (`new Material { BaseColor = ... }` does not compile). Create the material first, then assign; or use the channel form `material.SetTexture("BaseColor", texture)`.

## Custom geometry and primitive types

When built-in shapes aren't enough, fill the vertex buffers yourself to build any shape. The core is a `Geometry` plus vertex attributes, indices, and a primitive type:

```csharp
var geometry = new Geometry();

// Primitive type: how the vertices/indices are interpreted (default: Triangles)
geometry.PrimitiveType = PrimitiveType.Triangles;

// Vertex attribute: Position is slot 0 with 3 components
geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, new List<float>
{
    -0.5f, -0.5f, 0,   // bottom-left
     0.5f, -0.5f, 0,   // bottom-right
     0.0f,  0.5f, 0,   // top
});

// Per-vertex color goes into the Color_0 slot with 4 components
geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, new List<float>
{
    1, 0, 0, 1,
    0, 1, 0, 1,
    0, 0, 1, 1,
});

// Indices: recommended for Triangles; without indices vertices are grouped by 3
geometry.SetIndices(new List<uint> { 0, 1, 2 });

// Normal, tangent, etc. are enabled by default; disable what you don't need to save bandwidth
geometry.SetAttributeEnabled(BuildInVertexAttribute.TexCoord_1, false);

var material = new Material { BlendMode = BlendMode.Opaque };
var mesh = new Mesh { Geometry = geometry, Material = material };
view.AddNode(mesh);
```

### The seven primitive types

The same vertices become a point cloud, a wireframe, or solid surfaces depending on the primitive type. With `n` indices:

| PrimitiveType | How data is interpreted | Primitive count | Typical use |
|---|---|---|---|
| `Points` | one point per vertex | n | point clouds |
| `Lines` | one independent segment per 2 indices | n / 2 | wireframes, axes |
| `LineStrip` | indices connected in order | n - 1 | trails, outlines |
| `LineLoop` | polyline closed at the ends | n | closed outlines |
| `Triangles` | one triangle per 3 indices (default) | n / 3 | regular meshes |
| `TriangleStrip` | each new vertex adds one triangle | n - 2 | strip-like grids |
| `TriangleFan` | vertices fan out around the first vertex | n - 2 | discs, fans |

> [!TIP]
> Points and lines that ignore lighting (wireframes, colored point clouds) now work directly in the default pipelines through plain material parameters — no pipeline switch or custom shader needed. For fully custom point sprites, use the NoLight pipeline with a custom shader; see [Custom Materials and Shaders](./custom-material.md). The Primitives gallery demo compares all seven primitive types, and the Pointcloud Mix demo renders a point cloud and a model in one scene.

### Point clouds: xyz/rgb .ply files and point-primitive materials

Meshes whose primitive type is `Points` work out of the box in every default pipeline
(Blinn-Phong, PBR forward & deferred, cel, NoLight): the draw call follows the primitive
type, the vertex shaders write `gl_PointSize`, the default is 1px, and the value is clamped
to the device's `ALIASED_POINT_SIZE_RANGE` — the ceiling is driver dependent, and ANGLE's
D3D/Metal backends differ the most, so mind the browser.

When `AssimpLoader` loads an xyz/rgb .ply (no faces, no normals — the file from issue #21),
it clones the mesh's material and presets three parameters; triangle meshes sharing the
material are unaffected:

| Material parameter | Meaning | .ply point cloud default |
|---|---|---|
| `Material.PointSizeParameterName` (`uPointSize`) | Point size in screen pixels | 3 |
| `Material.UseVertexColorParameterName` (`uUseVertexColor`) | Mix the `Color_0` vertex color into baseColor | 1 when colors present |
| `Material.UnlitParameterName` (`uUnlit`) | Skip lighting, show raw baseColor | 1 |

The unlit default matches mainstream point cloud viewers (CloudCompare and friends): pure
point clouds have no normals, so lighting is undefined; clouds with real normals can set
`uUnlit` to 0 to take part in lighting. All three parameters are independent switches on
any material and can be changed at runtime without rebuilding shaders.

## Common pitfalls

- **The model loads but everything is black**: the default Blinn-Phong pipeline needs a light. Add a `DirectionalLight` first — see [Lighting and Shadows](./lighting.md).
- **Missing loader package**: glTF and Assimp are separate NuGet packages (`Aura3D.Model.GltfLoader` / `Aura3D.Model.AssimpLoader`); `Aura3D.Avalonia` itself does not include loaders.
- **Assigning to `Forward` etc.**: the direction vectors are read-only and won't compile as assignment targets; set `RotationDegrees` to change orientation.
- **Editing a clone's texture changes the original too**: `SharedResourceData` clones share geometry and material data — see the warning above.
- **`new Material { BaseColor = ... }` fails to compile**: `BaseColor` is an extension property and cannot go in an object initializer.
- **Part names don't match**: names in `Meshes` are decided by the export tool; print the name list before looking parts up.

## Runnable examples

- [ModelViewer demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/ModelViewer/ModelViewerDemo.axaml.cs) — part tree, bounding boxes (incl. `BoundingBoxPadding` / `CustomBoundingBox`), and a side-by-side view of what each `CopyType` clone actually shares.
- [Geometries demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Geometries/GeometriesDemo.axaml.cs) — every constructor parameter of the four built-in geometries, with live vertex/index counts.
- [Primitives demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Primitives/PrimitivesDemo.axaml.cs) — one hand-written vertex set rendered through all seven primitive types.
- [AssimpFbx demo](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/AssimpFbx/AssimpFbxDemo.axaml.cs) — Assimp FBX loading plus animation libraries (`LoadAnimations` on a shared skeleton).

## Next steps

- [Cameras and View Control](./camera.md) — frame the model you just placed.
- [Materials and Textures](./material.md) — change how it looks.
- [Animation System](./animation.md) — play the clips you loaded.
