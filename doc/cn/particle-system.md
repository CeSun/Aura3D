---
section: basics
order: 8
---

# 粒子系统

**做出火焰、烟雾、雨雪、碎片这类粒子效果**。层级只有两层：`ParticleSystem` 节点管位置与播放/停止，下面挂若干 `ParticleEmitter`，管发射形状、粒子属性与渲染外观（广告牌或网格）。

> [!IMPORTANT]
> **纹理、Flipbook、网格、材质和混合模式都是发射器级的**，不是系统级的。同一个 `ParticleSystem` 里的不同发射器可以使用完全不同的渲染方式——例如爆炸场景中不透明碎片（网格 + `Opaque`）和半透明烟尘（纹理 + `Translucent`）共存于一个系统。

## 创建一个粒子效果（最短路径）

火焰的例子：建系统节点 → 加一个带纹理和混合模式的发射器 → 加入场景并 `Play()`。

```csharp
private ParticleSystem? _particles;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // 1. 创建粒子系统（不在此处设置渲染资源）
    _particles = new ParticleSystem
    {
        Name = "火焰",
        Position = new Vector3(0, 0, 0),
    };

    // 2. 添加发射器，带上它自己的纹理和混合模式
    var emitter = new ParticleEmitter
    {
        MaxParticles = 5000,
        BlendMode = BlendMode.Translucent,
        Texture = Texture.CreateFromFile("fire.png"),

        EmissionRate = 200f,
        Shape = EmissionShape.Circle,
        ShapeSize = new Vector3(2, 0, 2),

        Lifetime = new RangeFloat(1f, 3f),
        StartSize = new RangeFloat(0.3f, 0.6f),
        EndSize = new RangeFloat(0.01f, 0.05f),

        Velocity = new RangeVector3(
            new Vector3(-0.5f, 3f, -0.5f),
            new Vector3(0.5f, 8f, 0.5f)),

        StartColor = Color.Orange,
        EndColor = Color.Transparent,

        Gravity = new Vector3(0, 2f, 0),
        Damping = 0.5f,
    };
    _particles.Emitters.Add(emitter);

    // 3. 添加到场景并播放
    view.AddNode(_particles);
    _particles.Play();

    view.AutoRequestNextFrameRendering = true;
}
```

> [!NOTE]
> 粒子每帧都要模拟，记得让画面持续刷新：`view.AutoRequestNextFrameRendering = true`（或每帧调用 `RequestNextFrameRendering()`）。

不设置 `Texture` 时，Shader 用 `smoothstep` 画一个柔和的程序化圆形——不写贴图也能先跑起来调参数。

## 发射形状

内置 7 种发射形状。所有形状定义在**局部空间**（相对于 `ParticleSystem` 节点位置），发射的位置和速度会经过节点世界旋转的变换。

| 形状 | 说明 | ShapeSize 含义 |
|---|---|---|
| `Point` | 原点单点发射。 | 忽略 |
| `Sphere` | 球体内部均匀体积发射。 | `(X,Y,Z)` = 球体半径 |
| `SphereSurface` | 球体表面均匀发射。 | `(X,Y,Z)` = 球体半径 |
| `Box` | 轴对齐盒体内均匀发射。 | `(X,Y,Z)` = 盒体半边长 |
| `Cone` | 沿 +Y 轴的锥形发射，带扩散角。 | `X` = 底面半径，`Y` = 高度 |
| `Circle` | XZ 平面（Y=0）圆形面均匀发射。 | `(X,0,Z)` = 圆面半径 |
| `Hemisphere` | 上半球均匀体积发射（Y ≥ 0）。 | `(X,Y,Z)` = 半球半径 |

按效果选形状：

```
Point          → 精确单点发射（子弹、固定点火花）
Sphere         → 体积发射（爆炸、魔法光环）
SphereSurface  → 外壳发射（扩散冲击波）
Box            → 矩形区域发射（雨、雪）
Cone           → 定向喷射（火焰喷射器、喷泉）
Circle         → 平面圆形发射（篝火底部、喷泉底座）
Hemisphere     → 向上爆发（碎片爆炸、扬尘）
```

## 常用参数速查

`ParticleSystem`（系统级：位置与生命周期）：

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `MaxParticles` | `int` | `10000` | 系统级容量提示。仅在不播放时可修改。 |
| `Emitters` | `List<ParticleEmitter>` | `new()` | 发射器配置和运行时状态列表。 |
| `CustomBoundingBox` | `BoundingBox?` | `null` | 自定义世界空间包围盒覆写。 |
| `EnableVisibilityCulling` | `bool` | `false` | 离开相机视锥时跳过模拟。 |

