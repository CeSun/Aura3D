using Aura3D.Core.Nodes;
using Aura3D.Core.Particles;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core;

/// <summary>
/// Particle render pass using billboard quads + GPU instancing.
/// Renders directly from ParticleSystem nodes, bypassing the InstancedMesh pipeline.
/// GPU buffer upload is managed by the render pipeline on first use.
/// </summary>
public class ParticlePass : RenderPass
{
    public float DefaultParticleSize { get; set; } = 1.0f;
    public float GlobalAlpha { get; set; } = 1.0f;

    private readonly ParticleQuadGeometry _quadGeometry = new();

    public ParticlePass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        ShaderName = nameof(ParticlePass);

        VertexShader = """
            #version 300 es
            precision mediump float;

            //{{defines}}

            layout(location = 0) in vec3 position;
            layout(location = 1) in vec2 texCoord;
            layout(location = 2) in vec4 instancePosRot;
            layout(location = 3) in vec4 instanceColor;
            layout(location = 4) in vec2 instanceSizeAge;

            uniform mat4 viewMatrix;
            uniform mat4 projectionMatrix;
            uniform vec3 cameraRight;
            uniform vec3 cameraUp;
            uniform float uGlobalAlpha;

            out vec2 vTexCoord;
            out vec4 vColor;
            out float vAgeRatio;

            void main()
            {
                vec3 worldPos = instancePosRot.xyz;
                float rot = instancePosRot.w;
                float cosA = cos(rot);
                float sinA = sin(rot);

                vec3 right = cameraRight * cosA + cameraUp * sinA;
                vec3 upVal = -cameraRight * sinA + cameraUp * cosA;

                float size = instanceSizeAge.x;
                vec3 offset = right * position.x + upVal * position.y;
                vec3 billboardPos = worldPos + offset * size;

                gl_Position = projectionMatrix * viewMatrix * vec4(billboardPos, 1.0);

                vTexCoord = texCoord;
                vColor = instanceColor;
                vColor.a *= uGlobalAlpha;
                vAgeRatio = instanceSizeAge.y;
            }
            """;

        FragmentShader = """
            #version 300 es
            precision mediump float;

            //{{defines}}

            in vec2 vTexCoord;
            in vec4 vColor;
            in float vAgeRatio;
            out vec4 outColor;

            #ifdef PARTICLE_TEXTURE
            uniform sampler2D uParticleTexture;
            #ifdef PARTICLE_FLIPBOOK
            uniform vec2 uFlipbookTiles;
            #endif
            #endif

            void main()
            {
                #ifdef PARTICLE_TEXTURE
                #ifdef PARTICLE_FLIPBOOK
                float totalFrames = uFlipbookTiles.x * uFlipbookTiles.y;
                float frame = floor(vAgeRatio * totalFrames);
                frame = clamp(frame, 0.0, totalFrames - 1.0);
                float col = mod(frame, uFlipbookTiles.x);
                float row = floor(frame / uFlipbookTiles.x);
                vec2 uv = (vTexCoord + vec2(col, row)) / uFlipbookTiles;
                #else
                vec2 uv = vTexCoord;
                #endif
                vec4 texColor = texture(uParticleTexture, uv);
                float a = texColor.a * vColor.a;
                outColor = vec4(texColor.rgb * vColor.rgb * a, a);
                #else
                float dist = length(vTexCoord - 0.5) * 2.0;
                float a = vColor.a * (1.0 - smoothstep(0.5, 1.0, dist));
                outColor = vec4(vColor.rgb * a, a);
                #endif
            }
            """;
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Less);
        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.Blend);
        gl.DepthMask(true);
    }

    public override void Render(Camera camera)
    {
        Matrix4x4.Invert(camera.View, out var invView);
        var camRight = new Vector3(invView.M11, invView.M12, invView.M13);
        var camUp = new Vector3(invView.M21, invView.M22, invView.M23);
        var camPos = new Vector3(invView.M41, invView.M42, invView.M43);

        // Opaque
        RenderParticleSystems(BlendMode.Opaque,
            camera.View, camera.Projection, camRight, camUp, camPos, "PARTICLE_OPAQUE");

        // Masked
        RenderParticleSystems(BlendMode.Masked,
            camera.View, camera.Projection, camRight, camUp, camPos, "PARTICLE_MASKED");

        // Translucent (premultiplied alpha, back-to-front sorted)
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.One, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);

        RenderParticleSystems(BlendMode.Translucent,
            camera.View, camera.Projection, camRight, camUp, camPos, "PARTICLE_TRANSLUCENT");

        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
    }

    public override void AfterRender(Camera camera) { }

    private void RenderParticleSystems(
        BlendMode blendMode, Matrix4x4 view, Matrix4x4 proj,
        Vector3 camRight, Vector3 camUp, Vector3 camPos, string baseDefine)
    {
        // Collect matching emitters across all systems (skip mesh-mode — they render through InstancedMesh)
        var emitterEntries = new List<(ParticleSystem System, ParticleEmitter Emitter)>();
        foreach (var ps in renderPipeline.ParticleSystems)
        {
            if (!ps.Enable || !ps.IsPlaying) continue;
            foreach (var em in ps.Emitters)
            {
                if (em.ActiveCount == 0) continue;
                if (em.UseMeshRenderer) continue;
                if (em.BlendMode != blendMode) continue;
                emitterEntries.Add((ps, em));
            }
        }

        // Sort translucent emitters back-to-front by system center distance
        if (blendMode == BlendMode.Translucent)
        {
            emitterEntries.Sort((a, b) =>
            {
                float da = Vector3.DistanceSquared(a.System.WorldTransform.Translation, camPos);
                float db = Vector3.DistanceSquared(b.System.WorldTransform.Translation, camPos);
                return db.CompareTo(da);
            });
        }

        foreach (var (ps, em) in emitterEntries)
        {
            // Sort translucent particles within this emitter back-to-front
            em.SortByDistance(camPos);

            bool hasTex = em.Texture != null;
            bool hasFlipbook = hasTex && (em.FlipbookTiles.X > 1f || em.FlipbookTiles.Y > 1f);

            if (hasTex && hasFlipbook)
                UseShader(baseDefine, "PARTICLE_TEXTURE", "PARTICLE_FLIPBOOK");
            else if (hasTex)
                UseShader(baseDefine, "PARTICLE_TEXTURE");
            else
                UseShader(baseDefine);

            UseShader_Internal();
            SetCommonUniforms(view, proj, camRight, camUp);

            if (hasTex)
                UniformTexture("uParticleTexture", em.Texture!);

            if (hasFlipbook)
                UniformVector2("uFlipbookTiles", em.FlipbookTiles);

            // Pack sorted data, upload, and draw this emitter's particles
            if (em.Particles != null && em.GpuBuffer != null)
            {
                em.GpuBuffer.SetParticleData(em.Particles, em.ActiveCount);
                em.GpuBuffer.QuadGeometry = _quadGeometry;
                renderPipeline.EnsureUploaded(_quadGeometry);
                renderPipeline.EnsureUploaded(em.GpuBuffer);
                em.GpuBuffer.Draw(gl!);
            }
        }
    }

    private void SetCommonUniforms(Matrix4x4 view, Matrix4x4 proj, Vector3 camRight, Vector3 camUp)
    {
        ClearTextureUnit();
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", proj);
        UniformVector3("cameraRight", camRight);
        UniformVector3("cameraUp", camUp);
        UniformFloat("uGlobalAlpha", GlobalAlpha);
    }
}
