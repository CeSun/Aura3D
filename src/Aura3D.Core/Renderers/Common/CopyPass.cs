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
/// 复制渲染通道，将一个渲染目标的纹理复制到另一个渲染目标
/// </summary>
public class CopyPass : RenderPass
{
    string _inputRenderTargetName;

    string _inputRenderTargetTextureName;
    /// <summary>
    /// 初始化复制渲染通道
    /// </summary>
    /// <param name="renderPipeline">渲染管线</param>
    /// <param name="inputRenderTargetName">输入渲染目标名称</param>
    /// <param name="inputRenderTargetTextureName">输入渲染目标纹理名称</param>
    public CopyPass(RenderPipeline renderPipeline, string inputRenderTargetName, string inputRenderTargetTextureName) : base(renderPipeline)
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

out vec4 outColor;

void main()
{
    vec4 color  = texture(u_texture, v_texCoord);
    color.a = min(color.a, 1.0);
    outColor = color;
}
";
    }


    public override void BeforeRender(Camera camera)
    {
        gl.Enable(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)  ;
        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);


    }
    public override void Render(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        var inputrt = GetRenderTarget(_inputRenderTargetName, new System.Drawing.Size((int)camera.Width, (int)camera.Height));
        var source = inputrt.GetTexture(_inputRenderTargetTextureName);
        if (source == null)
            throw new InvalidOperationException($"Source texture '{_inputRenderTargetTextureName}' not found in render target '{_inputRenderTargetName}'.");
        
        UseShader_Internal();
        ClearTextureUnit(); 
        UniformTexture("u_texture", source);
        RenderQuad();
    }
}
