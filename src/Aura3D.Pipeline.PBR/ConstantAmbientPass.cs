using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Drawing;
using Aura3D.Core.Renderers;

using Aura3D.Pipeline.PBR.Common;

namespace Aura3D.Pipeline.PBR;

internal class ConstantAmbientPass : RenderPass
{
    readonly RenderTargetHandle gbufferRenderTarget;
    public ConstantAmbientPass(RenderPipeline renderPipeline, RenderTargetHandle gbufferRendertarget) : base(renderPipeline)
    {
        gbufferRenderTarget = gbufferRendertarget;

        this.VertexShader = PbrResources.LightingVertexShader;

        FragmentShader = PbrResources.ConstantAmbientFragmentShader;
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.DepthTest);

        gl.Enable(EnableCap.Blend);

        gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);

        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);

    }

    public override void Render(Camera camera)
    {
        BindOutputRenderTarget(camera);
        gl.ClearColor(0, 0, 0, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        var rt = GetRenderTarget(gbufferRenderTarget, camera);

        var gBufferBaseColor = rt.GetTexture("BaseColor");
        var gBufferNormalRoughness = rt.GetTexture("NormalRoughness");
        var gBufferMetallicEmissive = rt.GetTexture("MetallicEmissive");
        var depthTexture = rt.DepthStencilTexture;

        UseShader("ENBALE_DEFERRED_SHADING");
        UseShader_Internal();
        ClearTextureUnit();

        UniformTexture(nameof(gBufferBaseColor), gBufferBaseColor);
        UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
        UniformTexture(nameof(gBufferMetallicEmissive), gBufferMetallicEmissive);
        UniformTexture(nameof(depthTexture), depthTexture);
        UniformVector3("viewPos", camera.WorldTransform.Translation);
        UniformMatrix4("invProjection", camera.Projection.Inverse());
        UniformMatrix4("invView", camera.View.Inverse());
        UniformColor("ambientColor", Color.White);
        UniformFloat("ambientIntensity", 0.03f);

        RenderQuad();
    }
}
