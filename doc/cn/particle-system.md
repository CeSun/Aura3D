# 粒子系统

Aura3D 粒子系统采用 **CPU 模拟 + GPU 实例化渲染**，每个发射器支持两种渲染模式：广告牌模式和网格模式。

每个 `ParticleEmitter` 拥有自己的粒子数组、GPU 缓冲和渲染资源（纹理、模型、混合模式）。一个 `ParticleSystem` 可以挂多个发射器，各自使用不同的视觉效果 —— 例如爆炸场景中不透明碎片和半透明烟尘共存于同一个系统。

## 架构概览

```
┌──────────────────────────────────────────────────────────────┐
│                     ParticleSystem (Node)                     │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                    Emitters[]                            │ │
│  │  ┌──────────────────────┐  ┌──────────────────────────┐ │ │
│  │  │  ParticleEmitter[0]  │  │  ParticleEmitter[1]      │ │ │
│  │  │  ├── Texture         │  │  ├── Mesh                │ │ │
│  │  │  ├── FlipbookTiles   │  │  ├── Material            │ │ │
│  │  │  ├── BlendMode       │  │  ├── BlendMode (Opaque)  │ │ │
│  │  │  ├── Particles[]     │  │  ├── Particles[]         │ │ │
│  │  │  ├── GpuBuffer       │  │  ├── InstancedMesh       │ │ │
│  │  │  └── (模拟参数)       │  │  └── (模拟参数)           │ │ │
│  │  └──────────────────────┘  └──────────────────────────┘ │ │
│  └─────────────────────────────────────────────────────────┘ │
│  系统级：Position, VisibilityCulling, Play/Stop/Pause        │
└──────────────────────────────────────────────────────────────┘
```

- **CPU 模拟**：每个发射器的粒子独立调用 `ParticleSimulation.Update()`。
- **GPU 实例化渲染**：每个发射器拥有自己的 `ParticleGpuBuffer`（广告牌）或 `InstancedMesh` 子节点（网格）。
- **发射器级资源**：纹理、Flipbook、模型、材质和混合模式都是发射器级别的 —— 同一系统的不同发射器可以使用完全不同的渲染方式。

---

## 快速开始

### 广告牌模式（默认）

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

### 网格模式

```csharp
private ParticleSystem? _debris;

private void CreateDebrisSystem(Aura3DView view)
{
    _debris = new ParticleSystem
    {
        Name = "碎片",
        Position = new Vector3(0, 3, 0),
        EnableVisibilityCulling = true,       // 可选：离屏时跳过模拟
    };

    var emitter = new ParticleEmitter
    {
        MaxParticles = 2000,
        BlendMode = BlendMode.Opaque,
        Mesh = Mesh.FromFile("debris.glb"),       // 要在发射器上实例化的模型
        // Material = someMaterial,               // 可选：覆写材质

        EmissionRate = 100f,
        Shape = EmissionShape.Hemisphere,
        ShapeSize = new Vector3(2, 2, 2),

        Lifetime = new RangeFloat(2f, 5f),
        StartSize = new RangeFloat(0.1f, 0.3f),
        EndSize = new RangeFloat(0.05f, 0.1f),

        Velocity = new RangeVector3(
            new Vector3(-2, 5, -2),
            new Vector3(2, 10, 2)),

        StartColor = Color.Gray,
        EndColor = Color.DarkGray,

        Gravity = new Vector3(0, -12f, 0),
        Damping = 2f,
        MeshScale = 1.5f,
    };
    _debris.Emitters.Add(emitter);

    view.AddNode(_debris);
    _debris.Play();
}
```

---

## 核心类

### ParticleSystem

主场景节点，继承自 `Node`。管理生命周期和共享资源 —— 渲染属性在 `ParticleEmitter` 上。

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `MaxParticles` | `int` | `10000` | 系统级容量提示。仅在不播放时可修改。 |
| `Emitters` | `List<ParticleEmitter>` | `new()` | 发射器配置和运行时状态列表。 |
| `CustomBoundingBox` | `BoundingBox?` | `null` | 自定义世界空间包围盒覆写。 |
| `EnableVisibilityCulling` | `bool` | `false` | 离开相机视锥时跳过模拟。 |

| 只读属性 | 类型 | 说明 |
|---|---|---|
| `IsPlaying` | `bool` | 是否正在播放。 |
| `ActiveCount` | `int` | 所有发射器的存活粒子数之和。 |
| `WorldBoundingBox` | `BoundingBox?` | 当前世界空间包围盒。 |

| 方法 | 说明 |
|---|---|
| `Play()` | 为每个发射器分配粒子数组、创建 GPU 缓冲和 InstancedMesh，开始模拟。 |
| `Stop()` | 停止模拟，释放每个发射器的资源和子节点。 |
| `Pause()` | 切换暂停状态。 |

