---
section: advanced
order: 1
---

# 实例化渲染

**一次 draw call 画出成千上万个物体**——实例化把「同一个网格画 N 遍」压成一次绘制。Aura3D 提供两条路：

- **`InstancedMesh`** —— 一张平铺的实例表，矩阵由你填、由你改，随时想怎么改就怎么改。
- **`InstancedMeshGroup`** —— 类似 Unreal 的 HISM（Hierarchical Instanced Static Mesh）：八叉树把实例切成若干叶子分组，每组内部就是一个 `InstancedMesh`，剔除按组来做。

两者都是普通节点，`view.AddNode(...)` 挂进场景即可，都吃当前管线的光照、阴影与材质通道。

## 先选一条路

| 你要画的东西 | 选哪个 | 为什么 |
|---|---|---|
| 同一份网格、几十到几千个、整片基本同时在视野里 | `InstancedMesh` | 一次 draw call，实例表随便改，没有额外结构要维护 |
| 同一份网格、上万级、散布在很大区域、相机每次只看到一角 | `InstancedMeshGroup` | 剔除按八叉树分组做，看不见的组整组不进入绘制；代价是要 `Build()`，增量更新有条件 |
| 逐帧全量刷新的动态数据（波形、扫描、粒子） | `InstancedMesh` + `SetInstances` | 位置每帧全变，空间分组没有意义，省掉建树 |
| 大量**互不相同**的网格 | 都不是 | 实例化要求几何与材质同源；网格不同就只能各自一个 `Mesh` |

> [!TIP]
> 判据其实只有一条：**这些物体是不是同一份几何 + 同一份材质**。是，就先考虑实例化；再问第二个问题——相机是不是经常只看得到其中一部分。是，就上 HISM。

## 最短可跑：一片 InstancedMesh

10×10×10 = 1000 个立方体，一次 draw call：

```csharp
private InstancedMesh? field;
private readonly List<Vector3> positions = [];
private readonly List<float> angles = [];

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // 1. 先准备一份普通网格作为「源」
    var source = new Mesh
    {
        Geometry = new BoxGeometry(),
        Material = new Material { BlendMode = BlendMode.Opaque },
    };
    source.Material.SetTexture("BaseColor", Texture.CreateFromColor(Color.White));

    // 2. 由源网格生成实例网格
    field = InstancedMesh.FromMesh(source);

    const int gridSize = 10;
    const float spacing = 2.5f;
    var offset = (gridSize - 1) * spacing / 2f;

    // 3. 逐个加实例，每个一份变换矩阵
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

`AddInstance` 返回新实例的下标，`field.InstanceCount` 是当前实例数。想一上来就知道数量、并且之后整表替换，用 `SetInstances(list)` 更省事。

> [!WARNING]
> 一大片实例很容易超出相机默认远裁剪面（`FarPlane` 默认 100），现象是「近处的画了、远处整片没了」。把相机拉远或扩大场地时记得 `camera.FarPlane = 120f;` 之类抬一下——Gallery 两个实例化示例都显式抬了它。

## 让实例动起来

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

三个常用动作：

```csharp
field.UpdateInstance(i, transform);   // 改一个实例
field.SetInstances(allTransforms);    // 整表换掉：动态数据推荐走这条
field.RemoveInstance(0);              // 移除一个实例，它之后的下标整体前移
```

> [!NOTE]
> 这些调用只写 CPU 侧的实例缓冲，并把资源的版本号加一；渲染时管线比对 `Version` 与 `SyncedVersion`，不一致才把整块实例缓冲重传一次。所以**同一帧内改 1 个和改 1000 个，GPU 侧代价相同**——要动的东西一帧内一起动完，别摊到好几帧去「省流量」。如果你关了自动渲染（`AutoRequestNextFrameRendering = false`），记得像上面那样自己 `RequestNextFrameRendering()`。

## 实例矩阵就是世界矩阵

`InstancedMesh` 自己的 `Position` / `Rotation` / `Scale` **不会**作用到实例上：每个实例的槽位 8–11 直接就是它的 `modelMatrix`。要整体搬动一整片实例，就把偏移乘进每个实例矩阵（或者换一层：把整片实例放进不同 `InstancedMesh` 节点，各自加常量偏移）。

这也是实例化剔除的代价：普通 `Mesh` 按自己的包围盒剔除，而 `InstancedMesh` 剔除时用的是**全部实例合并后的**那个大盒子——铺得很开的一片实例基本永远在视锥里，等于不剔除。这一点正是下面 HISM 存在的理由。

## 逐实例自定义属性

除了变换，还能给每个实例挂一份自己的数据（最常见是颜色）。做法是把它塞进一个空闲的顶点属性槽位，然后让你的着色器去读它：

```csharp
var colors = new List<Vector4>();

for (var i = 0; i < field.InstanceCount; i++)
{
    var t = i / (float)Math.Max(field.InstanceCount - 1, 1);
    colors.Add(new Vector4(t, 1f - t, 0.35f + 0.5f * MathF.Sin(t * 14f), 1f));
}

