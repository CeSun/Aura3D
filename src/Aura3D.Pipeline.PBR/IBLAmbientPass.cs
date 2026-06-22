using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;
using Aura3D.Core.Renderers;
using Aura3D.Core;

namespace Aura3D.Pipeline.PBR;

internal class IBLAmbientPass : RenderPass<PBRDeferredPipeline>
{
    readonly RenderTargetHandle gbufferRenderTarget;
    public IBLAmbientPass(RenderPipeline renderPipeline, RenderTargetHandle gbufferRenderTarget) : base(renderPipeline)
    {
        VertexShader = @"#version 300 es
precision highp float;

layout(location = 0) in vec3 a_position;
layout(location = 1) in vec2 a_texCoord;

out vec2 v_texCoord;
out vec4 v_clipPos;

void main() {
    gl_Position = vec4(a_position, 1.0);
    v_texCoord = a_texCoord;
    v_clipPos = gl_Position;
}";

        FragmentShader = ShaderResource.pbr_ibl_ambient_frag;
        this.gbufferRenderTarget = gbufferRenderTarget;
    }


    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.DepthTest);

        gl.Enable(EnableCap.Blend);

        gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);

        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);

        BindOutputRenderTarget(camera);
        gl.ClearColor(0, 0, 0, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public override void Render(Camera camera)
    {
        var rt = GetRenderTarget(gbufferRenderTarget, camera);

        var gBufferBaseColor = rt.GetTexture("BaseColor");
        var gBufferNormalRoughness = rt.GetTexture("NormalRoughness");
        var gBufferMetallicEmissive = rt.GetTexture("MetallicEmissive");
        var depthTexture = rt.DepthStencilTexture;

        var u_brdfLUT = RenderPipeline.BrdfLutTexture;


        var irradianceMap = camera.GetPipelineGpuState<CubeRenderTarget>("IrradianceMap");
        var u_irradianceMap = irradianceMap.GetTexture(0);

        var perfilteredEnvMap = camera.GetPipelineGpuState<CubeRenderTarget>("PrefilteredEnvironmentMap");
        var u_prefilterMap = perfilteredEnvMap.GetTexture(0);

        UseShader("ENBALE_DEFERRED_SHADING");
        UseShader_Internal();
        ClearTextureUnit();

        UniformTexture(nameof(gBufferBaseColor), gBufferBaseColor);
        UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
        UniformTexture(nameof(gBufferMetallicEmissive), gBufferMetallicEmissive);
        UniformTexture(nameof(depthTexture), depthTexture);
        UniformTexture(nameof(u_brdfLUT), u_brdfLUT);
        UniformTextureCubeMap(nameof(u_irradianceMap), u_irradianceMap);
        UniformTextureCubeMap(nameof(u_prefilterMap), u_prefilterMap);
        var u_viewMatrix = camera.View;
        var u_projMatrix = camera.Projection;
        var u_invViewProjMatrix = (u_viewMatrix * u_projMatrix).Inverse();
        UniformMatrix4(nameof(u_viewMatrix), u_viewMatrix);
        UniformMatrix4(nameof(u_projMatrix), u_projMatrix);
        UniformMatrix4(nameof(u_invViewProjMatrix), u_invViewProjMatrix);
        UniformVector3("u_cameraPos", camera.WorldTransform.Translation);

        int nearestPowerOfTwo = (int)MathF.Pow(2, MathF.Floor(MathF.Log2(u_prefilterMap.Width)));
        var mipmap =  BitOperations.TrailingZeroCount((uint)nearestPowerOfTwo) + 1;

        UniformFloat("u_max_mipmap", mipmap);
        RenderQuad();
    }

    public override void AfterRender(Camera camera)
    {

    }

}
       