### ParticleEmitter

每个发射器拥有自己的粒子、渲染资源和运行时状态。

#### 渲染设置

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `BlendMode` | `BlendMode` | `Translucent` | 此发射器的渲染混合模式。 |
| `Texture` | `ITexture?` | `null` | 广告牌纹理。null 时绘制程序化圆形。 |
| `FlipbookTiles` | `Vector2` | `(1,1)` | Flipbook 网格尺寸，如 `(8,8)` 表示 64 帧。 |
| `Mesh` | `Mesh?` | `null` | 设置后，此发射器激活网格模式。 |
| `Material` | `Material?` | `null` | 可选的网格模式材质覆写。 |
| `MaxParticles` | `int` | `1000` | 此发射器的最大粒子数。 |

#### 发射设置

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `EmissionRate` | `float` | `100` | 每秒发射粒子数。 |
| `Shape` | `EmissionShape` | `Point` | 发射形状。见[发射形状](#发射形状)。 |
| `ShapeSize` | `Vector3` | `(1,1,1)` | 发射形状尺寸（各轴缩放）。 |
| `ConeAngle` | `float` | `30` | 锥形扩散角度（度），仅 `Cone` 形状有效。 |
| `Looping` | `bool` | `true` | `false` 时，发射 `Duration` 秒后停止。 |
| `Duration` | `float` | `0` | 发射持续时间（秒），仅 `Looping = false` 时有效。 |

#### 粒子属性

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

#### 物理

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Gravity` | `Vector3` | `(0, -9.8, 0)` | 施加给粒子的重力。正 Y = 向上。 |
| `Damping` | `float` | `0` | 速度阻尼系数。 |

所有数值在赋值时立即校验：速率、持续时间、阻尼和形状尺寸必须为有限非负数；生命周期必须大于 0；范围必须有限且 `Min <= Max`；`ConeAngle` 为 `[0, 90)` 度；`MaxParticles` 和 `MeshScale` 必须大于 0；`FlipbookTiles` 的两个分量必须是正整数。非法枚举值同样会被拒绝。

#### 网格模式专用

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `MeshScale` | `float` | `1` | 在粒子自身大小基础上再乘的缩放倍率。 |

#### 运行时状态（只读）

| 属性 | 类型 | 说明 |
|---|---|---|
| `ElapsedTime` | `float` | 自 Play 以来经过的时间。 |
| `IsFinished` | `bool` | 非循环且已到 Duration 时为 `true`。 |
| `UseMeshRenderer` | `bool` | 设置了 `Mesh` 时为 `true`。 |

### 范围类型

```csharp
// 浮点范围
new RangeFloat(min, max);

// 向量范围（各分量独立随机）
new RangeVector3(min, max);
new RangeVector3(minX, minY, minZ, maxX, maxY, maxZ);
```

---

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

### 形状选择指南

```
Point          → 精确单点发射（子弹、固定点火花）
Sphere         → 体积发射（爆炸、魔法光环）
SphereSurface  → 外壳发射（扩散冲击波）
Box            → 矩形区域发射（雨、雪）
Cone           → 定向喷射（火焰喷射器、水花）
Circle         → 平面圆形发射（篝火底部、喷泉底座）
Hemisphere     → 向上爆发（碎片爆炸、扬尘）
```

---

## 配置指南

### 不同渲染方式的多发射器系统

每个发射器可以有自己的纹理、模型和混合模式。这让单个系统内可以混合不同的视觉效果：

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

### 颜色插值

粒子生命周期中，颜色从 `StartColor` 线性过渡到 `EndColor`：

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

### 生命周期内的大小变化

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

### 物理参数调优

每个发射器拥有独立的重力和阻尼：

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

### Flipbook 纹理

```csharp
emitter.Texture = Texture.CreateFromFile("fire_flipbook.png");
emitter.FlipbookTiles = new Vector2(8, 8);   // 8 列 × 8 行 = 64 帧
```

片段着色器根据 `AgeRatio` 选择对应帧：0% → 第 0 帧，50% → 第 32 帧，99% → 第 63 帧。

---

## 网格模式

当 `emitter.Mesh` 设置后，该发射器的粒子以 3D 模型实例渲染。

### 工作原理

1. `Play()` 时，每个网格模式发射器创建自己的 `InstancedMesh` 子节点。
2. 每帧，粒子变换被计算为 `缩放 × 旋转 × 平移` 矩阵。
3. 子 `InstancedMesh` 通过 `SetInstances()` 接收所有变换矩阵。
4. 渲染走标准网格管线。

### 网格模式粒子朝向

```csharp
// 绕 Y 轴自旋
emitter.Rotation = new RangeFloat(0, MathF.PI * 2);
emitter.AngularVelocity = new RangeFloat(-2f, 2f);
```

最终旋转 = 系统世界旋转 ∘ 粒子 Y 轴自旋。

### 网格缩放

```csharp
emitter.MeshScale = 2f;   // 模型放大到两倍
```

最终缩放 = `particle.CurrentSize × emitter.MeshScale`。

---

## 渲染细节

### 混合模式（发射器级）

每个发射器指定自己的 `BlendMode`。`ParticlePass` 按顺序渲染：

| BlendMode | 适用场景 | 行为 |
|---|---|---|
| `Opaque` | 不透明粒子（碎片、网格模式） | 深度写入开启，无混合 |
| `Masked` | 硬边缘粒子 | 深度写入开启，Alpha 测试 |
| `Translucent` | 柔和粒子（火焰、烟雾） | 预乘 Alpha 混合，深度写入关闭，从远到近排序 |

### 渲染顺序（广告牌模式）

1. **Opaque** 发射器先渲染。
2. **Masked** 发射器其次渲染。
3. **Translucent** 发射器最后渲染，按系统中心距离从远到近排序。
   - 每个半透明发射器内部，粒子按到相机的距离从远到近排序。

### Shader 变体（发射器级）

每个发射器独立选择 Shader 变体：

| 配置 | 宏定义 |
|---|---|
| 无纹理 | (无) — 程序化圆形 |
| 仅纹理 | `PARTICLE_TEXTURE` |
| 纹理 + Flipbook | `PARTICLE_TEXTURE`, `PARTICLE_FLIPBOOK` |

---

## 生命周期管理

```csharp
// 启动
ps.Play();

// 暂停 / 恢复
ps.Pause();

// 停止
ps.Stop();

// 运行时修改参数（播放中允许）
ps.Emitters[0].EmissionRate = 500f;
ps.Emitters[0].StartColor = Color.Red;
ps.Emitters[0].Texture = newTexture;   // 下一帧生效
```

---

## 性能

### 优化建议

1. **为每个发射器设置合适的 MaxParticles** —— `Play()` 时分配完整数组。
2. **高数量系统优先使用广告牌模式** —— GPU 负载更低。
3. **使用可见性剔除** —— `ps.EnableVisibilityCulling = true`。
4. **为范围受限的系统设置 CustomBoundingBox**。
5. **使用较短的 Lifetime** —— 稳态活跃粒子数更低。

### 性能监控

```csharp
int total = ps.ActiveCount;  // 所有发射器之和
foreach (var em in ps.Emitters)
    Console.WriteLine($"{em.ActiveCount} / {em.MaxParticles}");
```

---

## 调试可视化

```csharp
debugSettings.ShowParticleBounds = true;
```

为所有活跃粒子系统绘制橙色线框包围盒。

---

## 完整示例：带火花的火焰

两个发射器共享同一个 Flipbook 纹理，使用不同的模拟参数：

```csharp
private ParticleSystem? _fire;

private void CreateFire(Aura3DView view)
{
    var fireTex = Texture.CreateFromFile("Assets/fire_8x8.png");

    _fire = new ParticleSystem
    {
        Name = "火焰",
        Position = new Vector3(0, 0, 0),
    };

    // 主火焰
    _fire.Emitters.Add(new ParticleEmitter
    {
        MaxParticles = 4000,
        BlendMode = BlendMode.Translucent,
        Texture = fireTex,
        FlipbookTiles = new Vector2(8, 8),

        EmissionRate = 300f,
        Shape = EmissionShape.Circle,
        ShapeSize = new Vector3(1.5f, 0, 1.5f),
        Lifetime = new RangeFloat(0.5f, 1.5f),
        StartSize = new RangeFloat(0.3f, 0.6f),
        EndSize = new RangeFloat(0.01f, 0.1f),
        Velocity = new RangeVector3(new(-0.3f, 3f, -0.3f), new(0.3f, 6f, 0.3f)),
        StartColor = Color.Orange,
        EndColor = Color.Transparent,
        Gravity = new Vector3(0, 2f, 0),
        Damping = 0.3f,
    });

    // 火花
    _fire.Emitters.Add(new ParticleEmitter
    {
        MaxParticles = 1000,
        BlendMode = BlendMode.Translucent,
        Texture = fireTex,
        FlipbookTiles = new Vector2(8, 8),

        EmissionRate = 60f,
        Shape = EmissionShape.Cone,
        ShapeSize = new Vector3(0.3f, 2f, 0.3f),
        ConeAngle = 15f,
        Lifetime = new RangeFloat(0.8f, 2.5f),
        StartSize = new RangeFloat(0.05f, 0.12f),
        EndSize = new RangeFloat(0.01f, 0.03f),
        Velocity = new RangeVector3(new(-1f, 5f, -1f), new(1f, 10f, 1f)),
        StartColor = Color.Yellow,
        EndColor = Color.Red,
        Gravity = new Vector3(0, -1f, 0),
        Damping = 0.1f,
    });

    view.AddNode(_fire);
    _fire.Play();
    view.AutoRequestNextFrameRendering = true;
}
```

---

## 完整示例：爆炸（混合不透明网格 + 半透明广告牌）

一个系统，两个发射器，完全不同的渲染方式：

```csharp
private ParticleSystem? _explosion;

private void CreateExplosion(Aura3DView view, Vector3 position)
{
    _explosion = new ParticleSystem
    {
        Name = "爆炸",
        Position = position,
    };

    // 不透明碎片（网格模式）—— 一次性爆发
    _explosion.Emitters.Add(new ParticleEmitter
    {
        MaxParticles = 500,
        BlendMode = BlendMode.Opaque,
        Mesh = Mesh.FromFile("Assets/debris.glb"),

        EmissionRate = 800f,
        Looping = false,
        Duration = 0.2f,
        Shape = EmissionShape.Hemisphere,
        ShapeSize = new Vector3(1, 1, 1),
        Lifetime = new RangeFloat(2f, 4f),
        StartSize = new RangeFloat(0.1f, 0.4f),
        EndSize = new RangeFloat(0.05f, 0.1f),
        Velocity = new RangeVector3(new(-5, 8, -5), new(5, 15, 5)),
        StartColor = Color.LightGray,
        EndColor = Color.DarkGray,
        Gravity = new Vector3(0, -15f, 0),
        Damping = 1.5f,
        MeshScale = 1.2f,
        Rotation = new RangeFloat(0, MathF.PI * 2),
        AngularVelocity = new RangeFloat(-3f, 3f),
    });

    // 半透明烟尘（广告牌）—— 循环
    var smokeTex = Texture.CreateFromFile("Assets/smoke.png");
    _explosion.Emitters.Add(new ParticleEmitter
    {
        MaxParticles = 300,
        BlendMode = BlendMode.Translucent,
        Texture = smokeTex,

        EmissionRate = 50f,
        Shape = EmissionShape.Circle,
        ShapeSize = new Vector3(3, 0, 3),
        Lifetime = new RangeFloat(2f, 6f),
        StartSize = new RangeFloat(0.5f, 1.5f),
        EndSize = new RangeFloat(0.01f, 0.1f),
        Velocity = new RangeVector3(new(-1f, 1f, -1f), new(1f, 3f, 1f)),
        StartColor = Color.FromArgb(128, 180, 160, 140),
        EndColor = Color.Transparent,
        Gravity = new Vector3(0, -1f, 0),
        Damping = 2f,
    });

    view.AddNode(_explosion);
    _explosion.Play();
}
```

---

## ParticlePass 全局设置

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `DefaultParticleSize` | `float` | `1.0` | 保留字段（当前 Shader 中未使用）。 |
| `GlobalAlpha` | `float` | `1.0` | 应用到所有广告牌粒子的全局 Alpha 倍率。 |

```csharp
var particlePass = renderPipeline.FindPass<ParticlePass>();
particlePass.GlobalAlpha = 0.5f;
```

---

## 常见问题排查

| 问题 | 可能原因 | 解决方案 |
|---|---|---|
| 看不到粒子 | 未调用 `Play()` | 设置完成后调用 `Play()`。 |
| 粒子不动态更新 | 未设置 `AutoRequestNextFrameRendering` | 设置 `view.AutoRequestNextFrameRendering = true`。 |
| 所有粒子在原点 | `ShapeSize` 太小 | 根据所选形状设置合理的 `ShapeSize`。 |
| 粒子不移动 | `Velocity` 设为零 | 设置非零速度或使用 `Gravity`。 |
| 网格模式：模型全黑 | 材质缺失 | 检查 `emitter.Mesh.Material` 或设置 `emitter.Material`。 |
| 半透明混合异常 | 混合模式错误 | 设置 `emitter.BlendMode = BlendMode.Translucent`。 |
| Flipbook 不播放动画 | 未设置 `FlipbookTiles` | 同时设置 `emitter.Texture` 和 `emitter.FlipbookTiles`。 |
| 剔除不生效 | `EnableVisibilityCulling = false` | 在 ParticleSystem 上设为 `true`。 |
| 模型缩放不对 | 未设置 `MeshScale` | 设置 `emitter.MeshScale` 为所需倍率。 |
