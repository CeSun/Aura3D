using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.Maths;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the background pass type.
/// </summary>
public class BackgroundPass: RenderPass
{
    /// <summary>
    /// Initializes a new instance of the background pass type.
    /// </summary>
    public BackgroundPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.BackgroundVert;
        FragmentShader = ShaderResource.BackgroundFrag;
        ShaderName = nameof(BackgroundPass);
    }


    /// <summary>
    /// Performs the before render operation.
    /// </summary>
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
    /// <summary>
    /// Renders the associated data.
    /// </summary>
    public override void Render(Camera camera)
    {
        if (camera.IsRenderBackground == false)
            return;

        ClearTextureUnit();

        if (Scene.Background.IsT0 && Scene.Background.AsT0 != null)
        {
            Matrix4x4 projection = default;

            var worldTransform = camera.WorldTransform;

            var view = Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.Zero + worldTransform.ForwardVector(), worldTransform.UpVector());

            // 与其它 pass 一致：先 UseShader 设定宏，再 UseShader_Internal 绑定程序。
            // 反过来写会让本帧绑定上一帧（或首帧的空宏）变体：uniform 位置查不到被静默跳过，
            // 而空宏变体的 background.frag 两条 #ifdef 都不成立，片元输出 outColor 从未被写入，
            // 编译后程序没有任何片元输出。桌面 GL 与 ANGLE 宽容，WebGL2 会按
            // "missing fragment shader outputs" 判 GL_INVALID_OPERATION 丢弃这条 draw。
            if (camera.ProjectionType == ProjectionType.Orthographic)
            {
                UseShader("SKYBOX", "ORTHOGRAPHIC");
                UseShader_Internal();

                UniformMatrix4("viewRot", camera.View);
                UniformFloat("farPlane", camera.FarPlane);
                float aspectRatio = camera.Width / (float)camera.Height;
                UniformVector2("orthoSize", new Vector2(100 * aspectRatio, 100));
                projection = camera.Projection;

            }
            else
            {
                UseShader("SKYBOX");
                UseShader_Internal();

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
            UseShader_Internal();
            UniformTexture("uBackgroundTexture", Scene.Background.AsT1);
            RenderQuad();
        }
    }

    /// <summary>
    /// Performs the after render operation.
    /// </summary>
    public override void AfterRender(Camera camera)
    {
        gl.DepthMask(true);
        gl.Enable(EnableCap.DepthTest);
    }

}
