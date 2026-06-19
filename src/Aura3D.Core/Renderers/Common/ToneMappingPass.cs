using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.Common;

/// <summary>
/// 色调映射渲染通道，将 HDR 颜色映射到 LDR 范围
/// </summary>
public class ToneMappingPass : RenderPass
{
    string _inputRenderTargetName;

    string _inputRenderTargetTextureName;
    /// <summary>
    /// 初始化色调映射渲染通道
    /// </summary>
    /// <param name="renderPipeline">渲染管线</param>
    /// <param name="inputRenderTargetName">输入渲染目标名称</param>
    /// <param name="inputRenderTargetTextureName">输入渲染目标纹理名称</param>
    public ToneMappingPass(RenderPipeline renderPipeline, string inputRenderTargetName, string inputRenderTargetTextureName) : base(renderPipeline)
    {
        _inputRenderTargetName = inputRenderTargetName;
        _inputRenderTargetTextureName = inputRenderTargetTextureName;
        VertexShader = @"#version 300 es
layout(location = 0) in vec3 a_position;
layout(location = 1) in vec2 a_texCoord;

out vec2 v_texCoord;

void main() {
    gl_Position = vec4(a_position, 1.0);
    v_texCoord = a_texCoord;
}
";

        FragmentShader = @"#version 300 es
precision mediump float;

in vec2 v_texCoord;

uniform sampler2D u_texture;  
uniform float u_exposure;       
uniform float u_brightnessClamp;

out vec4 outColor;

vec3 acesToneMappingMobile(vec3 color) {
    const float a = 1.8;    
    const float b = 0.02;   
    const float c = 2.0;    
    const float d = 0.6;    
    const float e = 0.12;   
    color = clamp((color * (a * color + b)) / (color * (c * color + d) + e), 0.0, 1.0);
    return color;
}

vec3 applyExposureMobile(vec3 hdrColor, float exposure) {
    hdrColor = clamp(hdrColor, 0.0, u_brightnessClamp); 
    return 1.0 - exp(-hdrColor * exposure);
}

void main()
{
    vec4 hdrColor = texture(u_texture, v_texCoord);
    
    vec3 ldrColor = applyExposureMobile(hdrColor.rgb, u_exposure); 
    ldrColor = acesToneMappingMobile(ldrColor);                   
    ldrColor = clamp(ldrColor, 0.0, 1.0);
    
    float finalAlpha = min(hdrColor.a, 1.0);
    
    outColor = vec4(ldrColor, finalAlpha);
}
";
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);

    }
    public override void Render(Camera camera)
    {

        var inputrt = GetRenderTarget(_inputRenderTargetName, new System.Drawing.Size((int)camera.Width, (int)camera.Height));
        var source = inputrt.GetTexture(_inputRenderTargetTextureName);
        if (source == null)
            throw new InvalidOperationException($"Source texture '{_inputRenderTargetTextureName}' not found in render target '{_inputRenderTargetName}'.");

        BindOutPutRenderTarget(camera);

        UseShader_Internal();
        ClearTextureUnit();
        UniformTexture("u_texture", source);
        UniformFloat("u_exposure", renderPipeline.Settings.ToneMappingExposure);
        UniformFloat("u_brightnessClamp", renderPipeline.Settings.BrightnessClamp);
        RenderQuad();
    }
}
