using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.PBR;

internal class IrradianceMapPass : RenderPass<PBRDeferredPipeline>
{
    const uint _irradianceMapSize = 64;

    public IrradianceMapPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = @"#version 300 es
precision highp float;

layout (location = 0) in vec3 aPos;

out vec3 localPos;

uniform mat4 projection;
uniform mat4 view;

void main()
{
    localPos = aPos;
    gl_Position = projection * view * vec4(localPos, 1.0);
}";

        FragmentShader = @"#version 300 es
precision highp float;

in vec3 localPos;

out vec4 fragColor;

uniform samplerCube environmentMap;

const float PI = 3.14159265359;

void main()
{
    vec3 N = normalize(localPos);
    vec3 irradiance = vec3(0.0);
    
    vec3 up    = vec3(0.0, 1.0, 0.0);
    vec3 right = cross(up, N);
    up         = cross(N, right);

    float sampleDelta = 0.025;
    float nrSamples = 0.0;

    for(float phi = 0.0; phi < 2.0 * PI; phi += sampleDelta) {
        for(float theta = 0.0; theta < 0.5 * PI; theta += sampleDelta) {

            vec3 tangentSample = vec3(sin(theta) * cos(phi),  sin(theta) * sin(phi), cos(theta));

            vec3 sampleVec = tangentSample.x * right + tangentSample.y * up + tangentSample.z * N; 

            irradiance += texture(environmentMap, sampleVec).rgb * cos(theta) * sin(theta);
            nrSamples++;
        }
    }
    irradiance = PI * irradiance * (1.0 / float(nrSamples));

    fragColor = vec4(irradiance, 1.0);
}";

    }


    public override void BeforeRender(Camera camera)
    {
        
    }

    public override void Render(Camera camera)
    {
        CubeTexture? ibl = null;
        if (Scene.Background.IsT0 == false || Scene.Background.AsT0 == null)
        {
            ibl = this.RenderPipeline.DefaultIblAmbientCubeTexture;
        }
        else
        {
            ibl = Scene.Background.AsT0;
        }

        var irradianceMap = camera.GetPipelineGpuState<CubeRenderTarget>("IrradianceMap");

        if (irradianceMap != null)
            return;
        else
        {
            irradianceMap = new CubeRenderTarget()
            .SetSize(_irradianceMapSize, _irradianceMapSize)
            .AddRenderTexture("Irradiance", TextureFormat.Rgb16f)
            .SetDepthTexture(TextureFormat.DepthComponent16);

            renderPipeline.EnsureSynced(irradianceMap);
        }


        camera.SetPipelineGpuState("IrradianceMap", irradianceMap);

        var texture = irradianceMap.GetTexture(0)!;

        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2, 1, 0.1f, 10f);

        Span<Matrix4x4> views =
        [
            Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitX, -Vector3.UnitY),
            Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitX, -Vector3.UnitY),
            Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitY, Vector3.UnitZ),
            Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitY, -Vector3.UnitZ),
            Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitZ, -Vector3.UnitY),
            Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, -Vector3.UnitY)
        ];

        gl.ClearColor(0, 0, 0, 1);
        
        gl.ClearDepth(1);

        UseShader();
        for (int i = 0; i < 6; i ++)
        {
            gl.BindFramebuffer(GLEnum.Framebuffer, irradianceMap.FrameBufferId);

            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + i, texture.TextureId, 0);
            
            gl.Viewport(0, 0, (int)_irradianceMapSize, (int)_irradianceMapSize);

            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            UseShader_Internal();

            UniformMatrix4(nameof(projection), projection);
            
            UniformMatrix4("view", views[i]);
            
            ClearTextureUnit();
            UniformTextureCubeMap("environmentMap", ibl);

            RenderCube();

        }


    }

    public override void AfterRender(Camera camera)
    {
        
    }
}
