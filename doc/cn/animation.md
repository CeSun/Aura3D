---
section: basics
order: 3
---

# 动画系统

Aura3D 支持从简单的骨骼动画播放到复杂的状态机和混合空间。

## 骨骼动画

### 基本骨骼动画

加载带动画的 glTF 模型并播放：

```csharp
private Model? model;
private AnimationSampler? animationSampler;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // 加载模型和动画
    using var stream = File.OpenRead("character.glb");
    var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(stream);

    // 创建动画采样器并绑定
    animationSampler = new AnimationSampler(animations[0]);
    animationSampler.TimeScale = 1.0f;  // 播放速度
    model.AnimationSampler = animationSampler;

    model.Position = view.MainCamera.Forward * 3;
    view.AddNode(model);

    // 添加光源
    var dl = new DirectionalLight();
    dl.RotationDegrees = new Vector3(-30, 0, 0);
    dl.LightColor = Color.White;
    view.AddNode(dl);
}
```

### 切换动画

```csharp
// 当用户选择不同动画时
private void SwitchAnimation(string animationName)
{
    var targetAnim = animations.First(a => a.Name == animationName);
    animationSampler = new AnimationSampler(targetAnim);
    animationSampler.TimeScale = currentSpeed;
    model.AnimationSampler = animationSampler;
}
```

### 循环模式

`AnimationSampler` 提供三种循环模式：

```csharp
var sampler = new AnimationSampler(animation);

// 循环播放（默认）
sampler.LoopMode = LoopMode.Loop;

// 播放一次后停止
sampler.LoopMode = LoopMode.Once;

// 来回乒乓播放
sampler.LoopMode = LoopMode.PingPong;

// 重置动画到开头
sampler.Reset();
```

### 手动控制动画时间

默认情况下，`AnimationSampler` 使用系统时间自动推进。设置 `ExternalUpdate = true` 后，你需要手动调用 `Update` 来控制时间：

```csharp
sampler.ExternalUpdate = true;

// 在 SceneUpdated 中手动推进
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    sampler.Update(e.DeltaTime);
}
```

> 适用于动画混合空间和动画状态图：它们的 `Update` 会自动更新内部所有采样器。如果设置了 `ExternalUpdate = true`，需要手动调用顶层的 `Update`。

### 骨骼网格体包围盒

出于性能考虑，骨骼网格体不会逐帧按骨骼位置重新计算包围盒，而是使用静态顶点数据生成一个 T-Pose 包围盒。如果动画使模型明显超出该包围盒（如行走、跳跃），可能导致视锥体剔除错误地裁剪掉仍在视野内的网格。

可通过 `Model.BoundingBoxPadding` 在各方向扩展包围盒：

```csharp
// 各方向扩大 2 个单位，确保动画位移不被剔除
model.BoundingBoxPadding = new Vector3(2f);
```

或指定自定义包围盒完全覆盖动画范围：

```csharp
model.CustomBoundingBox = new BoundingBox(
    new Vector3(-5, 0, -5),
    new Vector3(5, 10, 5));
```

> 按需设置即可，静止模型无需调整。

### 使用 Assimp 加载外部动画

当模型和动画在不同文件中时（常见于 FBX 工作流）：

```csharp
// 先加载模型
using (var stream = File.OpenRead("character.fbx"))
{
    model = AssimpLoader.Load(stream, "fbx");
}

// 再加载动画（绑定到模型的骨骼）
using (var stream = File.OpenRead("walk.fbx"))
{
    var anims = AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx");
    model.AnimationSampler = new AnimationSampler(anims[0]);
}
```

## 动画混合空间

2D 混合空间可以在多个动画间根据二维参数（如移动方向和速度）平滑过渡。

