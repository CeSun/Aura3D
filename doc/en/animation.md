---
section: basics
order: 3
---

# Animation System

Aura3D supports everything from simple skeletal animation playback to complex state machines and blend spaces.

## Skeletal Animation

### Basic Skeletal Animation

Load an animated glTF model and play it:

```csharp
private Model? model;
private AnimationSampler? animationSampler;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // Load model and animations
    using var stream = File.OpenRead("character.glb");
    var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(stream);

    // Create animation sampler and bind it
    animationSampler = new AnimationSampler(animations[0]);
    animationSampler.TimeScale = 1.0f;  // Playback speed
    model.AnimationSampler = animationSampler;

    model.Position = view.MainCamera.Forward * 3;
    view.AddNode(model);

    // Add a light
    var dl = new DirectionalLight();
    dl.RotationDegrees = new Vector3(-30, 0, 0);
    dl.LightColor = Color.White;
    view.AddNode(dl);
}
```

### Switching Animations

```csharp
// When the user selects a different animation
private void SwitchAnimation(string animationName)
{
    var targetAnim = animations.First(a => a.Name == animationName);
    animationSampler = new AnimationSampler(targetAnim);
    animationSampler.TimeScale = currentSpeed;
    model.AnimationSampler = animationSampler;
}
```

### Loop Modes

`AnimationSampler` provides three loop modes:

```csharp
var sampler = new AnimationSampler(animation);

// Loop playback (default)
sampler.LoopMode = LoopMode.Loop;

// Play once then stop
sampler.LoopMode = LoopMode.Once;

// Ping-pong back and forth
sampler.LoopMode = LoopMode.PingPong;

// Reset animation to the beginning
sampler.Reset();
```

### Manual Animation Time Control

By default, `AnimationSampler` advances automatically using system time. Set `ExternalUpdate = true` to take manual control via `Update`:

```csharp
sampler.ExternalUpdate = true;

// Advance manually in SceneUpdated
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    sampler.Update(e.DeltaTime);
}
```

> This applies to blend spaces and animation graphs as well: their `Update` automatically updates all internal samplers. If `ExternalUpdate = true`, call `Update` on the top-level sampler.

### Skeletal Mesh Bounding Boxes

For performance reasons, skeletal meshes do not recompute bounding boxes per-frame based on bone positions. Instead, a T-Pose bounding box is generated from the static vertex data. If the animation moves the model significantly beyond this box (e.g., walking, jumping), frustum culling may incorrectly cull meshes that are still in view.

Use `Model.BoundingBoxPadding` to expand the bounding box in all directions:

```csharp
// Expand by 2 units in each direction to prevent culling during animation
model.BoundingBoxPadding = new Vector3(2f);
```

Or specify a custom bounding box that fully covers the animation range:

```csharp
model.CustomBoundingBox = new BoundingBox(
    new Vector3(-5, 0, -5),
    new Vector3(5, 10, 5));
```

> Only set this when needed; static models don't require adjustment.

### Loading External Animations via Assimp

When the model and animations are in separate files (common in FBX workflows):

```csharp
// Load the model first
using (var stream = File.OpenRead("character.fbx"))
{
    model = AssimpLoader.Load(stream, "fbx");
}

// Then load animations (bound to the model's skeleton)
using (var stream = File.OpenRead("walk.fbx"))
{
    var anims = AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx");
    model.AnimationSampler = new AnimationSampler(anims[0]);
}
```

## Animation Blend Space

A 2D blend space smoothly transitions between multiple animations based on two parameters (e.g., movement direction and speed).

