---
section: advanced
order: 1
---

# 实例化渲染

Aura3D 提供两种实例化方案：轻量级的 `InstancedMesh` 和层次化的 `InstancedMeshGroup`（类似 UE 的 HISM）。

## GPU 实例化 — InstancedMesh

对于需要渲染大量相同网格的场景（如粒子、植被、建筑群），使用 `InstancedMesh` 可以极大提升性能。

### 基本使用

```csharp
private InstancedMesh? instancedMesh;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;
    view.AutoRequestNextFrameRendering = false;

    // 创建源网格
    var sourceMesh = new Mesh();
    sourceMesh.Geometry = new BoxGeometry();
    sourceMesh.Material = new Material
    {
        BlendMode = BlendMode.Opaque,
    };
    sourceMesh.Material.BaseColor = Texture.CreateFromColor(Color.White);

    // 从源网格创建实例化网格
    instancedMesh = InstancedMesh.FromMesh(sourceMesh);

    // 批量添加实例
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

### 更新实例变换

```csharp
// 逐帧更新每个实例的变换（如旋转）
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

### 逐实例属性

除了变换矩阵，还可以为每个实例传递自定义属性（如颜色）：

```csharp
// 准备逐实例颜色数据
var colors = new List<Vector4>();
for (int i = 0; i < totalInstances; i++)
{
    colors.Add(new Vector4(
        (float)rand.NextDouble(),
        (float)rand.NextDouble(),
        (float)rand.NextDouble(),
        1.0f));
}

// 通过顶点属性传递（TexCoord_1 → location=15）
instancedMesh.SetInstanceAttribute<Vector4>(
    BuildInVertexAttribute.TexCoord_1, 4, colors);

// 可选：禁用不需要的实例属性以节省带宽
instancedMesh.SetAttributeEnabled("InstanceNormalTransform", false);
```

### 自定义实例着色器

要使用逐实例颜色，需要为材质的对应 Pass 编写自定义着色器，并在顶点着色器中声明实例颜色属性（`layout(location = 15) in vec4 instanceColor`），详见 [自定义材质着色器](./pipelines.md#自定义材质的着色器)。

## 层次化实例化 — InstancedMeshGroup (HISM)

`InstancedMeshGroup` 类似于 Unreal Engine 的 HISM（Hierarchical Instanced Static Mesh），自动将大量实例组织为层次化的空间分组。

### 优势

- **自动分组** — 按空间划分自动构建层次结构
- **增量更新** — 修改单实例时仅更新所在分组，无需重建全部
- **视锥体剔除** — 自动利用层次结构进行高效剔除

### 基本使用

```csharp
private InstancedMeshGroup? group;

private void BuildScene(Aura3DView view)
{
    // 清除旧的
    if (group != null) { view.Remove(group); }

    // 源网格
    var sourceMesh = new Mesh();
    sourceMesh.Geometry = new BoxGeometry();
    sourceMesh.Material = new Material
    {
        BlendMode = BlendMode.Opaque,
    };
    sourceMesh.Material.BaseColor = Texture.CreateFromColor(Color.White);

    // 创建 HISM 组
    group = new InstancedMeshGroup(sourceMesh)
    {
        MaxInstancesPerGroup = 64,   // 每组最大实例数
        MaxDepth = 6                 // 最大划分深度
    };

    // 生成实例变换矩阵
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

    // 设置实例并构建分组
    group.SetInstances(transforms);
    group.Build();

    view.AddNode(group);
}
```

### 增量更新

```csharp
// 修改单个实例的位置
var newTransform = Matrix4x4.CreateTranslation(new Vector3(10, 5, 10));
group.UpdateInstance(index, newTransform);

// 如果新位置仍在原分组内 → In-place 更新（高效）
// 如果新位置超出原分组 → 触发 Rebuild（自动重新分配）
```

### 监控统计

```csharp
// 在 SceneUpdated 中查看统计信息
int instanceCount = group.InstanceCount;       // 总实例数
int groupCount = group.GroupCount;             // 分组数量
int inPlaceUpdates = group.InPlaceUpdateCount; // 增量更新次数
int rebuildCount = group.RebuildCount;         // 重分配次数
```
