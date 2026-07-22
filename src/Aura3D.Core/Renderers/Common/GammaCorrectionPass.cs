using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Represents the gamma correction pass type.
/// </summary>
public class GammaCorrectionPass : RenderPass
{
    /// <summary>
    /// Gets the input texture.
    /// </summary>
    protected RenderTargetTextureHandle inputTexture;

    /// <summary>
    /// Initializes a new instance of the gamma correction pass type.
    /// </summary>
    public GammaCorrectionPass(RenderPipeline renderPipeline, RenderTargetTextureHandle inputTexture) : base(renderPipeline)
    {
        this.inputTexture = inputTexture;
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


    /// <summary>
    /// Renders the associated data.
    /// </summary>
    public override void Render(Camera camera)
    {
        BindOutputRenderTarget(camera);
        var source = GetTexture(inputTexture, camera);

        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);

        UseShader();
        ClearTextureUnit();
        UseShader_Internal();
        UniformTexture("colorTexture", source);
        RenderQuad();


    }
}
