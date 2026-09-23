using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Scenes;
using Aura3D.Pipeline.PBR;
using Metal;
using SkiaSharp;

#pragma warning disable AVA1700 // Unstable API

namespace Example.iOS;

/// <summary>
/// T4 临时宿主：在自建 ANGLE（Metal 后端）上下文里驱动真实 Aura3D Scene，
/// 输出 MTLTexture 走 T3 已验证的 lease 通路合成上屏。验证结束后连同 App.RootViewFactory 钩子一起删除。
/// 生命周期复刻 Aura3DViewBase：EnsureScene → Pipeline.Initialize(GetProc) → Update(dt) → Render()。
/// </summary>
public sealed class AngleSceneHost : Control
{
    // EGL tokens
    private const int EGL_OPENGL_ES_API = 0x30A0;
    private const int EGL_SURFACE_TYPE = 0x3033;
    private const int EGL_PBUFFER_BIT = 0x0001;
    private const int EGL_RENDERABLE_TYPE = 0x3040;
    private const int EGL_OPENGL_ES3_BIT = 0x0040;
    private const int EGL_RED_SIZE = 0x3024;
    private const int EGL_GREEN_SIZE = 0x3023;
    private const int EGL_BLUE_SIZE = 0x3022;
    private const int EGL_ALPHA_SIZE = 0x3021;
    private const int EGL_NONE = 0x3038;
    private const int EGL_CONTEXT_CLIENT_VERSION = 0x3098;
    private const int EGL_EXTENSIONS = 0x3055;
    private const int EGL_WIDTH = 0x3057;
    private const int EGL_HEIGHT = 0x3056;
    private const int EGL_METAL_TEXTURE_ANGLE = 0x34A7;
    private const int EGL_PLATFORM_ANGLE_ANGLE = 0x3202;
    private const int EGL_PLATFORM_ANGLE_TYPE_ANGLE = 0x3203;
    private const int EGL_PLATFORM_ANGLE_TYPE_METAL_ANGLE = 0x3489;

    // GLES tokens
    private const int GL_TEXTURE_2D = 0x0DE1;
    private const int GL_FRAMEBUFFER = 0x8D40;
    private const int GL_COLOR_ATTACHMENT0 = 0x8CE0;
    private const int GL_FRAMEBUFFER_COMPLETE = 0x8CD5;
    private const int GL_VERSION = 0x1F02;
    private const int GL_RENDERER = 0x1F01;
    private const int GL_ALIASED_POINT_SIZE_RANGE = 0x846D;
    private const int GL_MAX_TEXTURE_SIZE = 0x0D33;
    private const int GL_RGBA = 0x1908;
    private const int GL_UNSIGNED_BYTE = 0x1401;
    private const int GL_FLOAT = 0x1406;
    private const int GL_DEPTH_COMPONENT = 0x1902;
    private const int GL_DRAW_FRAMEBUFFER_BINDING = 0x8CA6;

    /// <summary>管线工厂，默认与 Aura3DView 一致（BlinnPhong）。</summary>
    public Func<Scene, RenderPipeline> CreateRenderPipeline { get; init; } = scene => new BlinnPhongPipeline(scene);

    public PipelineSettings PipelineSettings { get; init; } = new();

    /// <summary>场景创建、管线初始化完成后在渲染线程回调一次，用于添加节点。CPU-only 操作。</summary>
    public Action<Scene>? SceneConfigured { get; init; }

    /// <summary>每帧 scene.Update 之前在渲染线程回调。</summary>
    public Action<Scene, double>? FrameUpdating { get; init; }

    /// <summary>GL 纹理为左下原点，Skia 按 TopLeft 导入后需要纵向翻转才能正确显示。</summary>
    public bool FlipVertical { get; init; } = true;

    /// <summary>状态文本，由宿主页面创建并布局。</summary>
    public TextBlock? Status { get; init; }

    public Scene? Scene => _scene;

    public bool IsFailed => _failed;

    private readonly LeaseOperation _operation;
    private readonly Queue<string> _lines = new();
    private readonly RenderSurface _renderSurface = new();
    private readonly Stopwatch _stopwatch = new();

    private Scene? _scene;
    private IMTLTexture? _texture;
    private IntPtr _display;
    private IntPtr _context;
    private IntPtr _surface;
    private IntPtr _config;
    private IntPtr _eglImage;
    private uint _glTexture;
    private uint _fbo;
    private uint _pixelW;
    private uint _pixelH;
    private bool _failed;
    private int _frame;
    private DispatcherTimer? _timer;

    public AngleSceneHost()
    {
        _operation = new LeaseOperation(this);
    }

