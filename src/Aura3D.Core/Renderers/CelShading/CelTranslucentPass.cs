using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Renderers;

public class CelTranslucentPass : LightPass
{
    public CelTranslucentPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);
    }

    public override void Render(Camera camera)
    {

        var rt = GetRenderTarget("BaseRenderTarget",
            new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height));


        gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);

        UseShader("BLENDMODE_TRANSLUCENT");
        SetupUniform(camera);
        using(PushTextureUnit())
        {
            RenderMeshes(mesh => FilterSkeletonMesh(mesh) == false && (mesh.Material != null && mesh.Material.BlendMode == BlendMode.Translucent), camera.View, camera.Projection);
        }

        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT");
        SetupUniform(camera);
        using (PushTextureUnit())
        {
            RenderMeshes(mesh => FilterSkeletonMesh(mesh) && (mesh.Material != null && mesh.Material.BlendMode == BlendMode.Translucent), camera.View, camera.Projection);
        }

    }
}
