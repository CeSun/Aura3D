using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 渲染通道的基类，负责在渲染管线中执行特定阶段的绘制操作。
/// </summary>
public partial class RenderPass
{
    /// <summary>
    /// 初始化 <see cref="RenderPass"/> 类的新实例。
    /// </summary>
    /// <param name="renderPipeline">所属的渲染管线。</param>
    public RenderPass(RenderPipeline renderPipeline)
    {
        this.renderPipeline = renderPipeline;
        ShaderName = GetType().Name;
    }

    protected RenderPipeline renderPipeline;

    protected Scene Scene => renderPipeline.Scene;

    protected List<Mesh> Meshes => renderPipeline.Meshes;

    protected List<PointLight> PointLights => renderPipeline.PointLights;

    protected List<SpotLight> SpotLights => renderPipeline.SpotLights;
    
    protected IReadOnlyList<Mesh> VisibleMeshesInCamera => renderPipeline.VisibleMeshesInCamera;

    protected GL gl => renderPipeline.gl!;

    /// <summary>
    /// 设置当前渲染通道所需的着色器和其他资源。
    /// </summary>
    public virtual void Setup()
    {

    }

    /// <summary>
    /// 获取是否启用视锥体剔除。
    /// </summary>
    public bool EnableFrustumCulling => renderPipeline.EnableFrustumCulling;

    /// <summary>
    /// 在渲染指定相机之前执行的状态设置和准备工作。
    /// </summary>
    /// <param name="camera">当前要渲染的相机。</param>
    public virtual void BeforeRender(Camera camera)
    {

    }

    /// <summary>
    /// 执行指定相机的渲染逻辑。
    /// </summary>
    /// <param name="camera">当前要渲染的相机。</param>
    public virtual void Render(Camera camera)
    {

    }

    /// <summary>
    /// 在渲染指定相机之后执行的清理和恢复工作。
    /// </summary>
    /// <param name="camera">当前已渲染完成的相机。</param>
    public virtual void AfterRender(Camera camera)
    {

    }

    /// <summary>
    /// 在每帧全局渲染之前执行的准备工作。
    /// </summary>
    public virtual void BeforeRender()
    {

    }

    /// <summary>
    /// 执行每帧全局渲染逻辑。
    /// </summary>
    public virtual void Render()
    {

    }

    /// <summary>
    /// 在每帧全局渲染之后执行的清理工作。
    /// </summary>
    public virtual void AfterRender()
    {

    }

    protected RenderOutputRef? outputRenderTarget;

    protected RenderOutputRef CameraOutput => renderPipeline.CameraOutput;

    /// <summary>
    /// 设置当前渲染通道的输出渲染目标。
    /// </summary>
    /// <param name="output">输出引用，若为 <c>null</c> 则输出到相机默认目标。</param>
    /// <returns>当前的 <see cref="RenderPass"/> 实例。</returns>
    public RenderPass SetOutput(RenderOutputRef? output)
    {
        outputRenderTarget = output;
        return this;
    }

