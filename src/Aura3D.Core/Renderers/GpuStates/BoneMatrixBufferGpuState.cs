using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

internal sealed class BoneMatrixBufferGpuState : IResourceGpuState<BoneMatrixBuffer>
{
    private readonly WeakReference<BoneMatrixBuffer> boneMatrixBuffer;
    private Matrix4x4[] matrices = [];

    public BoneMatrixBufferGpuState(BoneMatrixBuffer boneMatrixBuffer)
    {
        this.boneMatrixBuffer = new WeakReference<BoneMatrixBuffer>(boneMatrixBuffer);
    }

    public BoneMatrixBuffer Resource
    {
        get
        {
            if (boneMatrixBuffer.TryGetTarget(out var value))
                return value;

            throw Aura3D.Core.Exceptions.RendererErrors.CpuResourceCollected(nameof(BoneMatrixBuffer));
        }
    }

    public bool IsAlive => boneMatrixBuffer.TryGetTarget(out _);
    public ulong Version => Resource.Version;
    public ulong SyncedVersion { get; protected set; }

    public uint BufferId { get; private set; }

    public unsafe void Upload(GL gl)
    {
        var resource = Resource;
        int boneCount = resource.Skeleton.Bones.Count;

        if (boneCount == 0)
            return;

        if (BufferId == 0)
        {
            BufferId = gl.GenBuffer();
            gl.BindBuffer(GLEnum.UniformBuffer, BufferId);
            gl.BufferData(GLEnum.UniformBuffer, (nuint)(BoneMatrixBuffer.MaxBones * 64), (void*)0, GLEnum.DynamicDraw);
        }

        if (matrices.Length < boneCount)
            matrices = new Matrix4x4[boneCount];

        var sampler = resource.AnimationSampler;
        for (int i = 0; i < boneCount; i++)
        {
            if (sampler != null && i < sampler.BonesTransform.Count)
            {
                matrices[i] = resource.Skeleton.Bones[i].InverseWorldMatrix * sampler.BonesTransform[i];
            }
            else
            {
                matrices[i] = resource.Skeleton.Bones[i].InverseWorldMatrix * resource.Skeleton.Bones[i].WorldMatrix;
            }
        }

        int uploadCount = boneCount < BoneMatrixBuffer.MaxBones ? boneCount : BoneMatrixBuffer.MaxBones;
        fixed (Matrix4x4* ptr = matrices)
        {
            gl.BindBuffer(GLEnum.UniformBuffer, BufferId);
            gl.BufferSubData(GLEnum.UniformBuffer, 0, (nuint)(uploadCount * 64), ptr);
        }

        SyncedVersion = resource.Version;
    }

    public void Bind(GL gl)
    {
        if (BufferId != 0)
        {
            gl.BindBufferBase(GLEnum.UniformBuffer, BoneMatrixBuffer.BindingIndex, BufferId);
        }
    }

    public void Destroy(GL gl)
    {
        if (BufferId != 0)
        {
            gl.DeleteBuffer(BufferId);
            BufferId = 0;
        }
    }
}