// 注意顺序：先有实例，再挂逐实例属性，数量必须等于 InstanceCount
field.SetInstanceAttribute<Vector4>(BuildInVertexAttribute.TexCoord_1, 4, colors);
```

槽位是**约定的、按枚举数值直接分配**的，所以自定义属性要挑没被占用的：

| 槽位 | 内容 |
|---|---|
| 0–7 | 逐顶点属性：0 位置、1 UV、2 顶点色、3 法线、4 切线、5 副切线、6 骨骼索引、7 骨骼权重 |
| 8–11 | 逐实例 `modelMatrix`（`InstancedTransformColumn0..3`） |
| 12–15 | 逐实例 `normalMatrix`（`InstancedNormalTransformColumn0..3`） |
| 16 起 | 你自己的逐实例属性：`TexCoord_1 = 16`、`TexCoord_2 = 17` …… |

内置 Pass 的着色器不认识你新塞的槽位，所以要用它就得覆盖对应 Pass 的顶点着色器——完整的材质级着色器写法见[自定义材质与着色器](./custom-material.md)。这里只需要记住两件事：**声明的 `location` 要和槽位表对齐**，以及**接管顶点阶段就等于接管了实例变换**，逐实例矩阵得自己乘：

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
> Gallery 用的那对实例感知着色器（逐实例色 + `#ifdef INSTANCED_MESH` 分支）就在 [Kit/Shaders.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Kit/Shaders.cs) 的 `InstancedColorVertex`，它把逐实例色放在槽 2（`BuildInVertexAttribute.Color_0`）——源网格自己没有顶点色时，这个槽也是空的，可以直接借用。

## 关掉用不到的实例属性

每个实例默认要带两份 `mat4`：变换矩阵（槽 8–11）和它的法线变换矩阵（槽 12–15），也就是 128 字节/实例。实例数上万时这很可观。不需要逐实例法线矩阵就别传它：

```csharp
field.SetAttributeEnabled("InstanceNormalTransform", false);
```

关掉后引擎不会为这个属性建缓冲、也不会启用槽 12–15，省下的是显存和每帧重传的带宽。

> [!WARNING]
> 内置 `base.vert`（BlinnPhong / PBR 的 `LightPass` 等）在 `INSTANCED_MESH` 下**会**声明并使用 `layout(location = 12) in mat4 normalMatrix` 来做 TBN。所以：只有在你的自定义着色器不读 12–15 时才关（例如只做常量色、逐实例色的无光照路径，或均匀缩放下你直接用 `mat3(modelMatrix)` 旋转法线）。给实例上了非均匀缩放、又关掉了它，光照就会明显不对。

## 上万个散布实例：InstancedMeshGroup 的 HISM 分组

