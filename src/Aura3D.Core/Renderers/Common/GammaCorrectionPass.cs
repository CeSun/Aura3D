using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public class GammaCorrectionPass : RenderPass
{
    protected string inputRenderTargetName;

    protected string inputRenderTargetTextureName;

    public GammaCorrectionPass(RenderPipeline renderPipeline, string inputRenderTargetName, string inputRenderTargetTextureName) : base(renderPipeline)
    {

        this.inputRenderTargetName = inputRenderTargetName;
        this.inputRenderTargetTextureName = inputRenderTargetTextureName;

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
    vec3 color = texture(colorTexture, TexCoords).rgb;

    color = pow(color, vec3(1.0 / 2.2));

    FragColor = vec4(color, 1.0);
}
     
";
    }


    public override void Render(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        var rt = GetRenderTarget(inputRenderTargetName,
            new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height));

        gl.Disable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        UseShader();
        ClearTextureUnit();
        UniformTexture("colorTexture", rt.GetTexture(inputRenderTargetTextureName));
        RenderQuad();


    }
}
