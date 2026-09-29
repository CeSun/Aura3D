using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.Common;

/// <summary>
/// Represents the tone mapping pass type.
/// </summary>
public class ToneMappingPass : RenderPass
{
    RenderTargetTextureHandle _inputTexture;
    /// <summary>
    /// Initializes a new instance of the tone mapping pass type.
    /// </summary>
    public ToneMappingPass(RenderPipeline renderPipeline, RenderTargetTextureHandle inputTexture) : base(renderPipeline)
    {
        _inputTexture = inputTexture;
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

vec3 acesToneMapping(vec3 color) {
    const float a = 2.51;   
    const float b = 0.03;   
    const float c = 2.43;   
    const float d = 0.59;   
    const float e = 0.14;   
    return (color * (a * color + b)) / (color * (c * color + d) + e);
}

void main()
{
    vec4 hdrColor = texture(u_texture, v_texCoord);
    
    // The curve must consume exposure-scaled HDR directly: a 1.0-exp() pre-stage pushes radiance
    // above 1 back into [0,1], and the ACES shoulder on top of it caps the whole frame at sRGB 213.
    vec3 color = clamp(hdrColor.rgb, 0.0, u_brightnessClamp) * u_exposure;
    vec3 ldrColor = clamp(acesToneMapping(color), 0.0, 1.0);
    
    float finalAlpha = min(hdrColor.a, 1.0);
    
    outColor = vec4(ldrColor, finalAlpha);
}
";
    }

    /// <summary>
    /// Performs the before render operation.
    /// </summary>
    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);

    }
    /// <summary>
    /// Renders the associated data.
    /// </summary>
    public override void Render(Camera camera)
    {
        var source = GetTexture(_inputTexture, camera);

        BindOutputRenderTarget(camera);

        UseShader_Internal();
        ClearTextureUnit();
        UniformTexture("u_texture", source);
        UniformFloat("u_exposure", renderPipeline.Settings.ToneMappingExposure);
        UniformFloat("u_brightnessClamp", renderPipeline.Settings.BrightnessClamp);
        RenderQuad();
    }
}
