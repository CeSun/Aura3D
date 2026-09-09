using Aura3D.Core.Exceptions;
using Aura3D.Core.Resources;
using System.Numerics;
using Xunit;

namespace Aura3D.Tests.Resources;

public class AnimationTests
{
    [Fact]
    public void GetValueByTime_ShouldExposeStableErrorCode_WhenKeyframeListIsEmpty()
    {
        IReadOnlyList<Keyframe<float>> keyframes = [];

        var exception = Assert.Throws<AnimationException>(
            () => keyframes.GetValueByTime(0f, SamplerHelper.Lerp));

        Assert.Equal(AnimationError.EmptyKeyframeList, exception.Code);
        Assert.Equal("The keyframe list is empty.", exception.Message);
    }

    [Fact]
    public void GetValueByTime_ShouldClampOutsideKeyframeRange()
    {
        var keyframes = new[]
        {
            new Keyframe<float> { Time = 1f, Value = 10f },
            new Keyframe<float> { Time = 3f, Value = 30f },
        };

        var beforeStart = keyframes.GetValueByTime(0f, SamplerHelper.Lerp);
        var afterEnd = keyframes.GetValueByTime(4f, SamplerHelper.Lerp);

        Assert.Equal(10f, beforeStart, 5);
        Assert.Equal(30f, afterEnd, 5);
    }

    [Fact]
    public void Sample_ShouldInterpolateTransformFromKeyframes()
    {
        var animation = new Animation();
        var channel = new AnimationChannel();
        channel.PositionKeyframes.AddRange(
        [
            new Keyframe<Vector3> { Time = 0f, Value = Vector3.Zero },
            new Keyframe<Vector3> { Time = 2f, Value = new Vector3(10f, 0f, 0f) }
        ]);
        channel.RotationKeyframes.AddRange(
        [
            new Keyframe<Quaternion> { Time = 0f, Value = Quaternion.Identity },
            new Keyframe<Quaternion> { Time = 2f, Value = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI) }
        ]);
        channel.ScaleKeyframes.AddRange(
        [
            new Keyframe<Vector3> { Time = 0f, Value = Vector3.One },
            new Keyframe<Vector3> { Time = 2f, Value = new Vector3(3f, 3f, 3f) }
        ]);
        animation.Channels["Arm"] = channel;

        var sample = animation.Sample("Arm", 1f);