`InstancedMeshGroup` 把一张实例表交给八叉树切成若干叶子块，每个叶子块是一个真正的 `InstancedMesh`（挂在组节点下面，从 `Groups` 里能拿到）。相机看不见的块整块不进绘制列表，于是「一万棵草、只看到一角」这种场景能省下大量绘制。

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
        MaxInstancesPerGroup = 512,   // 一个叶子块最多多少个实例
        MaxDepth = 6,                 // 八叉树最多切几层
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
    group.Build();          // 启动一次分组构建

    view.AddNode(group);    // 只加组节点，叶子块由它自己管
}
```

常用参数：

| 参数 / 成员 | 默认 | 作用与调法 |
|---|---|---|
| `MaxInstancesPerGroup` | 1024 | 叶子块容量。调小 → 块更多、剔除更精准，但 draw call 也更多 |
| `MaxDepth` | 6 | 八叉树最深切分层次。实例分布很密很散时可加深；调太深会让小块碎成一地 |
| `SetInstances(list)` | — | 整表替换实例（会作废旧分组） |
| `AddInstance(t)` / `AddInstances(list)` | — | 追加实例（同样作废旧分组） |
| `RemoveInstance(i)` / `ClearInstances()` | — | 移除单个 / 全部清空 |
| `Build()` | — | 显式启动一次构建；改上面两个参数后也要再 `Build()` |
| `Groups` | — | 当前叶子块（`InstancedMesh`）列表，可逐块设 `EnableFrustumCulling` |
| `InstanceCount` / `GroupCount` | — | 实例总数 / 当前分组数 |
| `InPlaceUpdateCount` / `RebuildCount` | 0 | 原地更新次数 / 重建次数（每次重建后前者归零） |
| `IsBuilding` | — | 后台构建是否还在跑 |

### 增量更新：什么时候便宜，什么时候不

```csharp
group.UpdateInstance(index, newTransform);
```

- 新位置**仍然落在该实例原来那个叶子块里** → 原地更新：只改那一格，`InPlaceUpdateCount++`，不建树。
- 新位置**跨到了别的块** → 整棵树作废，异步重建（`RebuildCount++`）。
- 构建**正在进行**时任何 `UpdateInstance` 都会作废重建——不能指望它插队进半棵树。

所以经验规则很简单：**小步挪动是免费的，大幅瞬移是昂贵的**。想验证就跑 Gallery 的 HISM 示例，它把「挪一点」和「瞬移」做成两个模式，读数会直接告诉你 `InPlace` 与 `Rebuild` 各涨了多少。

```csharp
// 每帧看统计，判断你的更新模式是不是在反复重建
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    if (group == null) return;

    Console.WriteLine($"实例 {group.InstanceCount} · 分组 {group.GroupCount}" +
                      $" · 原地 {group.InPlaceUpdateCount} · 重建 {group.RebuildCount}" +
                      $" · 构建中 {group.IsBuilding}");
}
```

> [!IMPORTANT]
> 分组构建跑在后台线程，结果要**由后续几帧的主线程更新来收尾**。开着默认连续渲染就不用管；关了自动渲染时，改完记得连着请求几帧（`RequestNextFrameRendering()`），否则会出现「统计里 `GroupCount` 一直是 0、画面什么都没有」。

## 点云只是它的特例

把源几何换成**只有一个顶点**、`PrimitiveType = PrimitiveType.Points` 的几何，实例矩阵就退化成「每个点的位置」，再配一段只写 `gl_PointSize` 的自定义着色器，就是一套高性能点云。Core 里另有一条内置的 `PointCloudPipeline` 把这条路径的 Pass 和点大小、颜色属性都准备好了（见[选择与配置管线](./pipelines.md)），逐实例色与自定义点图元着色器的写法在[自定义材质与着色器](./custom-material.md)。

## 常见坑

- **改了源网格的材质没反应**：`FromMesh` 会克隆一份材质给实例网格。要换颜色、混合模式、自定义着色器，改 `instancedMesh.Material`。
- **用 `new Mesh{...}` 直传 `Material`**：`BaseColor` 是扩展属性，不能写进对象初始化器；先 `var m = new Material();` 再 `m.BaseColor = ...`。
- **实例全叠在原点 / 整片不动**：你覆盖了顶点着色器却没乘逐实例矩阵——引擎不会替你乘，见上面那段 `#ifdef INSTANCED_MESH`。
- **`SetInstanceAttribute` 抛异常**：传入的数据条数必须**等于当前 `InstanceCount`**。先加实例，再挂逐实例属性。
- **逐实例属性不生效**：GLSL 里的 `location` 没对上槽位表（自定义属性从 16 起），或者忘了 `#ifdef INSTANCED_MESH` 分支。
- **上了非均匀缩放后光照发灰 / 发黑**：把 `InstanceNormalTransform` 关掉了，而内置 Pass 需要槽 12–15。
- **铺得很开的一片实例完全没有剔除**：`InstancedMesh` 的剔除盒是全实例合并的大盒子。这种分布该换 `InstancedMeshGroup`。
- **HISM 只显示了一小片 / 什么都没显示**：构建还没收尾（`IsBuilding`）就停止了渲染请求；或改完 `MaxInstancesPerGroup`、`MaxDepth`、`SetInstances` 之后忘了再 `Build()`。
- **HISM 更新后 `Rebuild` 疯涨**：你在做大幅瞬移。改小步长，或者接受重建、把更新频率降下来。
- **HISM 上拾取到的下标对不上**：`Scene.Pick` 返回的 `PickResult.Node` 是 `Groups` 里的某个叶子 `InstancedMesh`，`InstanceIndex` 是**该叶子内**的下标，不是 `SetInstances` 里的那个全局下标。要映射回去，得自己按叶子分组维护一份对应表。
- **`RemoveInstance` 之后逻辑错乱**：它会把后面所有实例的下标整体前移，别再用旧下标。

## 可运行示例

- InstancedMesh（1200 个实例的波浪场，左半边吃内置光照、右半边用逐实例色 + 自定义着色器，可切「批量重传 / 逐个更新」）：[InstancingDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Instancing/InstancingDemo.axaml.cs)
- HISM（12000 个实例、八叉树分组，带 `InPlace` / `Rebuild` 读数和「挪一点 / 瞬移」两种更新模式）：[HismDemo.axaml.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Hism/HismDemo.axaml.cs)
- 实例感知着色器对与逐实例顶点色材质：[Kit/Shaders.cs](https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Kit/Shaders.cs)
- 节点源码：[InstancedMesh.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/InstancedMesh.cs)、[InstancedMeshGroup.cs](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/InstancedMeshGroup.cs)

## 下一步

- 把逐实例属性真正画出来：[自定义材质与着色器](./custom-material.md)
- 逐实例数据驱动的粒子：[粒子系统](./particle-system.md)
- 选一条管线、配 `PipelineSettings`：[选择与配置管线](./pipelines.md)
- 实例缓冲占的显存怎么归还：[GPU 资源生命周期](./gpu-resource-lifecycle.md)
