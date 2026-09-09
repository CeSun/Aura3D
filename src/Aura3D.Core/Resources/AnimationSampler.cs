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
    public float TimeScale
    {
        get => _timeScale;
        set
        {
            if (!float.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(TimeScale), "Time scale must be finite and non-negative.");
            _timeScale = value;
        }
    }

    private float _timeScale = 1.0f;

    /// <summary>
    /// Gets or sets the animation.
    /// </summary>
    protected Animation animation { get; set; }

    private double _elapsedSeconds;

    /// <summary>
    /// Gets or sets the loop mode.
    /// </summary>
    public LoopMode LoopMode { get; set; } = LoopMode.Loop;

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        _elapsedSeconds += ValidateDeltaTime(deltaTime) * TimeScale;

        var duration = System.Math.Max(0d, animation.Duration);
        double time;
        if (duration <= 0)
        {
            time = 0;
        }
        else if (LoopMode == LoopMode.Once)
        {
            time = System.Math.Min(_elapsedSeconds, duration);
        }
        else if (LoopMode == LoopMode.PingPong)
        {
            var phase = _elapsedSeconds % (duration * 2);
            time = phase <= duration ? phase : duration * 2 - phase;
        }
        else
        {
            time = _elapsedSeconds % duration;
        }

        processBoneTransform(Skeleton.Root, (float)time);

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
        _elapsedSeconds = 0;
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
