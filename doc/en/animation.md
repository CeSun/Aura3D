---
section: basics
order: 7
---

# Animation System

**Make a rigged character move, and control how it moves**:

- Load a model with skeletal animation and play it, switch clips, change speed, loop or play once;
- Take frame-by-frame control of animation time when needed (stop-motion, slow motion, stepping);
- Attach animations from separate files via Assimp (common in FBX workflows);
- Smoothly blend between idle / forward / backward / strafe animations with a 2D blend space;
- Manage conditional transitions like "idle → walk → run" with an animation state graph;
- Read and write bone matrices directly, bypassing samplers, for procedural animation, IK, or attachments.

> [!NOTE]
> The bone matrices produced by animation samplers are consumed by the skinned mesh. See [./models.md](./models.md) for model loading and part organization, and [./scene-and-nodes.md](./scene-and-nodes.md) for scene node basics.

## Loading and Playing Skeletal Animation

The shortest working path: load the model and its animation array from a glb, create an `AnimationSampler` for the first clip, and assign it to `model.AnimationSampler` — playback starts automatically.

```csharp
private Model? model;
private AnimationSampler? animationSampler;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // Load model and animations
    using var stream = File.OpenRead("character.glb");
    var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(stream);

    // Create animation sampler and bind it to the model
    animationSampler = new AnimationSampler(animations[0]);
    animationSampler.TimeScale = 1.0f;  // Playback speed
    model.AnimationSampler = animationSampler;

    model.Position = new Vector3(0, 0, 3);
    view.AddNode(model);

    // Add a light
    var dl = new DirectionalLight();
    dl.RotationDegrees = new Vector3(-30, 0, 0);
    dl.LightColor = Color.White;
    view.AddNode(dl);
}
```

As long as an `AnimationSampler` (or a blend space / graph, covered below) is assigned to `model.AnimationSampler`, the scene advances the sampler and drives skinning every frame — no per-frame code required from you.

### Switching Animations

Switching clips means switching samplers: create a new `AnimationSampler` for the target `Animation` and rebind it.

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

### Loop Modes and Reset

`AnimationSampler` provides three loop modes:

```csharp
var sampler = new AnimationSampler(animation);

// Loop playback (default)
sampler.LoopMode = LoopMode.Loop;

// Play once then hold at the end
sampler.LoopMode = LoopMode.Once;

// Ping-pong back and forth
sampler.LoopMode = LoopMode.PingPong;

// Reset animation back to the beginning
sampler.Reset();
```

Playback speed is controlled with `TimeScale` (`1.0` is original speed, `0.5` is half-speed).

### Manual Animation Time Control

By default `AnimationSampler` advances automatically using system time. Set `ExternalUpdate = true` to take full control via `Update` — ideal for stop-motion, frame inspection, or server synchronization:

```csharp
sampler.ExternalUpdate = true;

// Advance manually in SceneUpdated
private void OnSceneUpdated(object sender, UpdateRoutedEventArgs e)
{
    sampler.Update(e.DeltaTime);
}
```

> [!NOTE]
> This applies to blend spaces (`AnimationBlendSpace`) and graphs (`AnimationGraph`) as well: their `Update` automatically updates all internal samplers. If `ExternalUpdate = true`, only call `Update` on the **top-level** sampler.

### Loading External Animations via Assimp

When the model and animations live in separate files (common in FBX workflows), load the model first, then bind the animation file to the model's skeleton:

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

## 2D Blend Space: Mixing Animations by Input Direction

A blend space places multiple animations at positions on a 2D plane and distance-weights between them based on an `(x, y)` parameter — the classic locomotion setup: idle at the origin, movement directions around it.

```csharp
private AnimationBlendSpace? blendSpace;

private void OnSceneInitialized(object sender, InitializedRoutedEventArgs e)
{
    var view = (Aura3DView)sender;

    // ... load model and animations ...

    // Create blend space (based on the model's skeleton)
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

Binding is identical to a single clip — the model can't tell whether a slot holds a sampler or a blend space. The weighting can be tuned with `IdwPower` (inverse-distance-weighting exponent, default 2; larger values favor the nearest animation more strongly).

## Animation Graph: Conditional Transitions with Cross-Fading

The animation graph (`AnimationGraph`) is ideal for state machines with transition conditions, such as "idle → walk → run". Each state is an `AnimationGraphNode`; transitions are declared as condition functions on edges, and cross-fading uses `BlendTime`.

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

    // Create the graph and bind it; the second argument is the entry node
    var graph = new AnimationGraph(model.Skeleton, idleNode);
    model.AnimationSampler = graph;

    view.AddNode(model);
}
```

Each frame, the outgoing conditions of the current node are checked. When a condition is met, the state machine transitions to the target state, with smoothness controlled by `BlendTime`.

## Manual Bone Manipulation

Beyond relying on animation samplers, you can directly read bone transforms for procedural animation, inverse kinematics (IK), or ragdoll systems. For debugging, `view.Scene.RenderPipeline.Settings.Debug.ShowBone = true;` draws the bones.

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

### Attaching Objects to Bones

Want a torch that follows the hand? No manual matrix sync — declare the target bone with a `BoneAttachment` node:

```csharp
var attachment = new BoneAttachment
{
    Mesh = targetSkinnedMesh,          // The skinned mesh the animation belongs to
    BoneName = "LeftHand",             // Target bone name
    LocalOffset = Matrix4x4.CreateTranslation(new Vector3(0, 0.35f, 0)),
};
view.AddNode(attachment);
attachment.AddChild(torchMesh, AttachToParentRule.KeepLocal);
```

## Common Pitfalls

> [!WARNING]
> **Modifying bone matrices directly in `SceneUpdated` has no effect.** Bone matrices are computed during the animation sampling phase by `IAnimationSampler.Update()`; writes made from your event callback get overwritten by the next sampling pass. To achieve procedural bone control, implement a custom `IAnimationSampler` or override matrices after animation sampling.

> [!WARNING]
> **Skeletal mesh bounding boxes are computed statically from the T-Pose.** For performance reasons, skeletal meshes do not recompute bounding boxes per-frame from bone positions; a T-Pose bounding box is generated from the static vertex data. If an animation moves the model significantly beyond this box (e.g., walking, jumping), frustum culling may incorrectly cull meshes still in view. Fixes:

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

Only set this when needed; static models don't require adjustment. See more culling issues in [./troubleshooting.md](./troubleshooting.md).

## Runnable Examples

- Skeletal playback / loop modes / manual time / bone attachment: <https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/SkinnedAnimation/SkinnedAnimationDemo.axaml.cs>
- Graph + 2D blend space side by side: <https://github.com/CeSun/Aura3D/blob/main/gallery/Aura3D.Gallery/Demos/AnimationMix/AnimationMixDemo.axaml.cs>

Core implementation sources: [AnimationSampler](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/AnimationSampler.cs), [AnimationBlendSpace](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/AnimationBlendSpace.cs), [AnimationGraph](https://github.com/CeSun/Aura3D/blob/main/src/Aura3D.Core/Resources/AnimationGraph.cs).
