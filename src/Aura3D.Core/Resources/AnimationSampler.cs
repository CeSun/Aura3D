using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the animation sampler type.
/// </summary>
public class AnimationSampler : AnimationSamplerBase
{
    /// <summary>
    /// Initializes a new instance of the animation sampler type.
    /// </summary>
    public AnimationSampler(Animation animation)
        : base(animation.Skeleton!)
    {
        InitializePoseFromWorldMatrices();
        this.animation = animation;

        // Compute the first frame immediately to avoid showing T-pose
        // before the first Update() call.
        processBoneTransform(Skeleton.Root, 0);
    }

    /// <summary>
    /// Gets or sets the time scale.
    /// </summary>
    public float TimeScale { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the animation.
    /// </summary>
    protected Animation animation { get; set; }

    private DateTime startTime { get; set; } = default;

    /// <summary>
    /// Gets or sets the loop mode.
    /// </summary>
    public LoopMode LoopMode { get; set; } = LoopMode.Loop;

    private bool pingPongForward { get; set; } = true;

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (startTime == default)
        {
            startTime = DateTime.Now;
        }

        var now = DateTime.Now;
        var elapsed = now - startTime;
        var duration = TimeSpan.FromSeconds(animation.Duration / TimeScale);

        while (elapsed > duration && duration > TimeSpan.Zero)
        {
            if (LoopMode == LoopMode.Loop)
            {
                startTime += duration;
                elapsed = now - startTime;
            }
            else if (LoopMode == LoopMode.PingPong)
            {
                startTime += duration;
                pingPongForward = !pingPongForward;
                elapsed = now - startTime;
            }
            else if (LoopMode == LoopMode.Once)
            {
                return;
            }
            else
            {
                break;
            }
        }

        var time = (float)elapsed.TotalSeconds * TimeScale;

        if (pingPongForward == false)
        {
            time = animation.Duration - time;
        }

        processBoneTransform(Skeleton.Root, time);

        BoneMatrixBuffer.MarkModified();
    }

    private void processBoneTransform(Bone bone, float time)
    {
        var channelMatrix = animation.Sample(bone.Name, time);
        if (bone.Parent != null)
        {
            _bonesTransform[bone.Index] = channelMatrix * BonesTransform[bone.Parent.Index];
        }
        else
        {
            _bonesTransform[bone.Index] = channelMatrix;
        }
        foreach (var child in bone.Children)
        {
            processBoneTransform(child, time);
        }
    }

    /// <inheritdoc />
    public override void Reset()
    {
        startTime = default;
    }

}

/// <summary>
/// Specifies values for loop mode.
/// </summary>
public enum LoopMode
{
    /// <summary>
    /// Specifies once.
    /// </summary>
    Once,
    /// <summary>
    /// Specifies loop.
    /// </summary>
    Loop,
    /// <summary>
    /// Specifies ping pong.
    /// </summary>
    PingPong
}

/// <summary>
/// Specifies values for copy type.
/// </summary>
public enum CopyType
{
    /// <summary>
    /// Specifies shared resource.
    /// </summary>
    SharedResource,
    /// <summary>
    /// Specifies shared resource data.
    /// </summary>
    SharedResourceData,
    /// <summary>
    /// Specifies full copy.
    /// </summary>
    FullCopy
}