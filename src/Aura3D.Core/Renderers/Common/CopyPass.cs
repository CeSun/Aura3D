using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.Common;

/// <summary>
/// Represents the copy pass type.
/// </summary>
public class CopyPass : RenderPass
{
    RenderTargetTextureHandle _inputTexture;
    /// <summary>
    /// Initializes a new instance of the copy pass type.
    /// </summary>
    public CopyPass(RenderPipeline renderPipeline, RenderTargetTextureHandle inputTexture) : base(renderPipeline)
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

out vec4 outColor;

void main()
{
    vec4 color  = texture(u_texture, v_texCoord);
    color.a = min(color.a, 1.0);
    outColor = color;
}
";
    }


    /// <summary>
    /// Performs the before render operation.
    /// </summary>
    public override void BeforeRender(Camera camera)
    {
        gl.Enable(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)  ;
        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);


    }
    /// <summary>
    /// Renders the associated data.
    /// </summary>
    public override void Render(Camera camera)
    {
        BindOutputRenderTarget(camera);

        var source = GetTexture(_inputTexture, camera);
        
        UseShader_Internal();
        ClearTextureUnit(); 
        UniformTexture("u_texture", source);
        RenderQuad();
    }
}