```csharp
private AnimationBlendSpace? blendSpace;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // ... 加载模型和动画 ...

    // 创建混合空间
    blendSpace = new AnimationBlendSpace(model.Skeleton);

    // 在二维空间的各方向放置动画
    blendSpace.AddAnimationSampler(new Vector2(0, 0),   // 原点：待机
        new AnimationSampler(idleAnim));
    blendSpace.AddAnimationSampler(new Vector2(0, 1),   // 上方：前进
        new AnimationSampler(walkForwardAnim));
    blendSpace.AddAnimationSampler(new Vector2(0, -1),  // 下方：后退
        new AnimationSampler(walkBackAnim));
    blendSpace.AddAnimationSampler(new Vector2(-1, 0),  // 左方：左移
        new AnimationSampler(walkLeftAnim));
    blendSpace.AddAnimationSampler(new Vector2(1, 0),   // 右方：右移
        new AnimationSampler(walkRightAnim));

    // 对角线方向的动画（可选）
    blendSpace.AddAnimationSampler(new Vector2(-1, -1),
        new AnimationSampler(walkBackLeftAnim));
    blendSpace.AddAnimationSampler(new Vector2(1, -1),
        new AnimationSampler(walkBackRightAnim));

    model.AnimationSampler = blendSpace;
    view.AddNode(model);
}

// 每帧更新混合参数
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    blendSpace?.SetAxis(inputX, inputY);  // X、Y 在 [-1, 1] 范围内
}
```

## 动画状态图

状态图（AnimationGraph）适合管理复杂的状态机，例如角色的"待机 → 行走 → 跑步"过渡。

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    using var stream = File.OpenRead("character.glb");
    var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(stream);

    // 创建状态节点
    var idleNode = new AnimationGraphNode(new AnimationSampler(animations[0]));
    idleNode.BlendTime = 0.5;  // 过渡混合时间（秒）

    var walkNode = new AnimationGraphNode(new AnimationSampler(animations[3]));
    walkNode.BlendTime = 0.3;

    var runNode = new AnimationGraphNode(new AnimationSampler(animations[1]));
    runNode.BlendTime = 0.2;

    // 定义状态转换条件
    // AddNextNode(条件函数, 目标节点)
    // 条件函数参数：(IAnimationSampler current, double deltaTime)
    idleNode.AddNextNode((sampler, dt) => Speed > 0.01, walkNode);
    walkNode.AddNextNode((sampler, dt) => Speed > 0.8, runNode);
    walkNode.AddNextNode((sampler, dt) => Speed < 0.01, idleNode);
    runNode.AddNextNode((sampler, dt) => Speed < 0.8, walkNode);

    // 创建状态图并绑定
    var graph = new AnimationGraph(model.Skeleton, idleNode);
    model.AnimationSampler = graph;

    view.AddNode(model);
}
```

每帧检查条件，当条件满足时自动切换到目标状态，过渡由 `BlendTime` 控制平滑度。

## 骨骼手动操作

除了依赖动画采样器，你也可以直接读写骨骼变换，用于程序化动画、反向动力学（IK）或布娃娃系统。

### 遍历骨骼

```csharp
var skeleton = model.Skeleton;

// 通过名称获取骨骼索引
int index = skeleton.GetBoneIndex("LeftArm");
// 或获取完整映射
var boneMap = skeleton.GetBoneIndexMap();

// 遍历骨骼树
void TraverseBone(Bone bone, int depth)
{
    Console.WriteLine($"{new string(' ', depth)}{bone.Name} (index={bone.Index})");
    foreach (var child in bone.Children)
        TraverseBone(child, depth + 1);
}
TraverseBone(skeleton.Root, 0);
```

### 读写骨骼矩阵

```csharp
// 读取骨骼的世界矩阵（当前帧的计算结果）
var boneIndex = skeleton.GetBoneIndex("Head");
Matrix4x4 worldMatrix = skeleton.Bones[boneIndex].WorldMatrix;

// 读取骨骼的局部矩阵（相对父骨骼）
Matrix4x4 localMatrix = skeleton.Bones[boneIndex].LocalMatrix;

// 读取骨骼的逆世界矩阵（蒙皮用，一般只读）
Matrix4x4 invWorldMatrix = skeleton.Bones[boneIndex].InverseWorldMatrix;
```

> **注意**：直接在 `SceneUpdated` 中修改骨骼矩阵不会生效——骨骼矩阵在动画采样阶段由 `IAnimationSampler.Update()` 计算。要实现程序化骨骼控制，需要自定义 `IAnimationSampler` 或在动画采样之后覆盖矩阵。
