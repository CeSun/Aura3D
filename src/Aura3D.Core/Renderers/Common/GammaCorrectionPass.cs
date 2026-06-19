using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 伽马校正渲染通道
/// </summary>
public class GammaCorrectionPass : RenderPass
{
    protected string inputRenderTargetName;

    protected string inputRenderTargetTextureName;

    /// <summary>
    /// 初始化伽马校正渲染通道
    /// </summary>
    /// <param name="renderPipeline">渲染管线</param>
    /// <param name="inputRenderTargetName">输入渲染目标名称</param>
    /// <param name="inputRenderTargetTextureName">输入渲染目标纹理名称</param>
    public GammaCorrectionPass(RenderPipeline renderPipeline, string inputRenderTargetName, string inputRenderTargetTextureName) : base(renderPipeline)
    {

        this.inputRenderTargetName = inputRenderTargetName;
        this.inputRenderTargetTextureName = inputRenderTargetTextureName;
        ShaderName = nameof(GammaCorrectionPass);

        VertexShader = @"#version 300 es
precision mediump float;

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aUV;

out vec2 TexCoords;

void main()
{
    TexCoords = aUV;
    
    gl_Position = vec4(aPosition, 1.0);
}
    
";

        FragmentShader = @"#version 300 es
precision mediump float;

in vec2 TexCoords;

out vec4 FragColor;

uniform sampler2D colorTexture;

void main()
{
    float gamma = 2.2;
    float exposure = 1.0;
    vec4 color = texture(colorTexture, TexCoords);
    vec3 rgb = color.rgb;

    rgb = pow(rgb, vec3(1.0 / 2.2));

    FragColor = vec4(rgb, color.a);
}
     
";
    }


    public override void Render(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        var rt = GetRenderTarget(inputRenderTargetName,
            new System.Drawing.Size((int)camera.Width, (int)camera.Height));

        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);

        UseShader();
        ClearTextureUnit();
        UseShader_Internal();
        UniformTexture("colorTexture", rt.GetTexture(inputRenderTargetTextureName));
        RenderQuad();


    }
}