只读：`IsPlaying`（是否播放中）、`ActiveCount`（所有发射器存活粒子数之和）、`WorldBoundingBox`（当前世界空间包围盒）。

`ParticleEmitter`（发射器级：外观与模拟）——渲染设置：

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `BlendMode` | `BlendMode` | `Translucent` | 此发射器的渲染混合模式。 |
| `Texture` | `ITexture?` | `null` | 广告牌纹理。null 时绘制程序化圆形。 |
| `FlipbookTiles` | `Vector2` | `(1,1)` | Flipbook 网格尺寸，如 `(8,8)` 表示 64 帧。 |
| `Mesh` | `Mesh?` | `null` | 设置后，此发射器激活网格模式。 |
| `Material` | `Material?` | `null` | 可选的网格模式材质覆写。 |
| `MaxParticles` | `int` | `1000` | 此发射器的最大粒子数。 |

发射设置：

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `EmissionRate` | `float` | `100` | 每秒发射粒子数。 |
| `Shape` | `EmissionShape` | `Point` | 发射形状，见[发射形状](#发射形状)。 |
| `ShapeSize` | `Vector3` | `(1,1,1)` | 发射形状尺寸（各轴缩放）。 |
| `ConeAngle` | `float` | `30` | 锥形扩散角度（度），仅 `Cone` 形状有效。 |
| `Looping` | `bool` | `true` | `false` 时，发射 `Duration` 秒后停止。 |
| `Duration` | `float` | `0` | 发射持续时间（秒），仅 `Looping = false` 时有效。 |

粒子属性：

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Lifetime` | `RangeFloat` | `(1, 3)` | 粒子生命周期的随机范围（秒）。 |
| `Velocity` | `RangeVector3` | `(0,5,0)~(0,10,0)` | 初始速度范围（局部空间）。 |
| `StartSize` | `RangeFloat` | `(0.1, 0.3)` | 初始大小范围。 |
| `EndSize` | `RangeFloat` | `(0.01, 0.05)` | 最终大小范围（随生命周期线性插值）。 |
| `StartColor` | `Color` | `White` | 初始颜色。 |
| `EndColor` | `Color` | `Transparent` | 最终颜色（随生命周期线性插值）。 |
| `Rotation` | `RangeFloat` | `(0, 2π)` | 初始旋转角度范围（弧度）。 |
| `AngularVelocity` | `RangeFloat` | `(-1, 1)` | 角速度范围（弧度/秒）。 |

物理：

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Gravity` | `Vector3` | `(0, -9.8, 0)` | 施加给粒子的重力。正 Y = 向上。 |
| `Damping` | `float` | `0` | 速度阻尼系数。 |

网格模式专用：`MeshScale`（`float`，默认 `1`，在粒子自身大小基础上再乘的缩放倍率）。运行时只读状态：`ElapsedTime`（自 Play 起经过的时间）、`IsFinished`（非循环且已到 Duration 时为 `true`）、`UseMeshRenderer`（设置了 `Mesh` 时为 `true`）。

所有数值在赋值时立即校验：速率、持续时间、阻尼和形状尺寸必须为有限非负数；生命周期必须大于 0；范围必须有限且 `Min <= Max`；`ConeAngle` 为 `[0, 90)` 度；`MaxParticles` 和 `MeshScale` 必须大于 0；`FlipbookTiles` 的两个分量必须是正整数。非法枚举值同样会被拒绝。

### 范围类型

大部分粒子属性接受随机范围而非定值：

```csharp
// 浮点范围
new RangeFloat(min, max);

// 向量范围（各分量独立随机）
new RangeVector3(min, max);
new RangeVector3(minX, minY, minZ, maxX, maxY, maxZ);
```

## 调外观：颜色、大小与物理

颜色在生命周期内从 `StartColor` 线性过渡到 `EndColor`：

```csharp
// 淡出（最常用）
emitter.StartColor = Color.White;
emitter.EndColor = Color.Transparent;

// 颜色渐变（火焰：橙色 → 红色）
emitter.StartColor = Color.Orange;
emitter.EndColor = Color.Red;

// 恒定颜色
emitter.StartColor = Color.Cyan;
emitter.EndColor = Color.Cyan;
```

大小同样在 `StartSize` → `EndSize` 间插值：

```csharp
// 缩小（火焰/烟雾常用）
emitter.StartSize = new RangeFloat(0.3f, 0.6f);
emitter.EndSize = new RangeFloat(0.01f, 0.05f);

// 放大（扩散效果）
emitter.StartSize = new RangeFloat(0.01f, 0.03f);
emitter.EndSize = new RangeFloat(0.3f, 0.5f);

// 恒定大小
emitter.StartSize = new RangeFloat(0.2f, 0.2f);
emitter.EndSize = new RangeFloat(0.2f, 0.2f);
```

每个发射器拥有**独立**的重力和阻尼：

```csharp
// 轻量漂浮粒子（烟雾）
emitter.Gravity = new Vector3(0, 0.5f, 0);
emitter.Damping = 0.8f;

// 重型碎片
emitter.Gravity = new Vector3(0, -15f, 0);
emitter.Damping = 0.2f;

// 失重太空粒子
emitter.Gravity = Vector3.Zero;
emitter.Damping = 0f;
```

### 循环发射 vs 一次性爆发

```csharp
// 循环（默认）—— 持续发射
emitter.Looping = true;

// 一次性爆发 —— 发射 Duration 秒后停止
emitter.Looping = false;
emitter.Duration = 2.0f;
emitter.EmissionRate = 500f;   // 总共产生 1000 个粒子

// 检查是否已结束
if (emitter.IsFinished) { /* ... */ }
```

### Flipbook 序列帧

```csharp
emitter.Texture = Texture.CreateFromFile("fire_flipbook.png");
emitter.FlipbookTiles = new Vector2(8, 8);   // 8 列 × 8 行 = 64 帧
```

片段着色器根据粒子的年龄比例（`AgeRatio`）选择对应帧：0% → 第 0 帧，50% → 第 32 帧，99% → 第 63 帧。纹理只有一帧时保持 `FlipbookTiles = (1,1)`，否则会被切成拼贴块。

### 多发射器混合外观

每个发射器可以有自己的纹理、模型和混合模式，一个系统内即可混出复合效果（如爆炸 = 不透明碎片 + 半透明烟尘）：

```csharp
var ps = new ParticleSystem { Position = new Vector3(0, 0, 0) };

// 不透明碎片发射器（网格模式）
ps.Emitters.Add(new ParticleEmitter
{
    MaxParticles = 500,
    BlendMode = BlendMode.Opaque,
    Mesh = Mesh.FromFile("debris.glb"),
    Shape = EmissionShape.Hemisphere,
    ShapeSize = new Vector3(1, 1, 1),
    EmissionRate = 200,
    Looping = false,
    Duration = 0.3f,
    Lifetime = new RangeFloat(1f, 3f),
    StartSize = new RangeFloat(0.2f, 0.5f),
    EndSize = new RangeFloat(0.1f, 0.3f),
    Velocity = new RangeVector3(new(-5, 8, -5), new(5, 15, 5)),
    Gravity = new Vector3(0, -15f, 0),
    Damping = 1.5f,
});

// 半透明烟尘发射器（广告牌模式）
ps.Emitters.Add(new ParticleEmitter
{
    MaxParticles = 300,
    BlendMode = BlendMode.Translucent,
    Texture = Texture.CreateFromFile("smoke.png"),
    Shape = EmissionShape.Circle,
    ShapeSize = new Vector3(2, 0, 2),
    EmissionRate = 50,
    Lifetime = new RangeFloat(2f, 6f),
    StartSize = new RangeFloat(0.5f, 1.5f),
    EndSize = new RangeFloat(0.01f, 0.1f),
    Velocity = new RangeVector3(new(-1, 1, -1), new(1, 3, 1)),
    StartColor = Color.FromArgb(128, 180, 180, 180),
    EndColor = Color.Transparent,
    Gravity = new Vector3(0, -1f, 0),
    Damping = 2f,
});
```

## 网格模式

当发射器的 `Mesh` 非空时，该发射器的粒子以 3D 模型实例渲染（引擎为其创建子 `InstancedMesh`，渲染走标准网格管线）。没有现成模型文件时，直接内联一个基础几何体网格也可以：

```csharp
var emitter = new ParticleEmitter
{
    Mesh = new Mesh
    {
        Name = "DebrisChunk",
        Geometry = new BoxGeometry(0.3f, 0.3f, 0.3f),
        Material = someMaterial,
    },
    MeshScale = 1f,
    // ...
};
```

网格粒子的朝向由旋转参数控制：

```csharp
// 绕 Y 轴自旋
emitter.Rotation = new RangeFloat(0, MathF.PI * 2);
emitter.AngularVelocity = new RangeFloat(-2f, 2f);
```

最终旋转 = 系统世界旋转 ∘ 粒子 Y 轴自旋；最终缩放 = `particle.CurrentSize × emitter.MeshScale`。

## 播放生命周期

```csharp
// 启动：为每个发射器分配粒子数组、创建 GPU 缓冲和 InstancedMesh，开始模拟
ps.Play();

// 暂停 / 恢复（切换暂停状态）
ps.Pause();

// 停止：停止模拟，释放每个发射器的资源和子节点
ps.Stop();

// 运行时修改参数（播放中允许）
ps.Emitters[0].EmissionRate = 500f;
ps.Emitters[0].StartColor = Color.Red;
ps.Emitters[0].Texture = newTexture;   // 下一帧生效

// 重播一次性爆发：Stop 再 Play
burst.Stop();
burst.Play();
```

注意：系统级 `MaxParticles` 仅在不播放时可修改；发射器级参数（速率、颜色、纹理等）播放中随时可改。

## 渲染行为（了解即可）

`ParticlePass` 按发射器各自的 `BlendMode` 分组渲染：

| BlendMode | 适用场景 | 行为 |
|---|---|---|
| `Opaque` | 不透明粒子（碎片、网格模式） | 深度写入开启，无混合 |
| `Masked` | 硬边缘粒子 | 深度写入开启，Alpha 测试 |
| `Translucent` | 柔和粒子（火焰、烟雾） | 预乘 Alpha 混合，深度写入关闭，从远到近排序 |

广告牌模式下渲染顺序为 Opaque → Masked → Translucent（半透明发射器间按系统中心距离、发射器内部粒子按到相机距离从远到近排序）。每个发射器还会独立选择 Shader 变体：无纹理（程序化圆形）/ 仅纹理（`PARTICLE_TEXTURE`）/ 纹理 + Flipbook（`PARTICLE_TEXTURE` + `PARTICLE_FLIPBOOK`）。

## 性能

1. **为每个发射器设置合适的 MaxParticles** —— `Play()` 时分配完整数组。
2. **高数量系统优先使用广告牌模式** —— GPU 负载更低。
3. **使用可见性剔除** —— `ps.EnableVisibilityCulling = true`，离屏时跳过模拟。
4. **为范围受限的系统设置 CustomBoundingBox**。
5. **使用较短的 Lifetime** —— 稳态活跃粒子数更低。
6. **尽量让发射器共享同一纹理/模型**。

监控活跃粒子数：

```csharp
int total = ps.ActiveCount;  // 所有发射器之和
foreach (var em in ps.Emitters)
    Console.WriteLine($"{em.ActiveCount} / {em.MaxParticles}");
```

调试时可为所有活跃粒子系统绘制橙色线框包围盒：

```csharp
view.Scene.RenderPipeline.Settings.Debug.ShowParticleBounds = true;
```

## ParticlePass 全局设置

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `DefaultParticleSize` | `float` | `1.0` | 保留字段（当前 Shader 中未使用）。 |
| `GlobalAlpha` | `float` | `1.0` | 应用到所有广告牌粒子的全局 Alpha 倍率。 |

```csharp
var particlePass = renderPipeline.FindPass<ParticlePass>();
particlePass.GlobalAlpha = 0.5f;
```

管线的选择与配置见 [./pipelines.md](./pipelines.md)。

## 常见问题

| 问题 | 可能原因 | 解决方案 |
|---|---|---|
| 看不到粒子 | 未调用 `Play()` | 设置完成后调用 `Play()`。 |
| 所有粒子挤在原点 | `ShapeSize` 太小 | 根据所选形状设置合理的 `ShapeSize`。 |
| 粒子不移动 | `Velocity` 设为零 | 设置非零速度或使用 `Gravity`。 |
| 网格模式：模型全黑 | 材质缺失 | 检查 `emitter.Mesh.Material` 或设置 `emitter.Material`。 |
| 半透明混合异常 | 混合模式错误 | 设置 `emitter.BlendMode = BlendMode.Translucent`。 |
| Flipbook 不播放动画 | 未设置 `FlipbookTiles` | 同时设置 `emitter.Texture` 和 `emitter.FlipbookTiles`。 |
| 剔除不生效 | `EnableVisibilityCulling = false` | 在 ParticleSystem 上设为 `true`。 |
| 模型缩放不对 | 未设置 `MeshScale` | 设置 `emitter.MeshScale` 为所需倍率。 |

画面完全不刷新（粒子停在第一帧）通常是 `AutoRequestNextFrameRendering` 未开启，更多通用排障见 [./troubleshooting.md](./troubleshooting.md)。

## 可运行示例

喷泉（锥形循环 + Flipbook）、一次性爆发（`SphereSurface` + `Looping=false`）、网格碎片（`Box` 几何体实例化）三条发射器的完整对照：

- <https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/Particles/ParticlesDemo.axaml.cs>

核心实现源码：[ParticleEmitter](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Particles/ParticleEmitter.cs)、[ParticleSystem](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Nodes/ParticleSystem.cs)。