    public override void Render(DrawingContext context)
    {
        context.Custom(_operation);
        base.Render(context);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    internal void RenderInto(ImmediateDrawingContext context)
    {
        if (_failed)
            return;

        var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        try
        {
            if (feature is null)
            {
                Fail("no lease feature");
                return;
            }

            if (_display == IntPtr.Zero && !InitEgl())
                return;

            if (!EnsureOutput())
                return;

            if (_scene == null)
                EnsureScene();

            double dt = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();

            FrameUpdating?.Invoke(_scene!, dt);
            _scene!.Update(dt);
            if (_frame == 1 && Environment.GetEnvironmentVariable("AURA_DEBUG_DRIVE") == "1")
                DebugDriveRealFrame(_scene!);
            else
                _scene!.RenderPipeline.Render();

            if (_frame == 2 && Environment.GetEnvironmentVariable("AURA_DEBUG_REPLAY") == "1")
            {
                DebugReplayPasses(_scene!);
                DebugDepthSamplerProbe();
            }

            if (_frame == 2 && Environment.GetEnvironmentVariable("AURA_DEBUG_REAL") == "1")
            {
                DebugReadRealTargets(_scene!);
                DebugProbeCoreTextures(_scene!);
                DebugProbeRealIbl(_scene!);
            }

            if (_frame is 0 or 30)
            {
                Native.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
                var px = new byte[4];
                Native.glReadPixels((int)(_pixelW / 2), (int)(_pixelH / 2), 1, 1, GL_RGBA, GL_UNSIGNED_BYTE, px);
                Log($"center px=({px[0]},{px[1]},{px[2]},{px[3]})");
            }

            Native.glFinish(); // T4 用同步 finish 保证纹理内容就绪，正式实现换 shared event

            var errCount = 0;
            int firstErr = 0;
            int err;
            while ((err = Native.glGetError()) != 0)
            {
                if (errCount == 0) firstErr = err;
                errCount++;
                if (errCount > 32) break;
            }
            if (errCount > 0)
                Log($"glError 0x{firstErr:X} x{errCount} frame={_frame}");

            Compose(feature!);

            _frame++;
        }
        catch (Exception ex)
        {
            Fail($"EX {ex.Message}");
        }
    }

    private RenderTarget? FindRenderTarget(RenderPipeline pipeline, System.Drawing.Size size, string name)
    {
        var field = typeof(RenderPipeline).GetField("renderTargets",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(pipeline) is not Dictionary<string, Dictionary<System.Drawing.Size, (RenderTarget, DateTime)>> map)
            return null;
        return map.TryGetValue(name, out var sm) && sm.TryGetValue(size, out var entry) ? entry.Item1 : null;
    }

    private string CenterPixel(RenderTarget rt, System.Drawing.Size size)
    {
        Native.glBindFramebuffer(GL_FRAMEBUFFER, rt.FrameBufferId);
        var pf = new float[4];
        Native.glReadPixels(size.Width / 2, size.Height / 2, 1, 1, GL_RGBA, GL_FLOAT, pf);
        if (Native.glGetError() == 0)
            return $"({pf[0]:F3},{pf[1]:F3},{pf[2]:F3},{pf[3]:F3})";
        Native.glGetError();
        var pb = new byte[4];
        Native.glReadPixels(size.Width / 2, size.Height / 2, 1, 1, GL_RGBA, GL_UNSIGNED_BYTE, pb);
        Native.glGetError();
        return $"8({pb[0]},{pb[1]},{pb[2]},{pb[3]})";
    }

    private void DebugDriveRealFrame(Scene scene)
    {
        Log("== drive frame start ==");
        var pipeline = scene.RenderPipeline;
        var camera = scene.MainCamera;
        var size = new System.Drawing.Size((int)camera.Width, (int)camera.Height);

        void DrainErrors(string tag)
        {
            var n = 0;
            int first = 0, e;
            while ((e = Native.glGetError()) != 0)
            {
                if (n == 0) first = e;
                n++;
                if (n > 8) break;
            }
            if (n > 0) Log($"drive {tag} err=0x{first:X} x{n}");
        }

        void AfterPass(RenderPass pass, string stage)
        {
            DrainErrors($"{pass.GetType().Name}/full");
            var baseTxt = FindRenderTarget(pipeline, size, "BaseRenderTarget") is { } brt
                ? CenterPixel(brt, size) : "-";
            var bgTxt = FindRenderTarget(pipeline, size, "BackgroundRenderTarget") is { } bgrt
                ? CenterPixel(bgrt, size) : "-";
            Native.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
            Log($"drive {stage} {pass.GetType().Name} BaseRT={baseTxt} BgRT={bgTxt}");
        }

        void StagedRender(RenderPass pass, bool cam, Camera camera)
        {
            if (cam) pass.BeforeRender(camera); else pass.BeforeRender();
            DrainErrors($"{pass.GetType().Name}/BeforeRender");
            if (cam) pass.Render(camera); else pass.Render();
            DrainErrors($"{pass.GetType().Name}/Render");
            if (cam) pass.AfterRender(camera); else pass.AfterRender();
            DrainErrors($"{pass.GetType().Name}/AfterRender");
        }

        pipeline.BeforeRender();
        foreach (var pass in pipeline.OnceRenderPasses)
        {
            StagedRender(pass, false, camera);
            AfterPass(pass, "once");
        }

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var vis = (List<Aura3D.Core.Nodes.Mesh>)typeof(RenderPipeline)
            .GetField("_visibleMeshesInCamera", flags)!.GetValue(pipeline)!;
        var visI = (List<Aura3D.Core.Nodes.InstancedMesh>)typeof(RenderPipeline)
            .GetField("_visibleInstancedMeshesInCamera", flags)!.GetValue(pipeline)!;
        vis.Clear();
        visI.Clear();
        if (pipeline.EnableFrustumCulling)
        {
            pipeline.UpdateVisibleMeshesInCamera(camera.View, camera.Projection, vis);
            pipeline.UpdateVisibleInstancedMeshesInCamera(camera.View, camera.Projection, visI);
        }
        else
        {
            vis.AddRange(pipeline.Meshes);
            visI.AddRange(pipeline.InstancedMeshes);
        }

        pipeline.BeforeCameraRender(camera);
        foreach (var pass in pipeline.EveryCameraRenderPasses)
        {
            StagedRender(pass, true, camera);
            AfterPass(pass, "cam");
        }
        pipeline.AfterCameraRender(camera);
        pipeline.AfterRender();
        Native.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
        DrainErrors("end");
        Log("== drive frame end ==");
    }

    private void DebugReadRealTargets(Scene scene)
    {
        var pipeline = scene.RenderPipeline;
        var field = typeof(RenderPipeline).GetField("renderTargets",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(pipeline) is not Dictionary<string, Dictionary<System.Drawing.Size, (RenderTarget, DateTime)>> map)
        {
            Log("real-target reflection failed");
            return;
        }

        var camera = scene.MainCamera;
        var size = new System.Drawing.Size((int)camera.Width, (int)camera.Height);
        Log("== real frame targets ==");
        foreach (var (name, sizeMap) in map)
        {
            if (!sizeMap.TryGetValue(size, out var entry))
            {
                Log($"real {name}: no entry for {size}");
                continue;
            }

            var rt = entry.Item1;
            Native.glBindFramebuffer(GL_FRAMEBUFFER, rt.FrameBufferId);
            var status = Native.glCheckFramebufferStatus(GL_FRAMEBUFFER);
            Native.glGetError();
            var pf = new float[4];
            Native.glReadPixels((int)(camera.Width / 2), (int)(camera.Height / 2), 1, 1,
                GL_RGBA, GL_FLOAT, pf);
            var err = Native.glGetError();
            if (err != 0)
            {
                var pb = new byte[4];
                Native.glReadPixels((int)(camera.Width / 2), (int)(camera.Height / 2), 1, 1,
                    GL_RGBA, GL_UNSIGNED_BYTE, pb);
                Native.glGetError();
                Log($"real {name} fbo={rt.FrameBufferId} status=0x{status:X} px8=({pb[0]},{pb[1]},{pb[2]},{pb[3]})");
            }
            else
            {
                Log($"real {name} fbo={rt.FrameBufferId} status=0x{status:X} px=({pf[0]:F3},{pf[1]:F3},{pf[2]:F3},{pf[3]:F3})");
            }
        }

        Native.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
        Log("== real frame targets end ==");
    }

    private void DebugProbeCoreTextures(Scene scene)
    {
        var pipeline = scene.RenderPipeline;
        var field = typeof(RenderPipeline).GetField("renderTargets",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(pipeline) is not Dictionary<string, Dictionary<System.Drawing.Size, (RenderTarget, DateTime)>> map)
            return;

        var camera = scene.MainCamera;
        var size = new System.Drawing.Size((int)camera.Width, (int)camera.Height);
        if (!map.TryGetValue("GBuffer", out var gm) || !gm.TryGetValue(size, out var gentry))
        {
            Log("probe-core: GBuffer not found");
            return;
        }

        var gbuffer = gentry.Item1;
        var baseColorId = gbuffer.GetTexture("BaseColor")?.TextureId ?? 0;
        var depthId = gbuffer.DepthStencilTexture.TextureId;

        const int ps = 128;
        Native.glGenTextures(1, out var dstTex);
        Native.glBindTexture(GL_TEXTURE_2D, dstTex);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2800, 0x2601);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2801, 0x2601);
        Native.glTexImage2D(GL_TEXTURE_2D, 0, 0x8814, ps, ps, 0, GL_RGBA, 0x1406, IntPtr.Zero);
        Native.glGenFramebuffers(1, out var dstFbo);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, dstFbo);
        Native.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, dstTex, 0);

        Native.glGenVertexArrays(1, out var vao);
        Native.glBindVertexArray(vao);
        var prog = BuildProbeProgram();
        Native.glUseProgram(prog);
        Native.glUniform1i(Native.glGetUniformLocation(prog, "u_texture"), 0);
        Native.glActiveTexture(0x84C0);
        var quad = new float[] { -1, -1, 0, 0, 1, -1, 1, 0, -1, 1, 0, 1, 1, 1, 1, 1 };
        Native.glGenBuffers(1, out var vbo);
        Native.glBindBuffer(0x8892, vbo);
        Native.glBufferData(0x8892, (IntPtr)(quad.Length * 4), quad, 0x88E4);
        Native.glVertexAttribPointer(0, 2, 0x1406, false, 16, IntPtr.Zero);
        Native.glVertexAttribPointer(1, 2, 0x1406, false, 16, (IntPtr)8);
        Native.glEnableVertexAttribArray(0);
        Native.glEnableVertexAttribArray(1);
        Native.glViewport(0, 0, ps, ps);
        Native.glGetError();

        void Sample(string label, uint tex)
        {
            Native.glBindTexture(GL_TEXTURE_2D, tex);
            Native.glDrawArrays(0x0005, 0, 6);
            Native.glFinish();
            var err = Native.glGetError();
            var px = new float[4];
            Native.glReadPixels(ps / 2, ps / 2, 1, 1, GL_RGBA, GL_FLOAT, px);
            Native.glGetError();
            Log($"probe-core {label} tex={tex} err=0x{err:X} px=({px[0]:F3},{px[1]:F3},{px[2]:F3},{px[3]:F3})");
        }

        Sample("gbuffer-BaseColor", baseColorId);
        Sample("gbuffer-Depth", depthId);

        // 多 sampler 组合：逼近 IBL/Directional 的绑定
        var normalId = gbuffer.GetTexture("NormalRoughness")?.TextureId ?? 0;
        var metallicId = gbuffer.GetTexture("MetallicEmissive")?.TextureId ?? 0;
        var prog4 = BuildProbeProgram4();
        Native.glUseProgram(prog4);
        for (var i = 0; i < 4; i++)
            Native.glUniform1i(Native.glGetUniformLocation(prog4, $"u_tex{i}"), i);
        Native.glBindTexture(GL_TEXTURE_2D, baseColorId);
        Native.glActiveTexture(0x84C0);
        Native.glBindTexture(GL_TEXTURE_2D, baseColorId);
        Native.glActiveTexture(0x84C1);
        Native.glBindTexture(GL_TEXTURE_2D, normalId);
        Native.glActiveTexture(0x84C2);
        Native.glBindTexture(GL_TEXTURE_2D, metallicId);
        Native.glActiveTexture(0x84C3);
        Native.glBindTexture(GL_TEXTURE_2D, depthId);
        Native.glActiveTexture(0x84C0);
        Native.glDrawArrays(0x0005, 0, 6);
        Native.glFinish();
        var err4 = Native.glGetError();
        var px4 = new float[4];
        Native.glReadPixels(ps / 2, ps / 2, 1, 1, GL_RGBA, GL_FLOAT, px4);
        Native.glGetError();
        Log($"probe-core 4-sampler err=0x{err4:X} px=({px4[0]:F3},{px4[1]:F3},{px4[2]:F3},{px4[3]:F3})");
        Native.glActiveTexture(0x84C1);
        Native.glBindTexture(GL_TEXTURE_2D, 0);
        Native.glActiveTexture(0x84C2);
        Native.glBindTexture(GL_TEXTURE_2D, 0);
        Native.glActiveTexture(0x84C3);
        Native.glBindTexture(GL_TEXTURE_2D, 0);
        Native.glActiveTexture(0x84C0);

        Native.glBindVertexArray(0);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
    }

    private void DebugProbeRealIbl(Scene scene)
    {
        const string Tag = "probe-ibl";

        // 1) 取 Core 真实 IBL 片元 shader（内嵌资源），做同款 defines 替换
        var asm = typeof(PBRDeferredPipeline).Assembly;
        var resType = asm.GetType("Aura3D.Pipeline.PBR.PbrResources");
        var fsSrc = resType?
            .GetProperty("IblAmbientFragmentShader",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?.GetValue(null) as string;
        if (fsSrc == null)
        {
            Log($"{Tag}: IblAmbientFragmentShader not found");
            return;
        }

        fsSrc = fsSrc.Replace("//{{defines}}", "#define ENBALE_DEFERRED_SHADING");

        // IBLAmbientPass 内联 VS 的逐字拷贝
        const string vsSrc = @"#version 300 es
precision highp float;

layout(location = 0) in vec3 a_position;
layout(location = 1) in vec2 a_texCoord;

out vec2 v_texCoord;
out vec4 v_clipPos;

void main() {
    gl_Position = vec4(a_position, 1.0);
    v_texCoord = a_texCoord;
    v_clipPos = gl_Position;
}";

        var vs = CompileShaderChecked(Tag + " vs", 0x8B31, vsSrc);
        var fs = CompileShaderChecked(Tag + " fs", 0x8B30, fsSrc);
        if (vs == 0 || fs == 0)
            return;

        var prog = Native.glCreateProgram();
        Native.glAttachShader(prog, vs);
        Native.glAttachShader(prog, fs);
        Native.glLinkProgram(prog);
        Native.glGetProgramiv(prog, 0x8B82, out var linkStatus);
        Native.glGetProgramiv(prog, 0x8B84, out var pLogLen);
        var pLog = new System.Text.StringBuilder(pLogLen + 2);
        Native.glGetProgramInfoLog(prog, pLogLen + 1, IntPtr.Zero, pLog);
        Native.glDeleteShader(vs);
        Native.glDeleteShader(fs);
        Log($"{Tag} link={linkStatus} log={pLog}");
        if (linkStatus == 0)
            return;

        // 2) 收集与真实 pass 相同的一整套纹理
        var pipeline = scene.RenderPipeline;
        var field = typeof(RenderPipeline).GetField("renderTargets",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(pipeline) is not Dictionary<string, Dictionary<System.Drawing.Size, (RenderTarget, DateTime)>> map)
            return;

        var camera = scene.MainCamera;
        var size = new System.Drawing.Size((int)camera.Width, (int)camera.Height);
        if (!map.TryGetValue("GBuffer", out var gm) || !gm.TryGetValue(size, out var gentry))
        {
            Log($"{Tag}: GBuffer not found");
            return;
        }

        var gbuffer = gentry.Item1;
        uint Id(Aura3D.Core.Resources.Texture? t) => t == null ? 0 : pipeline.EnsureSynced(t).TextureId;
        var baseColorId = Id(gbuffer.GetTexture("BaseColor"));
        var normalId = Id(gbuffer.GetTexture("NormalRoughness"));
        var metallicId = Id(gbuffer.GetTexture("MetallicEmissive"));
        var depthId = gbuffer.DepthStencilTexture.TextureId;
        var irradianceId = camera.GetPipelineGpuState<CubeRenderTarget>("IrradianceMap")?.GetTexture(0)?.TextureId ?? 0;
        var prefilterId = camera.GetPipelineGpuState<CubeRenderTarget>("PrefilteredEnvironmentMap")?.GetTexture(0)?.TextureId ?? 0;
        var brdfTex = pipeline.GetType().GetProperty("BrdfLutTexture")?.GetValue(pipeline) as Aura3D.Core.Resources.Texture;
        var brdfId = brdfTex == null ? 0 : pipeline.EnsureSynced(brdfTex).TextureId;

        Log($"{Tag} ids bc={baseColorId} nr={normalId} me={metallicId} d={depthId} brdf={brdfId} irr={irradianceId} pre={prefilterId}");

        // 3) InternalQuad 的逐字复制（含 VAO/EBO），输出到独立 128² RGBA32F
        const int ps = 128;
        Native.glGenTextures(1, out var dstTex);
        Native.glBindTexture(GL_TEXTURE_2D, dstTex);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2800, 0x2601);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2801, 0x2601);
        Native.glTexImage2D(GL_TEXTURE_2D, 0, 0x8814, ps, ps, 0, GL_RGBA, GL_FLOAT, IntPtr.Zero);
        Native.glGenFramebuffers(1, out var dstFbo);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, dstFbo);
        Native.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, dstTex, 0);
        Native.glViewport(0, 0, ps, ps);
        Native.glDisable(0x0B71); // DEPTH_TEST
        Native.glDisable(0x0BE2); // BLEND
        Native.glDisable(0x0B44); // CULL_FACE
        Native.glClearColor(0, 0, 0, 0);
        Native.glClear(0x4000);

        Native.glGenVertexArrays(1, out var vao);
        Native.glBindVertexArray(vao);
        var quadVerts = new float[]
        {
            -1, 1, 0, 0, 1,
            -1, -1, 0, 0, 0,
            1, -1, 0, 1, 0,
            1, 1, 0, 1, 1,
        };
        Native.glGenBuffers(1, out var vbo);
        Native.glBindBuffer(0x8892, vbo);
        Native.glBufferData(0x8892, (IntPtr)(quadVerts.Length * 4), quadVerts, 0x88E4);
        Native.glGenBuffers(1, out var ebo);
        Native.glBindBuffer(0x8893, ebo);
        Native.glBufferData(0x8893, (IntPtr)24, new uint[] { 0, 1, 2, 2, 3, 0 }, 0x88E4);
        Native.glVertexAttribPointer(0, 3, GL_FLOAT, false, 20, IntPtr.Zero);
        Native.glVertexAttribPointer(1, 2, GL_FLOAT, false, 20, (IntPtr)12);
        Native.glEnableVertexAttribArray(0);
        Native.glEnableVertexAttribArray(1);

        Native.glUseProgram(prog);

        void Bind2D(int unit, string name, uint id)
        {
            Native.glUniform1i(Native.glGetUniformLocation(prog, name), unit);
            Native.glActiveTexture(0x84C0 + unit);
            Native.glBindTexture(GL_TEXTURE_2D, id);
        }

        void BindCube(int unit, string name, uint id)
        {
            Native.glUniform1i(Native.glGetUniformLocation(prog, name), unit);
            Native.glActiveTexture(0x84C0 + unit);
            Native.glBindTexture(0x8513, id);
        }

        // 与 IBLAmbientPass.Render 相同的 unit 顺序
        Bind2D(0, "gBufferBaseColor", baseColorId);
        Bind2D(1, "gBufferNormalRoughness", normalId);
        Bind2D(2, "gBufferMetallicEmissive", metallicId);
        Bind2D(3, "depthTexture", depthId);
        Bind2D(4, "u_brdfLUT", brdfId);
        BindCube(5, "u_irradianceMap", irradianceId);
        BindCube(6, "u_prefilterMap", prefilterId);

        var view = camera.View;
        var proj = camera.Projection;
        System.Numerics.Matrix4x4.Invert(view * proj, out var invViewProj);
        Native.glUniformMatrix4(Native.glGetUniformLocation(prog, "u_viewMatrix"), 1, false, Mat(view));
        Native.glUniformMatrix4(Native.glGetUniformLocation(prog, "u_projMatrix"), 1, false, Mat(proj));
        Native.glUniformMatrix4(Native.glGetUniformLocation(prog, "u_invViewProjMatrix"), 1, false, Mat(invViewProj));
        var camPos = camera.WorldTransform.Translation;
        Native.glUniform3fv(Native.glGetUniformLocation(prog, "u_cameraPos"), 1, new[] { camPos.X, camPos.Y, camPos.Z });
        Native.glUniform1f(Native.glGetUniformLocation(prog, "iblAmbientIntensity"), 1f);
        Native.glUniform1f(Native.glGetUniformLocation(prog, "u_max_mipmap"), 5f);

        // 逐字复刻 IBLAmbientPass.BeforeRender 的状态：blend ONE/ONE（与探针原本 blend-off 的唯一差异之一）
        Native.glEnable(0x0BE2); // BLEND
        Native.glBlendFunc(0x0201, 0x0201); // ONE, ONE
        Native.glGetError();
        Native.glDrawElements(0x0004, 6, 0x1405, IntPtr.Zero);
        Native.glFinish();
        var err = Native.glGetError();

        var px = new float[4];
        Native.glReadPixels(ps / 2, ps / 2, 1, 1, GL_RGBA, GL_FLOAT, px);
        Native.glGetError();
        Log($"{Tag} draw err=0x{err:X} center=({px[0]:F3},{px[1]:F3},{px[2]:F3},{px[3]:F3})");

        // 变体矩阵：定位 0x502 的确切触发条件
        void Variant(string label, bool blend, int sfactor, int dfactor)
        {
            Native.glGetError();
            if (blend)
            {
                Native.glEnable(0x0BE2);
                Native.glBlendFunc(sfactor, dfactor);
            }
            else Native.glDisable(0x0BE2);
            Native.glClearColor(0, 0, 0, 0);
            Native.glClear(0x4000);
            Native.glDrawElements(0x0004, 6, 0x1405, IntPtr.Zero);
            Native.glFinish();
            var e = Native.glGetError();
            var p = new float[4];
            Native.glReadPixels(ps / 2, ps / 2, 1, 1, GL_RGBA, GL_FLOAT, p);
            Native.glGetError();
            Log($"{Tag} variant {label} err=0x{e:X} px=({p[0]:F3},{p[1]:F3},{p[2]:F3},{p[3]:F3})");
        }

        Native.glBindFramebuffer(GL_FRAMEBUFFER, dstFbo);
        Native.glViewport(0, 0, ps, ps);
        Variant("no-blend", false, 0, 0);
        Variant("blend-src-alpha", true, 0x0302, 0x0303); // SRC_ALPHA, ONE_MINUS_SRC_ALPHA
        Variant("blend-one-one-float", true, 0x0201, 0x0201);

        // RGBA8 目标 + ONE/ONE：区分“混合本身”还是“float 目标上的非规则混合”
        Native.glGenTextures(1, out var dst8Tex);
        Native.glBindTexture(GL_TEXTURE_2D, dst8Tex);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2800, 0x2601);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2801, 0x2601);
        Native.glTexImage2D(GL_TEXTURE_2D, 0, 0x8058, ps, ps, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero); // RGBA8
        Native.glGenFramebuffers(1, out var dst8Fbo);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, dst8Fbo);
        Native.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, dst8Tex, 0);
        Native.glGetError();
        Native.glClearColor(0, 0, 0, 0);
        Native.glClear(0x4000);
        Native.glDrawElements(0x0004, 6, 0x1405, IntPtr.Zero);
        Native.glFinish();
        var e8 = Native.glGetError();
        var p8 = new float[4];
        Native.glReadPixels(ps / 2, ps / 2, 1, 1, GL_RGBA, GL_FLOAT, p8);
        Native.glGetError();
        Log($"{Tag} variant blend-one-one-rgba8tgt err=0x{e8:X} px=({p8[0]:F3},{p8[1]:F3},{p8[2]:F3},{p8[3]:F3})");
        Native.glGetError();
        var b8 = new byte[4];
        Native.glReadPixels(ps / 2, ps / 2, 1, 1, GL_RGBA, GL_UNSIGNED_BYTE, b8);
        Native.glGetError();
        Log($"{Tag} variant blend-one-one-rgba8tgt byte=({b8[0]},{b8[1]},{b8[2]},{b8[3]})");

        // RGBA16F 目标 + ONE/ONE：验证 half-float 是否同样被拒（迁移方案备选：HDR 降为 16F）
        Native.glGenTextures(1, out var dstHTex);
        Native.glBindTexture(GL_TEXTURE_2D, dstHTex);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2800, 0x2601);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2801, 0x2601);
        Native.glTexImage2D(GL_TEXTURE_2D, 0, 0x881A, ps, ps, 0, GL_RGBA, 0x140B, IntPtr.Zero); // RGBA16F/HALF_FLOAT
        Native.glGenFramebuffers(1, out var dstHFbo);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, dstHFbo);
        Native.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, dstHTex, 0);
        var hStatus = Native.glCheckFramebufferStatus(GL_FRAMEBUFFER);
        Native.glGetError();
        Native.glClearColor(0, 0, 0, 0);
        Native.glClear(0x4000);
        Native.glDrawElements(0x0004, 6, 0x1405, IntPtr.Zero);
        Native.glFinish();
        var eh = Native.glGetError();
        var ph = new float[4];
        Native.glReadPixels(ps / 2, ps / 2, 1, 1, GL_RGBA, GL_FLOAT, ph);
        Native.glGetError();
        Log($"{Tag} variant blend-one-one-rgba16f status=0x{hStatus:X} err=0x{eh:X} px=({ph[0]:F3},{ph[1]:F3},{ph[2]:F3},{ph[3]:F3})");
        Native.glBindFramebuffer(GL_FRAMEBUFFER, dstFbo);

        // 第二步：完全在真实 BaseRenderTarget（含其深度附件）+ 真实 viewport 上重放同一次 draw
        var size2 = new System.Drawing.Size((int)camera.Width, (int)camera.Height);
        if (FindRenderTarget(pipeline, size2, "BaseRenderTarget") is { } baseRT)
        {
            Native.glDisable(0x0B71); // DepthTest（与 IBL BeforeRender 一致）
            Native.glBindFramebuffer(GL_FRAMEBUFFER, baseRT.FrameBufferId);
            Native.glViewport(0, 0, size2.Width, size2.Height);
            Native.glClearColor(0, 0, 0, 0);
            Native.glClear(0x4000);
            Native.glGetError();
            Native.glDrawElements(0x0004, 6, 0x1405, IntPtr.Zero);
            Native.glFinish();
            var err2 = Native.glGetError();
            var px2 = new float[4];
            Native.glReadPixels(size2.Width / 2, size2.Height / 2, 1, 1, GL_RGBA, GL_FLOAT, px2);
            Native.glGetError();
            Log($"{Tag} on-real-BaseRT fbo={baseRT.FrameBufferId} err=0x{err2:X} center=({px2[0]:F3},{px2[1]:F3},{px2[2]:F3},{px2[3]:F3})");
        }

        Native.glDisable(0x0BE2); // 还原 blend 状态

        Native.glBindVertexArray(0);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
    }

    private static float[] Mat(System.Numerics.Matrix4x4 m) => new[]
    {
        m.M11, m.M12, m.M13, m.M14,
        m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34,
        m.M41, m.M42, m.M43, m.M44,
    };

    private uint CompileShaderChecked(string label, int type, string source)
    {
        var s = Native.glCreateShader(type);
        Native.glShaderSource(s, 1, new[] { source }, IntPtr.Zero);
        Native.glCompileShader(s);
        Native.glGetShaderiv(s, 0x8B81, out var status);
        Native.glGetShaderiv(s, 0x8B84, out var logLen);
        var log = new System.Text.StringBuilder(logLen + 2);
        Native.glGetShaderInfoLog(s, logLen + 1, IntPtr.Zero, log);
        if (status == 0)
            Log($"{label} COMPILE FAILED log={log} src0={(source.Length > 80 ? source[..80] : source)}");
        else if (logLen > 0)
            Log($"{label} compile log={log}");
        return status == 0 ? 0 : s;
    }

    private void DebugDepthSamplerProbe()
    {
        const int size = 128;
        Native.glGenTextures(1, out var colorTex);
        Native.glBindTexture(GL_TEXTURE_2D, colorTex);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2800, 0x2601); // MIN_FILTER LINEAR
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2801, 0x2601); // MAG_FILTER LINEAR
        Native.glTexImage2D(GL_TEXTURE_2D, 0, 0x8814, size, size, 0, GL_RGBA, 0x1406, IntPtr.Zero); // RGBA32F
        Native.glGenTextures(1, out var depthTex);
        Native.glBindTexture(GL_TEXTURE_2D, depthTex);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2800, 0x2600); // NEAREST
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2801, 0x2600);
        Native.glTexImage2D(GL_TEXTURE_2D, 0, 0x81A6, size, size, 0, 0x1902, 0x1405, IntPtr.Zero); // DEPTH_COMPONENT24/UNSIGNED_INT
        Native.glGenFramebuffers(1, out var srcFbo);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, srcFbo);
        Native.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, colorTex, 0);
        Native.glFramebufferTexture2D(GL_FRAMEBUFFER, 0x8D00, GL_TEXTURE_2D, depthTex, 0); // DEPTH_ATTACHMENT
        var srcStatus = Native.glCheckFramebufferStatus(GL_FRAMEBUFFER);
        Native.glGenFramebuffers(1, out var dstFbo);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, dstFbo);
        Native.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, colorTex == 0 ? 0 : ReuseAsDst(), 0);
        Native.glGetError();

        var prog = BuildProbeProgram();
        Native.glGenVertexArrays(1, out var vao);
        Native.glBindVertexArray(vao);
        Native.glUseProgram(prog);
        Native.glUniform1i(Native.glGetUniformLocation(prog, "u_texture"), 0);
        Native.glActiveTexture(0x84C0); // TEXTURE0

        var quad = new float[] { -1, -1, 0, 0, 1, -1, 1, 0, -1, 1, 0, 1, 1, 1, 1, 1 };
        Native.glGenBuffers(1, out var vbo);
        Native.glBindBuffer(0x8892, vbo); // ARRAY_BUFFER
        Native.glBufferData(0x8892, (IntPtr)(quad.Length * 4), quad, 0x88E4); // STATIC_DRAW
        Native.glVertexAttribPointer(0, 2, 0x1406, false, 16, IntPtr.Zero);
        Native.glVertexAttribPointer(1, 2, 0x1406, false, 16, (IntPtr)8);
        Native.glEnableVertexAttribArray(0);
        Native.glEnableVertexAttribArray(1);

        Native.glBindFramebuffer(GL_FRAMEBUFFER, dstFbo);
        Native.glViewport(0, 0, size, size);

        // A: 采样普通 RGBA32F 颜色纹理
        Native.glBindTexture(GL_TEXTURE_2D, colorTex);
        Native.glDrawArrays(0x0005, 0, 6); // TRIANGLE_STRIP
        Log($"probe A sample color err=0x{Native.glGetError():X}");

        // B: 采样从未渲染过的 DEPTH_COMPONENT24 纹理
        Native.glBindTexture(GL_TEXTURE_2D, depthTex);
        Native.glDrawArrays(0x0005, 0, 6);
        Log($"probe B sample fresh depth24 err=0x{Native.glGetError():X}");

        // C: 先往 srcFbo 清深度+画一次，再采样
        Native.glBindFramebuffer(GL_FRAMEBUFFER, srcFbo);
        Native.glClearColor(0, 0, 0, 0);
        Native.glClear(0x4100); // COLOR|DEPTH
        Native.glDrawArrays(0x0005, 0, 6);
        Native.glFinish();
        Native.glBindFramebuffer(GL_FRAMEBUFFER, dstFbo);
        Native.glBindTexture(GL_TEXTURE_2D, depthTex);
        Native.glDrawArrays(0x0005, 0, 6);
        var errC = Native.glGetError();
        var px = new float[4];
        Native.glReadPixels(size / 2, size / 2, 1, 1, GL_RGBA, GL_FLOAT, px);
        Log($"probe C sample drawn-into depth24 err=0x{errC:X} px=({px[0]:F3},{px[1]:F3},{px[2]:F3},{px[3]:F3})");

        Native.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
    }

    private uint ReuseAsDst()
    {
        Native.glGenTextures(1, out var t);
        Native.glBindTexture(GL_TEXTURE_2D, t);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2800, 0x2601);
        Native.glTexParameteri(GL_TEXTURE_2D, 0x2801, 0x2601);
        Native.glTexImage2D(GL_TEXTURE_2D, 0, 0x8814, 128, 128, 0, GL_RGBA, 0x1406, IntPtr.Zero);
        return t;
    }

    private uint BuildProbeProgram()
    {
        const string vs = "#version 300 es\nlayout(location=0) in vec2 p; layout(location=1) in vec2 t; out vec2 v; void main(){ gl_Position=vec4(p,0,1); v=t; }";
        const string fs = "#version 300 es\nprecision highp float; precision highp sampler2D; in vec2 v; uniform sampler2D u_texture; out vec4 o; void main(){ o=texture(u_texture,v); }";

        var v = CompileShader(0x8B31, vs);
        var f = CompileShader(0x8B30, fs);
        var prog = Native.glCreateProgram();
        Native.glAttachShader(prog, v);
        Native.glAttachShader(prog, f);
        Native.glLinkProgram(prog);
        Native.glDeleteShader(v);
        Native.glDeleteShader(f);
        return prog;
    }

    private uint BuildProbeProgram4()
    {
        const string vs = "#version 300 es\nlayout(location=0) in vec2 p; layout(location=1) in vec2 t; out vec2 v; void main(){ gl_Position=vec4(p,0,1); v=t; }";
        const string fs = "#version 300 es\nprecision highp float; precision highp sampler2D; in vec2 v; uniform sampler2D u_tex0; uniform sampler2D u_tex1; uniform sampler2D u_tex2; uniform sampler2D u_tex3; out vec4 o; void main(){ o=texture(u_tex0,v)+texture(u_tex1,v)+texture(u_tex2,v)+texture(u_tex3,v); }";

        var v = CompileShader(0x8B31, vs);
        var f = CompileShader(0x8B30, fs);
        var prog = Native.glCreateProgram();
        Native.glAttachShader(prog, v);
        Native.glAttachShader(prog, f);
        Native.glLinkProgram(prog);
        Native.glDeleteShader(v);
        Native.glDeleteShader(f);
        return prog;
    }

    private static uint CompileShader(int type, string source)
    {
        var s = Native.glCreateShader(type);
        Native.glShaderSource(s, 1, new[] { source }, IntPtr.Zero);
        Native.glCompileShader(s);
        return s;
    }

    private void DebugReplayPasses(Scene scene)
    {
        var pipeline = scene.RenderPipeline;
        var camera = scene.MainCamera;
        Log("== pass replay begin ==");
        foreach (var pass in pipeline.OnceRenderPasses)
        {
            pass.BeforeRender();
            pass.Render();
            pass.AfterRender();
            ReadBoundPass($"once:{pass.GetType().Name}", camera);
        }

        foreach (var pass in pipeline.EveryCameraRenderPasses)
        {
            pipeline.BeforeCameraRender(camera);
            pass.BeforeRender(camera);
            pass.Render(camera);
            pass.AfterRender(camera);
            ReadBoundPass($"cam:{pass.GetType().Name}", camera);

            if (pass.GetType().Name == "BasePass")
                ReadGBufferAttachments(camera);
            if (pass.GetType().Name is "IBLAmbientPass" or "DirectionalLightingPass")
                ReadScanline($"{pass.GetType().Name} output", camera);
        }

        Log("== pass replay end ==");
    }

    private void ReadGBufferAttachments(Aura3D.Core.Nodes.Camera camera)
    {
        var x = (int)(camera.Width / 2);
        var y = (int)(camera.Height / 2);
        while (Native.glGetError() != 0)
        {
        }

        for (var i = 0; i < 3; i++)
        {
            Native.glReadBuffer(0x8CE0 + i);
            var pb = new byte[4];
            Native.glReadPixels(x, y, 1, 1, GL_RGBA, GL_UNSIGNED_BYTE, pb);
            var err = Native.glGetError();
            Log($"gbuffer attach{i} px8=({pb[0]},{pb[1]},{pb[2]},{pb[3]}) err=0x{err:X}");
        }

        Native.glReadBuffer(0x8CE0);
        while (Native.glGetError() != 0)
        {
        }

        var d = new float[1];
        Native.glReadPixels(x, y, 1, 1, GL_DEPTH_COMPONENT, GL_FLOAT, d);
        Log($"gbuffer depth={d[0]:F5} err=0x{Native.glGetError():X}");
    }

    private void ReadScanline(string name, Aura3D.Core.Nodes.Camera camera)
    {
        var x = (int)(camera.Width / 2);
        Native.glGetIntegerv(GL_DRAW_FRAMEBUFFER_BINDING, out var fbo);
        var span = (int)(camera.Width / 2);
        var samples = new float[span * 4];
        Native.glReadPixels(x - span / 2, (int)(camera.Height / 2),
            span, 1, GL_RGBA, GL_FLOAT, samples);
        var err = Native.glGetError();
        if (err != 0)
        {
            Log($"scanline {name} fbo={fbo} err=0x{err:X}");
            return;
        }

        var maxR = 0f;
        var maxG = 0f;
        var maxB = 0f;
        var maxA = 0f;
        for (var i = 0; i < samples.Length / 4; i++)
        {
            maxR = Math.Max(maxR, samples[i * 4]);
            maxG = Math.Max(maxG, samples[i * 4 + 1]);
            maxB = Math.Max(maxB, samples[i * 4 + 2]);
            maxA = Math.Max(maxA, samples[i * 4 + 3]);
        }

        Log($"scanline {name} fbo={fbo} maxRGB=({maxR:F3},{maxG:F3},{maxB:F3}) maxA={maxA:F3}");
    }

    private void ReadBoundPass(string name, Aura3D.Core.Nodes.Camera camera)
    {
        Native.glFinish();
        Native.glGetIntegerv(GL_DRAW_FRAMEBUFFER_BINDING, out var fbo);
        var x = (int)(camera.Width / 2);
        var y = (int)(camera.Height / 2);

        var drained = "";
        int derr;
        while ((derr = Native.glGetError()) != 0)
            drained += $"0x{derr:X} ";
        if (drained.Length > 0)
            Log($"pending errors after {name}: {drained}");

        var pf = new float[4];
        Native.glReadPixels(x, y, 1, 1, GL_RGBA, GL_FLOAT, pf);
        var err = Native.glGetError();
        if (err == 0)
        {
            Log($"replay {name} fbo={fbo} px=({pf[0]:F3},{pf[1]:F3},{pf[2]:F3},{pf[3]:F3})");
            return;
        }

        var px = new byte[4];
        Native.glReadPixels(x, y, 1, 1, GL_RGBA, GL_UNSIGNED_BYTE, px);
        var err2 = Native.glGetError();
        Log($"replay {name} fbo={fbo} px8=({px[0]},{px[1]},{px[2]},{px[3]}) err=0x{err2:X}");

        var d = new float[1];
        Native.glReadPixels(x, y, 1, 1, GL_DEPTH_COMPONENT, GL_FLOAT, d);
        var err3 = Native.glGetError();
        if (err3 == 0)
            Log($"replay {name} depth={d[0]:F4}");
    }

    private void Compose(ISkiaSharpApiLeaseFeature feature)
    {
        using var lease = feature.Lease();
        var gr = lease.GrContext;
        if (gr is null || lease.SkCanvas is null)
        {
            Fail("lease empty");
            return;
        }

        var info = new GRMtlTextureInfo(_texture!.Handle);
        using var backend = new GRBackendTexture((int)_pixelW, (int)_pixelH, false, info);
        using var image = SKImage.FromTexture(gr, backend, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888);
        if (image is null)
        {
            Fail("image null");
            return;
        }

        var canvas = lease.SkCanvas;
        canvas.Save();
        if (FlipVertical)
        {
            canvas.Translate(0, (float)Bounds.Height);
            canvas.Scale(1, -1);
        }
        canvas.DrawImage(image, new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));
        canvas.Restore();
    }

    private void EnsureScene()
    {
        Log($"version='{PtrToUtf8(Native.glGetString(GL_VERSION))}' renderer='{PtrToUtf8(Native.glGetString(GL_RENDERER))}'");

        var range = new float[2];
        Native.glGetFloatv(GL_ALIASED_POINT_SIZE_RANGE, range);
        Native.glGetIntegerv(GL_MAX_TEXTURE_SIZE, out var maxTex);
        while (Native.glGetError() != 0)
        {
        }
        Log($"F9 pointSizeRange=[{range[0]},{range[1]}] maxTextureSize={maxTex}");

        var exts = PtrToUtf8(Native.glGetString(0x1F03)) ?? "";
        Log($"KHR_debug in exts: {exts.Contains("GL_KHR_debug")}, extCount={exts.Split(' ').Length}");
        Log($"float_blend ext: {exts.Contains("GL_EXT_float_blend")}");
        Log($"gl version: {PtrToUtf8(Native.glGetString(GL_VERSION))}");
        foreach (var token in exts.Split(' '))
            if (token.Contains("blend", StringComparison.OrdinalIgnoreCase) || token.Contains("float", StringComparison.OrdinalIgnoreCase))
                Log($"ext sample: {token}");

        var scene = new Scene(CreateRenderPipeline, PipelineSettings, _renderSurface);
        scene.RenderPipeline.Initialize(GetProc);
        SceneConfigured?.Invoke(scene);
        _scene = scene;
        _stopwatch.Restart();
        Log("scene ready");
    }

    /// <summary>按控件当前像素尺寸（重）建输出 MTLTexture → EGLImage → GL 纹理 → FBO。</summary>
    private bool EnsureOutput()
    {
        var source = this.GetPresentationSource();
        float scale = source != null ? (float)source.RenderScaling : 1f;
        uint w = (uint)Math.Max(1, (int)(Bounds.Width * scale));
        uint h = (uint)Math.Max(1, (int)(Bounds.Height * scale));

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return false;
        if (_texture != null && w == _pixelW && h == _pixelH)
            return true;

        if (!Native.eglMakeCurrent(_display, _surface, _surface, _context))
        {
            Fail($"makecurrent 0x{Native.eglGetError():X}");
            return false;
        }

        if (_frame > 0)
            Native.glFinish(); // 等旧纹理上的渲染结束再删除

        if (_fbo != 0)
            Native.glDeleteFramebuffers(1, ref _fbo);
        if (_glTexture != 0)
            Native.glDeleteTextures(1, ref _glTexture);
        if (_eglImage != IntPtr.Zero)
            Native.eglDestroyImageKHR(_display, _eglImage);
        _texture?.Dispose();
        _fbo = 0;
        _glTexture = 0;
        _eglImage = IntPtr.Zero;
        _texture = null;

        var device = MTLDevice.SystemDefault;
        if (device is null)
        {
            Fail("no MTLDevice");
            return false;
        }

        var descriptor = new MTLTextureDescriptor
        {
            TextureType = MTLTextureType.k2D,
            Width = (nuint)w,
            Height = (nuint)h,
            MipmapLevelCount = 1,
            PixelFormat = MTLPixelFormat.RGBA8Unorm,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared,
        };
        _texture = device.CreateTexture(descriptor);
        if (_texture is null)
        {
            Fail("CreateTexture null");
            return false;
        }

        _eglImage = Native.eglCreateImageKHR(_display, IntPtr.Zero, EGL_METAL_TEXTURE_ANGLE, _texture.Handle, null);
        if (_eglImage == IntPtr.Zero)
        {
            Fail($"createImage 0x{Native.eglGetError():X}");
            return false;
        }

        Native.glGenTextures(1, out _glTexture);
        Native.glBindTexture(GL_TEXTURE_2D, _glTexture);
        Native.glEGLImageTargetTexture2DOES(GL_TEXTURE_2D, _eglImage);
        var glErr = Native.glGetError();
        if (glErr != 0)
        {
            Fail($"imageTarget 0x{glErr:X}");
            return false;
        }

        Native.glGenFramebuffers(1, out _fbo);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
        Native.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, _glTexture, 0);
        var status = Native.glCheckFramebufferStatus(GL_FRAMEBUFFER);
        if (status != GL_FRAMEBUFFER_COMPLETE)
        {
            Fail($"fbo incomplete 0x{status:X}");
            return false;
        }

        _pixelW = w;
        _pixelH = h;
        _renderSurface.FrameBufferId = _fbo;
        _renderSurface.Width = w;
        _renderSurface.Height = h;
        _renderSurface.Scale = scale;
        Log($"output {w}x{h} scale={scale} fbo={_fbo}");
        return true;
    }

    private bool InitEgl()
    {
        _display = Native.eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, IntPtr.Zero,
            new[] { EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_METAL_ANGLE, EGL_NONE });
        if (_display == IntPtr.Zero)
        {
            Fail("no ANGLE Metal display");
            return false;
        }

        if (!Native.eglInitialize(_display, out var maj, out var min) || maj < 1)
        {
            Fail($"eglInitialize 0x{Native.eglGetError():X}");
            return false;
        }

        var clientExts = PtrToUtf8(Native.eglQueryString(_display, EGL_EXTENSIONS)) ?? "";
        if (!clientExts.Contains("EGL_KHR_image_base") ||
            !clientExts.Contains("EGL_ANGLE_metal_texture_client_buffer"))
        {
            Fail("missing EGL ext");
            return false;
        }

        Native.eglBindAPI(EGL_OPENGL_ES_API);

        int[] configAttribs =
        {
            EGL_SURFACE_TYPE, EGL_PBUFFER_BIT,
            EGL_RENDERABLE_TYPE, EGL_OPENGL_ES3_BIT,
            EGL_RED_SIZE, 8, EGL_GREEN_SIZE, 8, EGL_BLUE_SIZE, 8, EGL_ALPHA_SIZE, 8,
            EGL_NONE,
        };
        if (!Native.eglChooseConfig(_display, configAttribs, out _config, 1, out var n) || n < 1)
        {
            Fail($"chooseConfig 0x{Native.eglGetError():X}");
            return false;
        }

        var ctxAttribs = Environment.GetEnvironmentVariable("AURA_ES32") == "1"
            ? new[] { EGL_CONTEXT_CLIENT_VERSION, 3, 0x30FB, 1, EGL_NONE } // EGL_CONTEXT_MINOR_VERSION_KHR = 2 → ES 3.2
            : new[] { EGL_CONTEXT_CLIENT_VERSION, 3, EGL_NONE };
        _context = Native.eglCreateContext(_display, _config, IntPtr.Zero, ctxAttribs);
        if (_context == IntPtr.Zero)
        {
            Fail($"createContext 0x{Native.eglGetError():X}");
            return false;
        }

        _surface = Native.eglCreatePbufferSurface(_display, _config,
            new[] { EGL_WIDTH, 1, EGL_HEIGHT, 1, EGL_NONE });
        if (_surface == IntPtr.Zero ||
            !Native.eglMakeCurrent(_display, _surface, _surface, _context))
        {
            Fail($"surface/makecurrent 0x{Native.eglGetError():X}");
            return false;
        }

        Log($"egl {maj}.{min} ok");

        if (Environment.GetEnvironmentVariable("AURA_DEBUG_REPLAY") == "1")
        {
            unsafe
            {
                Native.glDebugMessageCallback(
                    (nint)(delegate* unmanaged[Cdecl]<int, int, uint, int, int, IntPtr, IntPtr, void>)&OnGlDebugMessage,
                    IntPtr.Zero);
            }
            Native.glEnable(0x8248); // GL_DEBUG_OUTPUT
            var enableErr = Native.glGetError();
            Native.glEnable(0x8249); // GL_DEBUG_OUTPUT_SYNCHRONOUS
            Native.glGetError();
            Native.glDebugMessageControl(0x1111, 0x1111, 0x1111, 0, IntPtr.Zero, 1);
            while (Native.glGetError() != 0)
            {
            }

            Log($"GL_KHR_debug enabled (enableErr=0x{enableErr:X})");
            Native.glDebugMessageInsert(0x824A, 0x8251, 1, 0x914B, 5, "hello", IntPtr.Zero);
            Log($"insert err=0x{Native.glGetError():X}");
        }

        return true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnGlDebugMessage(int source, int type, uint id, int severity, int length,
        IntPtr message, IntPtr userParam)
    {
        Console.WriteLine("[angle-gldebug] " + (Marshal.PtrToStringAnsi(message, length) ?? ""));
    }

    private static IntPtr GetProc(string name)
    {
        var p = Native.eglGetProcAddress(name);
        if (p == IntPtr.Zero || p == new IntPtr(-1))
            p = Native.dlsym(new IntPtr(-2), name); // RTLD_DEFAULT，覆盖 ANGLE 直接导出的核心符号
        return p;
    }

    private static string? PtrToUtf8(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

    private void Fail(string message)
    {
        _failed = true;
        Log("FAIL " + message);
    }

    /// <summary>绘制回调在渲染线程，写 UI 属性必须切回 UI 线程。</summary>
    private void Log(string message)
    {
        Console.WriteLine("[angle-host] " + message);
        var status = Status;
        if (status is null)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            _lines.Enqueue(message);
            while (_lines.Count > 8)
            {
                _lines.Dequeue();
            }

            status.Text = string.Join(Environment.NewLine, _lines);
        });
    }

    private static class Native
    {
        private const string Lib = "__Internal";

        [DllImport(Lib)] public static extern IntPtr eglGetDisplay(IntPtr displayId);

        [DllImport(Lib)] public static extern bool eglInitialize(IntPtr dpy, out int major, out int minor);

        [DllImport(Lib)] public static extern int eglGetError();

        [DllImport(Lib)] public static extern IntPtr eglQueryString(IntPtr dpy, int name);

        [DllImport(Lib)] public static extern bool eglBindAPI(int api);

        [DllImport(Lib)]
        public static extern bool eglChooseConfig(IntPtr dpy, int[] attribList, out IntPtr config,
            int bufSize, out int n);

        [DllImport(Lib)]
        public static extern IntPtr eglCreateContext(IntPtr dpy, IntPtr config, IntPtr shareCtx,
            int[] attribList);

        [DllImport(Lib)]
        public static extern IntPtr eglCreatePbufferSurface(IntPtr dpy, IntPtr config, int[] attribList);

        [DllImport(Lib)] public static extern bool eglMakeCurrent(IntPtr dpy, IntPtr draw, IntPtr read, IntPtr ctx);

        [DllImport(Lib)] public static extern IntPtr eglGetProcAddress(string procName);

        [DllImport(Lib)] public static extern IntPtr eglGetPlatformDisplayEXT(int platform,
            IntPtr nativeDisplay, int[] attribList);

        [DllImport(Lib)] public static extern IntPtr eglCreateImageKHR(IntPtr dpy, IntPtr ctx, int target,
            IntPtr buffer, int[]? attribList);

        [DllImport(Lib)] public static extern bool eglDestroyImageKHR(IntPtr dpy, IntPtr image);

        [DllImport(Lib)] public static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport(Lib)] public static extern void glEGLImageTargetTexture2DOES(int target, IntPtr image);

        [DllImport(Lib)] public static extern IntPtr glGetString(int name);

        [DllImport(Lib)] public static extern void glGetFloatv(int pname, float[] data);

        [DllImport(Lib)] public static extern void glGetIntegerv(int pname, out int data);

        [DllImport(Lib)] public static extern void glReadBuffer(int mode);

        [DllImport(Lib)] public static extern void glReadPixels(int x, int y, int w, int h,
            int format, int type, byte[] pixels);

        [DllImport(Lib)] public static extern void glReadPixels(int x, int y, int w, int h,
            int format, int type, float[] pixels);

        [DllImport(Lib)] public static extern void glGenTextures(int n, out uint tex);

        [DllImport(Lib)] public static extern void glBindTexture(int target, uint tex);

        [DllImport(Lib)] public static extern void glDeleteTextures(int n, ref uint tex);

        [DllImport(Lib)] public static extern void glGenFramebuffers(int n, out uint fbo);

        [DllImport(Lib)] public static extern void glBindFramebuffer(int target, uint fbo);

        [DllImport(Lib)] public static extern void glDeleteFramebuffers(int n, ref uint fbo);

        [DllImport(Lib)] public static extern void glFramebufferTexture2D(int target, int attachment,
            int texTarget, uint tex, int level);

        [DllImport(Lib)] public static extern int glCheckFramebufferStatus(int target);

        [DllImport(Lib)] public static extern int glGetError();

        [DllImport(Lib)] public static extern void glTexImage2D(int target, int level, int internalformat,
            int width, int height, int border, int format, int type, IntPtr pixels);

        [DllImport(Lib)] public static extern void glTexParameteri(int target, int pname, int param);

        [DllImport(Lib)] public static extern void glGenBuffers(int n, out uint buffers);

        [DllImport(Lib)] public static extern void glBindBuffer(int target, uint buffer);

        [DllImport(Lib)] public static extern void glBufferData(int target, IntPtr size, float[] data, int usage);

        [DllImport(Lib)] public static extern void glGenVertexArrays(int n, out uint arrays);

        [DllImport(Lib)] public static extern void glBindVertexArray(uint array);

        [DllImport(Lib)] public static extern void glVertexAttribPointer(uint index, int size, int type,
            bool normalized, int stride, IntPtr offset);

        [DllImport(Lib)] public static extern void glEnableVertexAttribArray(uint index);

        [DllImport(Lib)] public static extern void glDrawArrays(int mode, int first, int count);

        [DllImport(Lib)] public static extern void glActiveTexture(int texture);

        [DllImport(Lib)] public static extern void glViewport(int x, int y, int w, int h);

        [DllImport(Lib)] public static extern void glClearColor(float r, float g, float b, float a);

        [DllImport(Lib)] public static extern void glClear(uint mask);

        [DllImport(Lib)] public static extern uint glCreateShader(int type);

        [DllImport(Lib)] public static extern void glShaderSource(uint shader, int count, string[] source, IntPtr length);

        [DllImport(Lib)] public static extern void glCompileShader(uint shader);

        [DllImport(Lib)] public static extern uint glCreateProgram();

        [DllImport(Lib)] public static extern void glAttachShader(uint program, uint shader);

        [DllImport(Lib)] public static extern void glLinkProgram(uint program);

        [DllImport(Lib)] public static extern void glDeleteShader(uint shader);

        [DllImport(Lib)] public static extern void glUseProgram(uint program);

        [DllImport(Lib)] public static extern int glGetUniformLocation(uint program, string name);

        [DllImport(Lib)] public static extern void glUniform1i(int location, int v0);

        [DllImport(Lib)] public static extern void glEnable(int cap);

        [DllImport(Lib)] public static extern void glDisable(int cap);

        [DllImport(Lib)] public static extern void glDrawElements(int mode, int count, int type, IntPtr indices);

        [DllImport(Lib)] public static extern void glGetShaderiv(uint shader, int pname, out int param);

        [DllImport(Lib)] public static extern void glGetShaderInfoLog(uint shader, int bufSize, IntPtr length,
            System.Text.StringBuilder infoLog);

        [DllImport(Lib)] public static extern void glGetProgramiv(uint program, int pname, out int param);

        [DllImport(Lib)] public static extern void glGetProgramInfoLog(uint program, int bufSize, IntPtr length,
            System.Text.StringBuilder infoLog);

        [DllImport(Lib)] public static extern void glBufferData(int target, IntPtr size, uint[] data, int usage);

        [DllImport(Lib, EntryPoint = "glUniformMatrix4fv")]
        public static extern void glUniformMatrix4(int location, int count, bool transpose,
            float[] value);

        [DllImport(Lib)] public static extern void glUniform3fv(int location, int count, float[] value);

        [DllImport(Lib)] public static extern void glUniform1f(int location, float v0);

        [DllImport(Lib)] public static extern void glBlendFunc(int src, int dst);

        [DllImport(Lib)] public static extern void glDebugMessageCallback(IntPtr callback, IntPtr userParam);

        [DllImport(Lib)] public static extern void glDebugMessageControl(int source, int type, int severity,
            int count, IntPtr ids, int enabled);

        [DllImport(Lib)] public static extern void glDebugMessageInsert(int source, int type, uint id,
            int severity, int length, string message, IntPtr userParam);

        [DllImport(Lib)] public static extern void glFinish();
    }

    private sealed class LeaseOperation : ICustomDrawOperation
    {
        private readonly AngleSceneHost _owner;

        public LeaseOperation(AngleSceneHost owner) => _owner = owner;

        public Rect Bounds => new(0, 0, _owner.Bounds.Width, _owner.Bounds.Height);

        public bool HitTest(Point p) => true;

        public bool Intersects(Rect rect) => true;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context) => _owner.RenderInto(context);
    }
}
