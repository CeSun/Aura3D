using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public class BackgroundPass: RenderPass
{
    public BackgroundPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.BackgroundVert;
        FragmentShader = ShaderResource.BackgroundFrag;
    }


    public override void BeforeRender(Camera camera)
    {

        BindOutPutRenderTarget(camera);

        gl.ClearColor(camera.ClearColor);

        gl.DepthMask(true);

        if (camera.ClearType != ClearType.Color)
            gl.ClearColor(Color.Black);

        if (camera.ClearType != ClearType.OnlyDepth)
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        else
            gl.Clear(ClearBufferMask.DepthBufferBit);

        gl.DepthMask(false);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);

    }
    public override void Render(Camera camera)
    {
        ClearTextureUnit();
        if (camera.ClearType == ClearType.Skybox && camera.SkyboxTexture != null)
        {
            if (camera.ProjectionType == ProjectionType.Orthographic)
            {
                UseShader("SKYBOX", "ORTHOGRAPHIC");
                UniformMatrix4("viewRot", camera.View);
                UniformFloat("farPlane", camera.FarPlane);
                float aspectRatio = camera.RenderTarget.Width / (float)camera.RenderTarget.Height;
                UniformVector2("orthoSize", new Vector2(100 * aspectRatio, 100));

            }
            else
            {
                UseShader("SKYBOX");

            }
            UniformMatrix4("invViewProj", (camera.View * camera.Projection).Inverse());
            UniformTextureCubeMap("uSkybox", camera.SkyboxTexture);
            RenderQuad();
        }
        else if (camera.ClearType == ClearType.Texture && camera.BackgroundTexture != null)
        {
            UseShader("BACKGROUND_TEXTURE");
            UniformTexture("uBackgroundTexture", camera.BackgroundTexture);
            RenderQuad();
        }
    }

    public override void AfterRender(Camera camera)
    {
        gl.DepthMask(true);
        gl.Enable(EnableCap.DepthTest);
    }

}
