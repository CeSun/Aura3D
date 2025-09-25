using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public class FxaaPass : RenderPass
{
    public FxaaPass(RenderPipeline renderPipeline, string inputRenderTargetName, string inputRenderTargetTextureName) : base(renderPipeline)
    {
        this.inputRenderTargetName = inputRenderTargetName;
        this.inputRenderTargetTextureName = inputRenderTargetTextureName;
        VertexShader = @"#version 300 es
layout(location = 0) in vec3 a_position;
layout(location = 1) in vec2 a_texCoord;

out vec2 v_texCoord;

void main() {
    gl_Position = vec4(a_position, 1.0);
    v_texCoord = a_texCoord;
}
    
";

        var fs = @"#version 300 es
precision mediump float;

in vec2 v_texCoord;

uniform sampler2D u_texture;

uniform vec2 u_textureSize;

out vec4 outColor;

void main()
{
   outColor = texture(u_texture, v_texCoord);
}
";

        var fs1 = @"#version 300 es
precision mediump float;

uniform sampler2D u_texture;
uniform vec2 u_textureSize;
in vec2 v_texCoord;
out vec4 outColor;

#define EDGE_STEP_COUNT 6
#define EDGE_GUESS 8.0
#define EDGE_STEPS 1.0, 1.5, 2.0, 2.0, 2.0, 4.0
const float edgeSteps[EDGE_STEP_COUNT] = float[EDGE_STEP_COUNT](EDGE_STEPS);

float _ContrastThreshold = 0.0312;
float _RelativeThreshold = 0.063;
float _SubpixelBlending = 1.0;

vec4 Sample(sampler2D tex2D, vec2 uv) {
    return texture(tex2D, uv);
}

float SampleLuminance(sampler2D tex2D, vec2 uv) {
    return dot(Sample(tex2D, uv).rgb, vec3(0.3, 0.59, 0.11));
}

float SampleLuminance(sampler2D tex2D, vec2 texSize, vec2 uv, float uOffset, float vOffset) {
    uv += texSize * vec2(uOffset, vOffset);
    return SampleLuminance(tex2D, uv);
}

struct LuminanceData {
    float m, n, e, s, w;
    float ne, nw, se, sw;
    float highest, lowest, contrast;
};

LuminanceData SampleLuminanceNeighborhood(sampler2D tex2D, vec2 texSize, vec2 uv) {
    LuminanceData l;
    l.m = SampleLuminance(tex2D, uv);
    l.n = SampleLuminance(tex2D, texSize, uv, 0.0, 1.0);
    l.e = SampleLuminance(tex2D, texSize, uv, 1.0, 0.0);
    l.s = SampleLuminance(tex2D, texSize, uv, 0.0, -1.0);
    l.w = SampleLuminance(tex2D, texSize, uv, -1.0, 0.0);

    l.ne = SampleLuminance(tex2D, texSize, uv, 1.0, 1.0);
    l.nw = SampleLuminance(tex2D, texSize, uv, -1.0, 1.0);
    l.se = SampleLuminance(tex2D, texSize, uv, 1.0, -1.0);
    l.sw = SampleLuminance(tex2D, texSize, uv, -1.0, -1.0);

    l.highest = max(max(max(max(l.n, l.e), l.s), l.w), l.m);
    l.lowest = min(min(min(min(l.n, l.e), l.s), l.w), l.m);
    l.contrast = l.highest - l.lowest;
    return l;
}

bool ShouldSkipPixel(LuminanceData l) {
    float threshold = max(_ContrastThreshold, _RelativeThreshold * l.highest);
    return l.contrast < threshold;
}

float DeterminePixelBlendFactor(LuminanceData l) {
    float f = 2.0 * (l.n + l.e + l.s + l.w);
    f += l.ne + l.nw + l.se + l.sw;
    f *= 1.0 / 12.0;
    f = abs(f - l.m);
    f = clamp(f / l.contrast, 0.0, 1.0);

    float blendFactor = smoothstep(0.0, 1.0, f);
    return blendFactor * blendFactor * _SubpixelBlending;
}

struct EdgeData {
    bool isHorizontal;
    float pixelStep;
    float oppositeLuminance, gradient;
};

EdgeData DetermineEdge(vec2 texSize, LuminanceData l) {
    EdgeData e;
    float horizontal =
        abs(l.n + l.s - 2.0 * l.m) * 2.0 +
        abs(l.ne + l.se - 2.0 * l.e) +
        abs(l.nw + l.sw - 2.0 * l.w);
    float vertical =
        abs(l.e + l.w - 2.0 * l.m) * 2.0 +
        abs(l.ne + l.nw - 2.0 * l.n) +
        abs(l.se + l.sw - 2.0 * l.s);
    e.isHorizontal = horizontal >= vertical;

    float pLuminance = e.isHorizontal ? l.n : l.e;
    float nLuminance = e.isHorizontal ? l.s : l.w;
    float pGradient = abs(pLuminance - l.m);
    float nGradient = abs(nLuminance - l.m);

    e.pixelStep = e.isHorizontal ? texSize.y : texSize.x;

    if (pGradient < nGradient) {
        e.pixelStep = -e.pixelStep;
        e.oppositeLuminance = nLuminance;
        e.gradient = nGradient;
    } else {
        e.oppositeLuminance = pLuminance;
        e.gradient = pGradient;
    }

    return e;
}

float DetermineEdgeBlendFactor(sampler2D tex2D, vec2 texSize, LuminanceData l, EdgeData e, vec2 uv) {
    vec2 uvEdge = uv;
    vec2 edgeStep;
    if (e.isHorizontal) {
        uvEdge.y += e.pixelStep * 0.5;
        edgeStep = vec2(texSize.x, 0.0);
    } else {
        uvEdge.x += e.pixelStep * 0.5;
        edgeStep = vec2(0.0, texSize.y);
    }

    float edgeLuminance = (l.m + e.oppositeLuminance) * 0.5;
    float gradientThreshold = e.gradient * 0.25;

    vec2 puv = uvEdge + edgeStep * edgeSteps[0];
    float pLuminanceDelta = SampleLuminance(tex2D, puv) - edgeLuminance;
    bool pAtEnd = abs(pLuminanceDelta) >= gradientThreshold;

    for (int i = 1; i < EDGE_STEP_COUNT; i++) {
        if (pAtEnd) break;
        puv += edgeStep * edgeSteps[i];
        pLuminanceDelta = SampleLuminance(tex2D, puv) - edgeLuminance;
        pAtEnd = abs(pLuminanceDelta) >= gradientThreshold;
    }

    if (!pAtEnd) {
        puv += edgeStep * EDGE_GUESS;
    }

    vec2 nuv = uvEdge - edgeStep * edgeSteps[0];
    float nLuminanceDelta = SampleLuminance(tex2D, nuv) - edgeLuminance;
    bool nAtEnd = abs(nLuminanceDelta) >= gradientThreshold;

    for (int i = 1; i < EDGE_STEP_COUNT; i++) {
        if (nAtEnd) break;
        nuv -= edgeStep * edgeSteps[i];
        nLuminanceDelta = SampleLuminance(tex2D, nuv) - edgeLuminance;
        nAtEnd = abs(nLuminanceDelta) >= gradientThreshold;
    }

    if (!nAtEnd) {
        nuv -= edgeStep * EDGE_GUESS;
    }

    float pDistance, nDistance;
    if (e.isHorizontal) {
        pDistance = puv.x - uv.x;
        nDistance = uv.x - nuv.x;
    } else {
        pDistance = puv.y - uv.y;
        nDistance = uv.y - nuv.y;
    }

    float shortestDistance;
    bool deltaSign;
    if (pDistance <= nDistance) {
        shortestDistance = pDistance;
        deltaSign = pLuminanceDelta >= 0.0;
    } else {
        shortestDistance = nDistance;
        deltaSign = nLuminanceDelta >= 0.0;
    }

    if (deltaSign == (l.m - edgeLuminance >= 0.0)) {
        return 0.0;
    }

    return 0.5 - shortestDistance / (pDistance + nDistance);
}

vec4 ApplyFXAA(sampler2D tex2D, vec2 texSize, vec2 uv) {
    LuminanceData luminance = SampleLuminanceNeighborhood(tex2D, texSize, uv);
    if (ShouldSkipPixel(luminance)) {
        return Sample(tex2D, uv);
    }

    float pixelBlend = DeterminePixelBlendFactor(luminance);
    EdgeData edge = DetermineEdge(texSize, luminance);
    float edgeBlend = DetermineEdgeBlendFactor(tex2D, texSize, luminance, edge, uv);
    float finalBlend = max(pixelBlend, edgeBlend);

    if (edge.isHorizontal) {
        uv.y += edge.pixelStep * finalBlend;
    } else {
        uv.x += edge.pixelStep * finalBlend;
    }

    return Sample(tex2D, uv);
}

void main() {
    outColor = ApplyFXAA(u_texture, 1.0 / u_textureSize, v_texCoord);
}

";
        FragmentShader = fs1;
    }


    public override void Render(Camera camera)
    {
        var size = new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height);
        gl.BindFramebuffer(GLEnum.Framebuffer, camera.RenderTarget.FrameBufferId);

        var rt = GetRenderTarget(inputRenderTargetName, size);

        gl.Disable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend); 
        UseShader();
        ClearTextureUnit();
        UniformTexture("u_texture", rt.GetTexture(inputRenderTargetTextureName));
        UniformVector2("u_textureSize", new Vector2(rt.GetTexture(inputRenderTargetTextureName).Width, rt.GetTexture(inputRenderTargetTextureName).Height));
        RenderQuad();


    }



    protected string inputRenderTargetName;

    protected string inputRenderTargetTextureName;
}
