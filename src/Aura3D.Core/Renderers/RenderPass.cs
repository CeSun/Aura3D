using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public partial class RenderPass
{
    public RenderPass(RenderPipeline renderPipeline)
    {
        this.renderPipeline = renderPipeline;
    }

    protected RenderPipeline renderPipeline;

    protected List<Mesh> Meshes => renderPipeline.Meshes;

    protected List<PointLight> PointLights => renderPipeline.PointLights;

    protected List<SpotLight> SpotLights => renderPipeline.SpotLights;

    protected GL gl => renderPipeline.gl!;
    public virtual void Setup()
    {

    }

    public virtual void BeforeRender(Camera camera)
    {

    }
    public virtual void Render(Camera camera)
    {

    }

    public virtual void AfterRender(Camera camera)
    {

    }


    public virtual void BeforeRender()
    {

    }
    public virtual void Render()
    {

    }

    public virtual void AfterRender()
    {

    }

    protected string? outputRenderTargetName;
    public RenderPass SetOutPutRenderTarget(string? renderTargetName)
    {
        this.outputRenderTargetName = renderTargetName;

        return this;
    }

    public void BindOutPutRenderTarget(Camera camera)
    {
        uint fbo = 0;

        if (outputRenderTargetName != null)
        {
            var rt = GetRenderTarget(outputRenderTargetName,
                new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height));
            fbo = rt.FrameBufferId;
        }
        else
        {
            fbo = camera.RenderTarget.FrameBufferId;
        }
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        gl.Viewport(0, 0, camera.RenderTarget.Width, camera.RenderTarget.Height);
    }

    public RenderTarget GetRenderTarget(string name, Size size) => renderPipeline.GetRenderTarget(name, size);

    public unsafe virtual void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("modelMatrix", mesh.WorldTransform);
        gl.BindVertexArray(mesh.Geometry!.Vao);
        gl.DrawElements(GLEnum.Triangles, (uint)mesh.Geometry.IndicesCount, GLEnum.UnsignedInt, (void*)0);
    }

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
                RenderMesh(mesh, view, projection);
            }
        }
    }


    protected bool FilterSkeletonMesh(Mesh mesh)
    {
        if (mesh.IsSkinnedMesh == true && mesh is SkinnedMesh skinnedMesh && skinnedMesh.Skeleton != null)
            return true;
        return false;
    }

    public virtual void SortMeshes(List<Mesh> Meshes, Camera camera)
    {
        renderPipeline.SortMeshes(Meshes, camera);
    }    

    public void RenderCube()
    {
        renderPipeline.RenderCube();
    }

    public void RenderQuad()
    {
        renderPipeline.RenderQuad();
    }

    public void Destroy()
    { 
        foreach(var shader in Shaders)
        {
            gl.DeleteProgram(shader.Value.ProgramId);
        }
        Shaders.Clear();
    }

}