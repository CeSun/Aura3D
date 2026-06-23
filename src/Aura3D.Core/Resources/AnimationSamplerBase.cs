using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// 动画采样器的抽象基类，持有 Skeleton、BoneMatrixBuffer、BonesTransform 和 ExternalUpdate 的共享实现。
/// 子类必须各自实现 <see cref="IAnimationSampler.Update"/> 和 <see cref="IAnimationSampler.Reset"/>。
/// </summary>
/// <remarks>
/// Reset() 约定：重置采样器到初始播放状态，具体语义由子类定义（重置时间/轴值/图节点状态）。
/// </remarks>
public abstract class AnimationSamplerBase : IAnimationSampler
{
    /// <summary>
    /// 获取骨骼数据。
    /// </summary>
    public Skeleton Skeleton { get; }

    /// <summary>
    /// 获取骨骼矩阵 UBO。
    /// </summary>
    public BoneMatrixBuffer BoneMatrixBuffer { get; }

    /// <summary>
    /// 获取骨骼变换矩阵只读视图。
    /// </summary>
    public IReadOnlyList<Matrix4x4> BonesTransform => _bonesTransform;

    /// <summary>
    /// 骨骼变换矩阵数组（可被子类直接读写）。
    /// </summary>
    protected readonly Matrix4x4[] _bonesTransform;

    /// <summary>
    /// 获取或设置是否由外部更新动画。
    /// </summary>
    public bool ExternalUpdate { get; set; } = false;

    /// <summary>
    /// 初始化基类共享状态。
    /// </summary>
    /// <param name="skeleton">骨骼数据。</param>
    protected AnimationSamplerBase(Skeleton skeleton)
    {
        Skeleton = skeleton;
        _bonesTransform = new Matrix4x4[skeleton.Bones.Count];
        BoneMatrixBuffer = new BoneMatrixBuffer(Skeleton, this);
    }

    /// <summary>
    /// 通过复制首帧姿态初始化骨骼变换，避免首帧显示绑定姿态（T-Pose）。
    /// </summary>
    /// <param name="source">源骨骼变换数组。</param>
    protected void InitializePoseFrom(IReadOnlyList<Matrix4x4> source)
    {
        for (var i = 0; i < _bonesTransform.Length; i++)
        {
            _bonesTransform[i] = source[i];
        }
    }

    /// <summary>
    /// 通过骨骼的 WorldMatrix 初始化骨骼变换。
    /// </summary>
    protected void InitializePoseFromWorldMatrices()
    {
        for (var i = 0; i < _bonesTransform.Length; i++)
        {
            _bonesTransform[i] = Skeleton.Bones[i].WorldMatrix;
        }
    }

    /// <inheritdoc />
    public abstract void Update(double deltaTime);

    /// <inheritdoc />
    public abstract void Reset();
}