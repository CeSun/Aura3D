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
}
