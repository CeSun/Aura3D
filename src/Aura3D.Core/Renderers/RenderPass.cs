using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the render pass type.
/// </summary>
public partial class RenderPass
{
    /// <summary>
    /// Initializes a new instance of the render pass type.
    /// </summary>
    public RenderPass(RenderPipeline renderPipeline)
    {
        this.renderPipeline = renderPipeline;
        ShaderName = GetType().Name;
    }

    /// <summary>
    /// Gets the render pipeline.
    /// </summary>
    protected RenderPipeline renderPipeline;

    /// <summary>
    /// Gets the scene.
    /// </summary>
    protected Scene Scene => renderPipeline.Scene;

    /// <summary>
    /// Gets the meshes.
    /// </summary>
    protected List<Mesh> Meshes => renderPipeline.Meshes;

    /// <summary>
    /// Gets the point lights.
    /// </summary>
    protected List<PointLight> PointLights => renderPipeline.PointLights;

    /// <summary>
    /// Gets the spot lights.
    /// </summary>
    protected List<SpotLight> SpotLights => renderPipeline.SpotLights;
    
    /// <summary>
    /// Gets the visible meshes in camera.
    /// </summary>
    protected IReadOnlyList<Mesh> VisibleMeshesInCamera => renderPipeline.VisibleMeshesInCamera;

    /// <summary>
    /// Gets the gl.
    /// </summary>
    protected GL gl => renderPipeline.gl!;

    /// <summary>
    /// Sets the up.
    /// </summary>
    public virtual void Setup()
    {

    }

    /// <summary>
    /// Gets the enable frustum culling.
    /// </summary>
    public bool EnableFrustumCulling => renderPipeline.EnableFrustumCulling;

    /// <summary>
    /// Performs the before render operation.
    /// </summary>
    public virtual void BeforeRender(Camera camera)
    {

    }

    /// <summary>
    /// Renders the associated data.
    /// </summary>
    public virtual void Render(Camera camera)
    {

    }

    /// <summary>
    /// Performs the after render operation.
    /// </summary>
    public virtual void AfterRender(Camera camera)
    {

    }

    /// <summary>
    /// Performs the before render operation.
    /// </summary>
    public virtual void BeforeRender()
    {

    }

    /// <summary>
    /// Renders the associated data.
    /// </summary>
    public virtual void Render()
    {

    }

    /// <summary>
    /// Performs the after render operation.
    /// </summary>
    public virtual void AfterRender()
    {

    }

    /// <summary>
    /// Gets the output render target.
    /// </summary>
    protected RenderOutputRef? outputRenderTarget;

    /// <summary>
    /// Gets the camera output.
    /// </summary>
    protected RenderOutputRef CameraOutput => renderPipeline.CameraOutput;

    /// <summary>
    /// Sets the output.
    /// </summary>
    public RenderPass SetOutput(RenderOutputRef? output)
    {
        outputRenderTarget = output;
        return this;
    }

    /// <summary>
    /// Binds the output.
    /// </summary>
    public void BindOutput(Camera camera)
    {
        var resolvedOutput = (outputRenderTarget ?? CameraOutput).Resolve(renderPipeline, camera);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, resolvedOutput.FramebufferId);
        gl.Viewport(0, 0, resolvedOutput.Width, resolvedOutput.Height);
    }

    /// <summary>
    /// Gets the output framebuffer id.
    /// </summary>
    protected uint GetOutputFramebufferId(Camera camera)
    {
        return (outputRenderTarget ?? CameraOutput).Resolve(renderPipeline, camera).FramebufferId;
    }

    /// <summary>
    /// Binds the output render target.
    /// </summary>
    public void BindOutputRenderTarget(Camera camera)
    {
        BindOutput(camera);
    }

    /// <summary>
    /// Gets the render target.
    /// </summary>
    protected RenderTarget GetRenderTarget(RenderTargetHandle renderTargetHandle, Size size)
        => renderPipeline.GetRenderTarget(renderTargetHandle, size);

    /// <summary>
    /// Gets the render target.
    /// </summary>
    protected RenderTarget GetRenderTarget(RenderTargetHandle renderTargetHandle, Camera camera)
        => renderPipeline.GetRenderTarget(renderTargetHandle, new Size((int)camera.Width, (int)camera.Height));

    /// <summary>
    /// Gets the texture.
    /// </summary>
    protected RenderTexture GetTexture(RenderTargetTextureHandle renderTargetTextureHandle, Camera camera)
        => renderTargetTextureHandle.ResolveTexture(renderPipeline, camera);

    /// <summary>
    /// Gets the output render target or throw.
    /// </summary>
    protected RenderTarget GetOutputRenderTargetOrThrow(Camera camera)
    {
        if (outputRenderTarget == null)
            throw Aura3D.Core.Exceptions.RendererErrors.RenderPassOutputNotSet();

        return outputRenderTarget.ResolveRenderTarget(renderPipeline, camera);
    }

    /// <summary>
    /// Renders the mesh.
    /// </summary>
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


    /// <summary>
    /// Renders the instanced mesh.
    /// </summary>
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

    /// <summary>
    /// Performs the sync and bind bone matrix buffer operation.
    /// </summary>
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
    /// Gets the gl primitive type.
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

    /// <summary>
    /// Renders the meshes.
    /// </summary>
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
    /// Renders the visible meshes in camera.
    /// </summary>
    public void RenderVisibleMeshesInCamera(Func<Mesh, bool> filter, Matrix4x4 view, Matrix4x4 projection)
    {
        RenderMeshesFromList(VisibleMeshesInCamera, filter, view, projection);
    }

    /// <summary>
    /// Renders the meshes from list.
    /// </summary>
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
    /// Renders the static meshes.
    /// </summary>
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
    /// Renders the skinned meshes.
    /// </summary>
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

    /// <summary>
    /// Renders the instanced meshes.
    /// </summary>
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
    /// Renders the visible instanced meshes in camera.
    /// </summary>
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

    /// <summary>
    /// Determines whether material blend mode.
    /// </summary>
    protected bool IsMaterialBlendMode(Mesh mesh, BlendMode mode)
    {
        return IsMaterialBlendMode(mesh.Material, mode);
    }

    /// <summary>
    /// Determines whether material blend mode.
    /// </summary>
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
    /// Performs the sort meshes operation.
    /// </summary>
    public virtual void SortMeshes(IReadOnlyList<Mesh> Meshes, Camera camera)
    {
        renderPipeline.SortMeshes(Meshes, camera);
    }    

    /// <summary>
    /// Renders the cube.
    /// </summary>
    public void RenderCube()
    {
        renderPipeline.RenderCube();
    }

    /// <summary>
    /// Renders the quad.
    /// </summary>
    public void RenderQuad()
    {
        renderPipeline.RenderQuad();
    }

    /// <summary>
    /// Destroys the associated data.
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
/// Represents the render pass type.
/// </summary>
public class RenderPass<T> : RenderPass where T : RenderPipeline
{
    /// <summary>
    /// Initializes a new instance of the render pass type.
    /// </summary>
    public RenderPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
    }

    /// <summary>
    /// Gets the render pipeline.
    /// </summary>
    public T RenderPipeline => (T)renderPipeline;
}
