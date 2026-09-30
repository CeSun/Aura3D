---
section: basics
order: 7
---

# 动画系统

**让带动画的角色模型动起来，并控制它怎么动**：

- 加载带骨骼动画的模型并播放、切歌、调速、循环或单次播放；
- 需要逐帧掌控时，手动推进动画时间（定格、慢放、步进）；
- 模型和动画不在同一个文件时（常见 FBX 工作流），用 Assimp 外挂动画；
- 用 2D 混合空间在待机/前后左右移动之间平滑过渡；
- 用动画状态图管理"待机 → 行走 → 跑步"这类带条件的状态切换；
- 绕过采样器直接读写骨骼矩阵，做程序化动画、IK 或挂附件。

> [!NOTE]
> 动画采样器输出的骨骼矩阵最终由蒙皮网格消费。模型节点的加载与部件组织见 [./models.md](./models.md)，场景节点基础见 [./scene-and-nodes.md](./scene-and-nodes.md)。

## 加载并播放骨骼动画

最短可跑路径：加载 glb 里的模型和动画数组，给第一个剪辑建一个 `AnimationSampler`，挂到 `model.AnimationSampler` 上即可自动播放。

```csharp
private Model? model;
private AnimationSampler? animationSampler;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // 加载模型和动画
    using var stream = File.OpenRead("character.glb");
    var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(stream);

    // 创建动画采样器并绑定到模型
    animationSampler = new AnimationSampler(animations[0]);
    animationSampler.TimeScale = 1.0f;  // 播放速度
    model.AnimationSampler = animationSampler;

    model.Position = new Vector3(0, 0, 3);
    view.AddNode(model);

    // 添加光源
    var dl = new DirectionalLight();
    dl.RotationDegrees = new Vector3(-30, 0, 0);
    dl.LightColor = Color.White;
    view.AddNode(dl);
}
```

只要 `AnimationSampler`（或后面提到的混合空间、状态图）挂在 `model.AnimationSampler` 上，场景每帧更新时就会自动推进采样并驱动蒙皮，不需要你写每帧代码。

### 切换动画

换剪辑就是换采样器：为目标 `Animation` 新建一个 `AnimationSampler` 并重新绑定。

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

### 循环模式与重置

`AnimationSampler` 提供三种循环模式：

```csharp
var sampler = new AnimationSampler(animation);

// 循环播放（默认）
sampler.LoopMode = LoopMode.Loop;

// 播放一次后停在末尾
sampler.LoopMode = LoopMode.Once;

// 来回乒乓播放
sampler.LoopMode = LoopMode.PingPong;

// 重置动画回到开头
sampler.Reset();
```

播放速度用 `TimeScale` 控制（`1.0` 为原速，`0.5` 为半速慢放）。

### 手动控制动画时间

默认情况下 `AnimationSampler` 用系统时间自动推进。设置 `ExternalUpdate = true` 后，时间完全由你调用 `Update` 推进——适合定格播放、逐帧检查或服务器同步：

```csharp
sampler.ExternalUpdate = true;

// 在 SceneUpdated 中手动推进
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    sampler.Update(e.DeltaTime);
}
```

> [!NOTE]
> 混合空间（`AnimationBlendSpace`）和状态图（`AnimationGraph`）同样适用：它们的 `Update` 会自动更新内部所有采样器。如果设置了 `ExternalUpdate = true`，只需手动调用**顶层**采样器的 `Update`。

### 使用 Assimp 加载外部动画

当模型和动画在不同文件时（常见于 FBX 工作流），先加载模型，再把动画文件绑定到模型的骨骼上：

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

## 2D 混合空间：按输入方向 blend 多个动画

混合空间把多条动画摆放在二维平面的各个位置，根据一组 `(x, y)` 参数在它们之间做距离加权混合——典型的 locomotion 设置：待机放原点，前后左右移动放四个方向。

