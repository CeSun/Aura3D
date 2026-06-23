using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Resources;

/// <summary>
/// 动画采样器，负责动画的播放和采样
/// </summary>
public class AnimationSampler : AnimationSamplerBase
{
    /// <summary>
    /// 初始化动画采样器
    /// </summary>
    /// <param name="animation">动画对象</param>
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
    /// 获取或设置时间缩放因子。
    /// </summary>
    public float TimeScale { get; set; } = 1.0f;

    protected Animation animation { get; set; }

    private DateTime startTime { get; set; } = default;

    /// <summary>
    /// 获取或设置动画循环模式。
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
/// 动画循环模式
/// </summary>
public enum LoopMode
{
    /// <summary>
    /// 播放一次
    /// </summary>
    Once,
    /// <summary>
    /// 循环播放
    /// </summary>
    Loop,
    /// <summary>
    /// 往复播放
    /// </summary>
    PingPong
}

/// <summary>
/// 复制类型
/// </summary>
public enum CopyType
{
    /// <summary>
    /// 共享资源
    /// </summary>
    SharedResource,
    /// <summary>
    /// 共享资源数据
    /// </summary>
    SharedResourceData,
    /// <summary>
    /// 完全复制
    /// </summary>
    FullCopy
}