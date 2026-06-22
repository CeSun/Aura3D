using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.Maths;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 背景渲染通道，支持天空盒和背景纹理
/// </summary>
public class BackgroundPass: RenderPass
{
    /// <summary>
    /// 初始化背景渲染通道
    /// </summary>
    /// <param name="renderPipeline">渲染管线</param>
    public BackgroundPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.BackgroundVert;
        FragmentShader = ShaderResource.BackgroundFrag;
        ShaderName = nameof(BackgroundPass);
    }


    public override void BeforeRender(Camera camera)
    {

        BindOutputRenderTarget(camera);

        gl.DepthMask(true);

        gl.ClearColor(0, 0, 0, 0);

        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        gl.DepthMask(false);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);

    }
    public override void Render(Camera camera)
    {
        if (camera.IsRenderBackground == false)
            return;

        ClearTextureUnit(); 
        UseShader_Internal();
        if (Scene.Background.IsT0 && Scene.Background.AsT0 != null)
        {
            Matrix4x4 projection = default;

            var worldTransform = camera.WorldTransform;

            var view = Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.Zero + worldTransform.ForwardVector(), worldTransform.UpVector());

            if (camera.ProjectionType == ProjectionType.Orthographic)
            {
                UseShader("SKYBOX", "ORTHOGRAPHIC");
                UniformMatrix4("viewRot", camera.View);
                UniformFloat("farPlane", camera.FarPlane);
                float aspectRatio = camera.Width / (float)camera.Height;
                UniformVector2("orthoSize", new Vector2(100 * aspectRatio, 100));
                projection = camera.Projection;

            }
            else
            {
                UseShader("SKYBOX"); 

                var fovRadians = camera.FieldOfView.DegreeToRadians();

                var aspectRatio = camera.Width / (float)camera.Height;

                projection = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, 10, 100);

            }
            UniformMatrix4("invViewProj", (view * projection).Inverse());
            UniformTextureCubeMap("uSkybox", Scene.Background.AsT0);
            RenderQuad();
        }
        else if (Scene.Background.IsT1 && Scene.Background.AsT1 != null)
        {
            UseShader("BACKGROUND_TEXTURE");
            UniformTexture("uBackgroundTexture", Scene.Background.AsT1);
            RenderQuad();
        }
    }

    public override void AfterRender(Camera camera)
    {
        gl.DepthMask(true);
        gl.Enable(EnableCap.DepthTest);
    }

}