    /// <summary>
    /// 绑定当前渲染通道的输出渲染目标到指定相机的帧缓冲。
    /// </summary>
    /// <param name="camera">当前渲染的相机。</param>
    public void BindOutput(Camera camera)
    {
        var resolvedOutput = (outputRenderTarget ?? CameraOutput).Resolve(renderPipeline, camera);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, resolvedOutput.FramebufferId);
        gl.Viewport(0, 0, resolvedOutput.Width, resolvedOutput.Height);
    }

    protected uint GetOutputFramebufferId(Camera camera)
    {
        return (outputRenderTarget ?? CameraOutput).Resolve(renderPipeline, camera).FramebufferId;
    }

    public void BindOutputRenderTarget(Camera camera)
    {
        BindOutput(camera);
    }

    protected RenderTarget GetRenderTarget(RenderTargetHandle renderTargetHandle, Size size)
        => renderPipeline.GetRenderTarget(renderTargetHandle, size);

    protected RenderTarget GetRenderTarget(RenderTargetHandle renderTargetHandle, Camera camera)
        => renderPipeline.GetRenderTarget(renderTargetHandle, new Size((int)camera.Width, (int)camera.Height));

    protected RenderTexture GetTexture(RenderTargetTextureHandle renderTargetTextureHandle, Camera camera)
        => renderTargetTextureHandle.ResolveTexture(renderPipeline, camera);

    protected RenderTarget GetOutputRenderTargetOrThrow(Camera camera)
    {
        if (outputRenderTarget == null)
            throw Aura3D.Core.Exceptions.RendererErrors.RenderPassOutputNotSet();

        return outputRenderTarget.ResolveRenderTarget(renderPipeline, camera);
    }

    /// <summary>
    /// 渲染单个网格，并设置模型矩阵与材质相关的着色器参数。
    /// </summary>
    /// <param name="mesh">要渲染的网格。</param>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    public unsafe virtual void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("modelMatrix", mesh.WorldTransform);
        var geometryGpuState = renderPipeline.EnsureSynced(mesh.Geometry!);
        gl.BindVertexArray(geometryGpuState.Vao);
        BindMaterialParameters(mesh.Material);

        var primitive = GetGLPrimitiveType(mesh.Geometry.PrimitiveType);

        if (primitive == GLEnum.Points)
        {
            gl.Enable(EnableCap.ProgramPointSize);
        }
        if (mesh.Geometry.IndicesCount > 0)
            gl.DrawElements(primitive, (uint)mesh.Geometry.IndicesCount, GLEnum.UnsignedInt, (void*)0);
        else
            gl.DrawArrays(primitive, 0, (uint)mesh.Geometry.VertexCount);
    }


    public unsafe virtual void RenderInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        var imGpuState = renderPipeline.EnsureSynced(instancedMesh);
        gl.BindVertexArray(imGpuState.Vao);
        BindMaterialParameters(instancedMesh.Material);

        var primitive = GetGLPrimitiveType(instancedMesh.PrimitiveType);


        if (primitive == GLEnum.Points)
        {
            gl.Enable(EnableCap.ProgramPointSize);
        }
        if (instancedMesh.IndicesCount > 0)
            gl.DrawElementsInstanced(primitive, (uint)instancedMesh.IndicesCount, GLEnum.UnsignedInt, (void*)0, (uint)instancedMesh.InstanceCount);
        else
            gl.DrawArraysInstanced(primitive, 0, (uint)instancedMesh.VertexCount, (uint)instancedMesh.InstanceCount);
    }

    protected void SyncAndBindBoneMatrixBuffer(Mesh mesh)
    {
        if (mesh.IsSkinnedMesh == false)
            return;

        var boneBuffer = mesh.AnimationSampler?.BoneMatrixBuffer ?? mesh.Skeleton!.BoneMatrixBuffer;
        renderPipeline.SyncAndBindBoneMatrixBuffer(boneBuffer);
    }

    void BindMaterialParameters(Material? material)
    {
        if (material == null)
            return;

        foreach (var kv in material.EnumerateParameters())
        {
            if (kv.Value is int intValue)
            {
                UniformInt(kv.Key, intValue);
            }
            else if (kv.Value is float floatValue)
            {
                UniformFloat(kv.Key, floatValue);
            }
            else if (kv.Value is Vector2 vector2Value)
            {
                UniformVector2(kv.Key, vector2Value);
            }
            else if (kv.Value is Vector3 vector3Value)
            {
                UniformVector3(kv.Key, vector3Value);
            }
            else if (kv.Value is Vector4 vector4Value)
            {
                UniformVector4(kv.Key, vector4Value);
            }
            else if (kv.Value is Matrix4x4 matrix4Value)
            {
                UniformMatrix4(kv.Key, matrix4Value);
            }
        }
    }

    /// <summary>
    /// 根据筛选条件渲染场景中所有符合条件的网格。
    /// </summary>
    /// <param name="filter">网格筛选条件。</param>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    /// <summary>
    /// 将 <see cref="PrimitiveType"/> 转换为 OpenGL 图元枚举。
    /// </summary>
    private static GLEnum GetGLPrimitiveType(Aura3D.Core.Resources.PrimitiveType type) => type switch
    {
        Aura3D.Core.Resources.PrimitiveType.Points => GLEnum.Points,
        Aura3D.Core.Resources.PrimitiveType.Lines => GLEnum.Lines,
        Aura3D.Core.Resources.PrimitiveType.LineStrip => GLEnum.LineStrip,
        Aura3D.Core.Resources.PrimitiveType.LineLoop => GLEnum.LineLoop,
        Aura3D.Core.Resources.PrimitiveType.TriangleStrip => GLEnum.TriangleStrip,
        Aura3D.Core.Resources.PrimitiveType.TriangleFan => GLEnum.TriangleFan,
        _ => GLEnum.Triangles,
    };

    public void RenderMeshes(Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        foreach (var mesh in renderPipeline.Meshes)
        {
            if (mesh.Enable == false)
                continue;
            if (mesh.Geometry == null)
                continue;
            if (filter(mesh))
            {
                UseShader_Internal(mesh);
                RenderMesh(mesh, view, projection);
            }
        }
    }
    
    /// <summary>
    /// 渲染当前相机视锥体中可见且符合条件的网格。
    /// </summary>
    /// <param name="filter">网格筛选条件。</param>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    public void RenderVisibleMeshesInCamera(Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        RenderMeshesFromList(VisibleMeshesInCamera, filter, view, projection);
    }

    /// <summary>
    /// 从指定的网格列表中渲染符合条件的网格。
    /// </summary>
    /// <param name="meshes">要遍历的网格列表。</param>
    /// <param name="filter">网格筛选条件。</param>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    public void RenderMeshesFromList(IReadOnlyList<Mesh> meshes, Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        foreach (var mesh in meshes)
        {
            if (mesh.Enable == false)
                continue;
            if (mesh.Geometry == null)
                continue;
            if (filter(mesh))
            {
                UseShader_Internal(mesh);
                RenderMesh(mesh, view, projection);
            }
        }
    }
    
    List<Mesh> meshes = new List<Mesh>();
    Plane[] planes = new Plane[6];

    /// <summary>
    /// 渲染所有静态网格（非蒙皮），支持视锥体剔除。
    /// </summary>
    /// <param name="filter">网格筛选条件。</param>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    public void RenderStaticMeshes(Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        var list = renderPipeline.Meshes;

        if (EnableFrustumCulling == true)
        {
            meshes.Clear();
            renderPipeline.UpdateVisibleMeshesInCamera(view, projection, meshes);
            list = meshes;
        }
        foreach (var mesh in list)
        {
            if (mesh.Enable == false)
                continue;
            if (mesh.Geometry == null)
                continue;
            if (mesh.IsSkinnedMesh == true)
                continue;
            if (filter(mesh))
            {
                UseShader_Internal(mesh);
                RenderMesh(mesh, view, projection);
            }
        }
    }

    /// <summary>
    /// 渲染所有蒙皮网格，支持视锥体剔除。
    /// </summary>
    /// <param name="filter">网格筛选条件。</param>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    public void RenderSkinnedMeshes(Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        var list = renderPipeline.Meshes;

        if (EnableFrustumCulling == true)
        {
            meshes.Clear();
            renderPipeline.UpdateVisibleMeshesInCamera(view, projection, meshes);
            list = meshes;
        }
        foreach (var mesh in list)
        {
            if (mesh.Enable == false)
                continue;
            if (mesh.Geometry == null)
                continue;
            if (mesh.IsSkinnedMesh == false)
                continue;
            if (filter(mesh))
            {
                UseShader_Internal(mesh);
                RenderMesh(mesh, view, projection);
            }
        }
    }

    public void RenderInstancedMeshes(Func<InstancedMesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        foreach (var instancedMesh in renderPipeline.InstancedMeshes)
        {
            if (instancedMesh.Enable == false)
                continue;
            if (!filter(instancedMesh))
                continue;
            UseShader_Internal(instancedMesh.Material);
            RenderInstancedMesh(instancedMesh, view, projection);
        }
    }

    /// <summary>
    /// 渲染当前相机视锥体中可见且符合条件的实例化网格（支持视锥体剔除）。
    /// </summary>
    /// <param name="filter">实例化网格筛选条件。</param>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    public void RenderVisibleInstancedMeshesInCamera(Func<InstancedMesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        var list = EnableFrustumCulling
            ? renderPipeline.VisibleInstancedMeshesInCamera
            : renderPipeline.InstancedMeshes;

        foreach (var instancedMesh in list)
        {
            if (instancedMesh.Enable == false)
                continue;
            if (!filter(instancedMesh))
                continue;
            UseShader_Internal(instancedMesh.Material);
            RenderInstancedMesh(instancedMesh, view, projection);
        }
    }

    protected bool IsMaterialBlendMode(Mesh mesh, BlendMode mode)
    {
        return IsMaterialBlendMode(mesh.Material, mode);
    }

    protected bool IsMaterialBlendMode(Material? material, BlendMode mode)
    {
        if (material == null)
            if (mode == BlendMode.Opaque)
                return true;
            else
                return false;
        else
        {
            if (material.BlendMode == mode)
                return true;
            return false;
        }
    }

    /// <summary>
    /// 对网格列表按与相机的距离进行排序。
    /// </summary>
    /// <param name="Meshes">要排序的网格列表。</param>
    /// <param name="camera">用于计算距离的相机。</param>
    public virtual void SortMeshes(IReadOnlyList<Mesh> Meshes, Camera camera)
    {
        renderPipeline.SortMeshes(Meshes, camera);
    }    

    /// <summary>
    /// 渲染一个单位立方体。
    /// </summary>
    public void RenderCube()
    {
        renderPipeline.RenderCube();
    }

    /// <summary>
    /// 渲染一个全屏四边形。
    /// </summary>
    public void RenderQuad()
    {
        renderPipeline.RenderQuad();
    }

    /// <summary>
    /// 销毁当前渲染通道分配的所有着色器程序。
    /// </summary>
    public virtual void Destroy()
    {
        foreach(var shader in Shaders)
        {
            gl.DeleteProgram(shader.Value.ProgramId);
        }
        Shaders.Clear();

        if (_immVbo != 0)
        {
            gl.DeleteBuffer(_immVbo);
            _immVbo = 0;
        }
        if (_immVao != 0)
        {
            gl.DeleteVertexArray(_immVao);
            _immVao = 0;
        }
    }
}

/// <summary>
/// 泛型渲染通道基类，提供对特定类型渲染管线的强类型访问。
/// </summary>
/// <typeparam name="T">渲染管线的类型。</typeparam>
public class RenderPass<T> : RenderPass where T : RenderPipeline
{
    /// <summary>
    /// 初始化 <see cref="RenderPass{T}"/> 类的新实例。
    /// </summary>
    /// <param name="renderPipeline">所属的渲染管线。</param>
    public RenderPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
    }

    /// <summary>
    /// 获取当前渲染通道所属的强类型渲染管线实例。
    /// </summary>
    public T RenderPipeline => (T)renderPipeline;
}