        Assert.Equal(5f, sample.M41, 5);
        Assert.Equal(0f, sample.M42, 5);
        Assert.Equal(0f, sample.M43, 5);
        Assert.Equal(0f, sample.M11, 5);
        Assert.Equal(2f, sample.M22, 5);
        Assert.Equal(2f, MathF.Abs(sample.M13), 5);
        Assert.Equal(2f, MathF.Abs(sample.M31), 5);
        Assert.Equal(-sample.M13, sample.M31, 5);
    }

    [Fact]
    public void Sample_ShouldFallbackToSkeletonBone_WhenChannelDoesNotExist()
    {
        var expected = Matrix4x4.CreateTranslation(1f, 2f, 3f);
        var skeleton = new Skeleton();
        skeleton.Bones.Add(new Bone
        {
            Name = "Hip",
            LocalMatrix = expected
        });
        var animation = new Animation { Skeleton = skeleton };

        var sample = animation.Sample("Hip", 0.5f);

        Assert.Equal(expected, sample);
    }

    [Fact]
    public void AddNextNode_ShouldExposeStableErrorCode_WhenNodeReferencesItself()
    {
        var node = new AnimationGraphNode(new TestAnimationSampler());

        var exception = Assert.Throws<AnimationException>(
            () => node.AddNextNode((_, _) => true, node));

        Assert.Equal(AnimationError.GraphSelfReference, exception.Code);
    }

    [Fact]
    public void AnimationSampler_ShouldAdvanceOnlyByDeltaTime()
    {
        var sampler = CreateTranslationSampler();

        sampler.Update(0.25);
        Assert.Equal(2.5f, sampler.BonesTransform[0].M41, 5);

        sampler.Update(0.25);
        Assert.Equal(5f, sampler.BonesTransform[0].M41, 5);
    }

    [Fact]
    public void AnimationSampler_Once_ShouldKeepFinalPose()
    {
        var sampler = CreateTranslationSampler();
        sampler.LoopMode = LoopMode.Once;

        sampler.Update(2);
        Assert.Equal(10f, sampler.BonesTransform[0].M41, 5);

        sampler.Update(1);
        Assert.Equal(10f, sampler.BonesTransform[0].M41, 5);
    }

    [Fact]
    public void AnimationSampler_ShouldRejectInvalidTimeScale()
    {
        var sampler = CreateTranslationSampler();

        Assert.Throws<ArgumentOutOfRangeException>(() => sampler.TimeScale = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => sampler.TimeScale = float.NaN);
    }

    [Fact]
    public void AnimationBlendSpace_ShouldPreserveRigidRotationScale()
    {
        var root = new Bone { Name = "Root", Index = 0 };
        var skeleton = new Skeleton { Root = root };
        skeleton.Bones.Add(root);
        var blendSpace = new AnimationBlendSpace(skeleton);
        blendSpace.AddAnimationSampler(
            new Vector2(-1, 0),
            new StaticAnimationSampler(skeleton, Matrix4x4.Identity));
        blendSpace.AddAnimationSampler(
            new Vector2(1, 0),
            new StaticAnimationSampler(skeleton, Matrix4x4.CreateRotationY(MathF.PI)));

        blendSpace.SetAxis(0, 0);
        blendSpace.InitializePose();

        Assert.True(Matrix4x4.Decompose(
            blendSpace.BonesTransform[0], out var scale, out _, out _));
        Assert.Equal(1f, scale.X, 5);
        Assert.Equal(1f, scale.Y, 5);
        Assert.Equal(1f, scale.Z, 5);
    }

    private static AnimationSampler CreateTranslationSampler()
    {
        var root = new Bone { Name = "Root", Index = 0 };
        var skeleton = new Skeleton { Root = root };
        skeleton.Bones.Add(root);

        var animation = new Animation { Duration = 1, Skeleton = skeleton };
        var channel = new AnimationChannel();
        channel.PositionKeyframes.AddRange(
        [
            new Keyframe<Vector3> { Time = 0, Value = Vector3.Zero },
            new Keyframe<Vector3> { Time = 1, Value = new Vector3(10, 0, 0) }
        ]);
        channel.RotationKeyframes.Add(new Keyframe<Quaternion> { Time = 0, Value = Quaternion.Identity });
        channel.ScaleKeyframes.Add(new Keyframe<Vector3> { Time = 0, Value = Vector3.One });
        animation.Channels[root.Name] = channel;

        return new AnimationSampler(animation);
    }

    private sealed class TestAnimationSampler : IAnimationSampler
    {
        public bool ExternalUpdate { get; set; }

        public Skeleton Skeleton { get; } = new();

        public IReadOnlyList<Matrix4x4> BonesTransform { get; } = [];

        public BoneMatrixBuffer BoneMatrixBuffer => Skeleton.BoneMatrixBuffer;

        public void Update(double deltaTime)
        {
        }

        public void Reset()
        {
        }
    }

    private sealed class StaticAnimationSampler : IAnimationSampler
    {
        public StaticAnimationSampler(Skeleton skeleton, Matrix4x4 transform)
        {
            Skeleton = skeleton;
            BonesTransform = [transform];
        }

        public bool ExternalUpdate { get; set; }
        public Skeleton Skeleton { get; }
        public IReadOnlyList<Matrix4x4> BonesTransform { get; }
        public BoneMatrixBuffer BoneMatrixBuffer => Skeleton.BoneMatrixBuffer;
        public void Update(double deltaTime) { }
        public void Reset() { }
    }
}