```csharp
private AnimationBlendSpace? blendSpace;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // ... 加载模型和动画 ...

    // 创建混合空间（基于模型的骨骼）
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

挂法和单条动画完全一样——模型不关心 `AnimationSampler` 位置放的是采样器还是混合空间。加权方式可通过 `IdwPower` 调整（反距离加权幂次，默认 2，越大越"偏向"最近的动画）。

## 动画状态图：按条件切换并交叉淡化

状态图（`AnimationGraph`）适合管理"待机 → 行走 → 跑步"这类带转换条件的状态机。每个状态是一个 `AnimationGraphNode`，节点间的转换用一个条件函数声明，切换时按 `BlendTime` 做交叉淡化。

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

    // 创建状态图并绑定，第二个参数是入口节点
    var graph = new AnimationGraph(model.Skeleton, idleNode);
    model.AnimationSampler = graph;

    view.AddNode(model);
}
```

每帧检查当前节点出边的条件，条件满足时自动切换到目标状态，过渡的平滑程度由 `BlendTime` 控制。

## 骨骼手动操作

除了依赖动画采样器，你也可以直接读写骨骼变换，用于程序化动画、反向动力学（IK）或布娃娃系统。调试时可用 `view.Scene.RenderPipeline.Settings.Debug.ShowBone = true;` 画出骨骼。

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

### 读取骨骼矩阵

```csharp
// 读取骨骼的世界矩阵（当前帧的计算结果）
var boneIndex = skeleton.GetBoneIndex("Head");
Matrix4x4 worldMatrix = skeleton.Bones[boneIndex].WorldMatrix;

// 读取骨骼的局部矩阵（相对父骨骼）
Matrix4x4 localMatrix = skeleton.Bones[boneIndex].LocalMatrix;

// 读取骨骼的逆世界矩阵（蒙皮用，一般只读）
Matrix4x4 invWorldMatrix = skeleton.Bones[boneIndex].InverseWorldMatrix;
```

### 把物体挂到骨骼上

想让火把跟着手掌走？不需要手写矩阵同步，用 `BoneAttachment` 节点声明"挂在哪个骨骼上"即可：

```csharp
var attachment = new BoneAttachment
{
    Mesh = targetSkinnedMesh,          // 动画所属的蒙皮网格
    BoneName = "LeftHand",             // 目标骨骼名
    LocalOffset = Matrix4x4.CreateTranslation(new Vector3(0, 0.35f, 0)),
};
view.AddNode(attachment);
attachment.AddChild(torchMesh, AttachToParentRule.KeepLocal);
```

## 常见坑

> [!WARNING]
> **直接在 `SceneUpdated` 里修改骨骼矩阵不会生效。** 骨骼矩阵在动画采样阶段由 `IAnimationSampler.Update()` 计算，你在事件回调里的写入会被下一次采样覆盖。要实现程序化骨骼控制，需要自定义 `IAnimationSampler`，或在动画采样之后覆盖矩阵。

> [!WARNING]
> **骨骼网格的包围盒是 T-Pose 静态计算的。** 出于性能考虑，骨骼网格体不会逐帧按骨骼位置重新计算包围盒，而是用静态顶点数据生成 T-Pose 包围盒。如果动画使模型明显超出该包围盒（如行走、跳跃），视锥剔除可能错误地裁掉仍在视野内的网格。解决办法：

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

按需设置即可，静止模型无需调整。更多剔除相关问题见 [./troubleshooting.md](./troubleshooting.md)。

## 可运行示例

- 骨骼动画播放/循环模式/手动时间/骨骼附件：<https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/SkinnedAnimation/SkinnedAnimationDemo.axaml.cs>
- 状态图 + 2D 混合空间对照：<https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/AnimationMix/AnimationMixDemo.axaml.cs>

核心实现源码：[AnimationSampler](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/AnimationSampler.cs)、[AnimationBlendSpace](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/AnimationBlendSpace.cs)、[AnimationGraph](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/AnimationGraph.cs)。