```csharp
private AnimationBlendSpace? blendSpace;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // ... load model and animations ...

    // Create blend space
    blendSpace = new AnimationBlendSpace(model.Skeleton);

    // Place animations at positions in 2D space
    blendSpace.AddAnimationSampler(new Vector2(0, 0),   // Origin: idle
        new AnimationSampler(idleAnim));
    blendSpace.AddAnimationSampler(new Vector2(0, 1),   // Up: forward
        new AnimationSampler(walkForwardAnim));
    blendSpace.AddAnimationSampler(new Vector2(0, -1),  // Down: backward
        new AnimationSampler(walkBackAnim));
    blendSpace.AddAnimationSampler(new Vector2(-1, 0),  // Left: strafe left
        new AnimationSampler(walkLeftAnim));
    blendSpace.AddAnimationSampler(new Vector2(1, 0),   // Right: strafe right
        new AnimationSampler(walkRightAnim));

    // Diagonal animations (optional)
    blendSpace.AddAnimationSampler(new Vector2(-1, -1),
        new AnimationSampler(walkBackLeftAnim));
    blendSpace.AddAnimationSampler(new Vector2(1, -1),
        new AnimationSampler(walkBackRightAnim));

    model.AnimationSampler = blendSpace;
    view.AddNode(model);
}

// Update blend parameters each frame
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    blendSpace?.SetAxis(inputX, inputY);  // X, Y must be in [-1, 1]
}
```

## Animation Graph

The animation graph (AnimationGraph) is ideal for managing complex state machines, such as an "idle → walk → run" transition for a character.

```csharp
private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    using var stream = File.OpenRead("character.glb");
    var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(stream);

    // Create state nodes
    var idleNode = new AnimationGraphNode(new AnimationSampler(animations[0]));
    idleNode.BlendTime = 0.5;  // Blend transition time (seconds)

    var walkNode = new AnimationGraphNode(new AnimationSampler(animations[3]));
    walkNode.BlendTime = 0.3;

    var runNode = new AnimationGraphNode(new AnimationSampler(animations[1]));
    runNode.BlendTime = 0.2;

    // Define state transition conditions
    // AddNextNode(condition function, target node)
    // Condition function params: (IAnimationSampler current, double deltaTime)
    idleNode.AddNextNode((sampler, dt) => Speed > 0.01, walkNode);
    walkNode.AddNextNode((sampler, dt) => Speed > 0.8, runNode);
    walkNode.AddNextNode((sampler, dt) => Speed < 0.01, idleNode);
    runNode.AddNextNode((sampler, dt) => Speed < 0.8, walkNode);

    // Create the graph and bind it
    var graph = new AnimationGraph(model.Skeleton, idleNode);
    model.AnimationSampler = graph;

    view.AddNode(model);
}
```

Conditions are checked each frame. When a condition is met, the state machine automatically transitions to the target state, with smoothness controlled by `BlendTime`.

## Manual Bone Manipulation

Beyond relying on animation samplers, you can directly read and write bone transforms for procedural animation, inverse kinematics (IK), or ragdoll systems.

### Traversing Bones

```csharp
var skeleton = model.Skeleton;

// Get bone index by name
int index = skeleton.GetBoneIndex("LeftArm");
// Or get the full mapping
var boneMap = skeleton.GetBoneIndexMap();

// Traverse the bone tree
void TraverseBone(Bone bone, int depth)
{
    Console.WriteLine($"{new string(' ', depth)}{bone.Name} (index={bone.Index})");
    foreach (var child in bone.Children)
        TraverseBone(child, depth + 1);
}
TraverseBone(skeleton.Root, 0);
```

### Reading Bone Matrices

```csharp
// Read the world matrix of a bone (computed for the current frame)
var boneIndex = skeleton.GetBoneIndex("Head");
Matrix4x4 worldMatrix = skeleton.Bones[boneIndex].WorldMatrix;

// Read the local matrix (relative to parent bone)
Matrix4x4 localMatrix = skeleton.Bones[boneIndex].LocalMatrix;

// Read the inverse world matrix (for skinning; typically read-only)
Matrix4x4 invWorldMatrix = skeleton.Bones[boneIndex].InverseWorldMatrix;
```

> **Note**: Directly modifying bone matrices in `SceneUpdated` will have no effect — bone matrices are computed during the animation sampling phase by `IAnimationSampler.Update()`. To achieve procedural bone control, implement a custom `IAnimationSampler` or override matrices after animation sampling.
