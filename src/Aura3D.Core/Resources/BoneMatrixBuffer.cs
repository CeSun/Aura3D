using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// 骨骼矩阵的 CPU 侧资源，由 <see cref="IAnimationSampler"/> 或 <see cref="Skeleton"/> 持有。
/// GPU 侧的 UBO 由独立的 BoneMatrixBufferGpuState 管理。
/// </summary>
public class BoneMatrixBuffer : IVersionedResource
{
    /// <summary>
    /// 与 shader 中 <c>#define MAX_BONES 256</c> 一致，也是 GLES 3.0 UBO 最小保证值（16KB ÷ 64B）。
    /// </summary>
    public const int MaxBones = 256;

    /// <summary>
    /// UBO 的绑定索引，需与 shader 中 <c>layout(std140) uniform BoneBlock</c> 经
    /// <c>glUniformBlockBinding</c> 绑定到的索引一致。
    /// </summary>
    public const uint BindingIndex = 0;

    public Skeleton Skeleton { get; }
    public IAnimationSampler? AnimationSampler { get; }
    public ulong Version { get; protected set; } = 1;

    public BoneMatrixBuffer(Skeleton skeleton, IAnimationSampler? animationSampler = null)
    {
        Skeleton = skeleton;
        AnimationSampler = animationSampler;
    }

    public void MarkModified()
    {
        Version++;
    }

}
