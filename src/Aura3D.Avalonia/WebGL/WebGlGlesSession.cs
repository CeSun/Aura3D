#if WEBGL_HOST
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

#pragma warning disable AVA1700 // ISkiaSharpApiLeaseFeature 为 Avalonia 标注的 Unstable API

namespace Aura3D.Avalonia.WebGL;

/// <summary>
/// Browser（WebGL2）GLES 会话，与 iOS 的 <c>AngleGlesSession</c> 同构但更薄：
/// 不自建 EGL 上下文——Avalonia.Browser 的合成器已持有一个 WebGL2 上下文（经
/// SkiaSharp.NativeAssets.WebAssembly 链入的 emscripten GLES shim，GLES 调用最终落到
/// WebGL2），自定义绘制操作运行在该上下文的渲染线程上，直接在其上分配输出
/// 纹理 + FBO 供管线写入。合成侧因 Skia 与引擎共用同一 GL 上下文，输出纹理经
/// lease 的 GRContext 以 <see cref="GRGlTextureInfo"/> 零拷贝导入（无需 iOS 那样的
/// EGLImage 跨 API 包装，也无需 glFinish 同步——同上下文同线程，命令天然有序）。
/// 约束：所有 GL 调用只能发生在渲染线程（绘制操作或 Compositor 服务器任务内）。
/// </summary>
internal sealed class WebGlGlesSession
{
    // GLES tokens
    private const int GL_TEXTURE_2D = 0x0DE1;
    private const int GL_FRAMEBUFFER = 0x8D40;
    private const int GL_COLOR_ATTACHMENT0 = 0x8CE0;
    private const int GL_FRAMEBUFFER_COMPLETE = 0x8CD5;
    private const int GL_VERSION = 0x1F02;
    private const int GL_RENDERER = 0x1F01;
    private const int GL_TEXTURE_MIN_FILTER = 0x2801;
    private const int GL_TEXTURE_MAG_FILTER = 0x2800;
    private const int GL_TEXTURE_WRAP_S = 0x2802;
    private const int GL_TEXTURE_WRAP_T = 0x2803;
    private const int GL_LINEAR = 0x2601;
    private const int GL_CLAMP_TO_EDGE = 0x812F;
    private const int GL_RGBA8 = 0x8058;
    private const int GL_RGBA = 0x1908;
    private const int GL_UNSIGNED_BYTE = 0x1401;

    private uint _glTexture;
    private uint _fbo;
    private uint _pixelW;
    private uint _pixelH;
    private bool _infoTraced;

    /// <summary>初始化或渲染出现不可恢复错误。失败后宿主停止驱动本会话。</summary>
    public bool IsFailed { get; private set; }

    /// <summary>当前输出帧缓冲，供 <see cref="Aura3DViewBase"/> 写入 RenderSurface。</summary>
    public uint OutputFrameBufferId => _fbo;

    /// <summary>
    /// 喂给 RenderPipeline.Initialize 的入口点解析。emscripten 的 eglGetProcAddress
    /// 返回主 wasm 模块函数表内可经函数指针调用的真实地址（GLES3 子集，WebGL2 承载）。
    /// </summary>
    public static nint GetProcAddress(string name) => WebGlNative.eglGetProcAddress(name);

    /// <summary>
    /// 按像素尺寸（重）建输出纹理并把渲染目标 FBO 绑定为当前。尺寸未变时仅做 bind。
    /// 必须在渲染线程、合成器 GL 上下文 current 时调用（自定义绘制操作内天然满足）。
    /// </summary>
    public bool EnsureOutput(uint width, uint height)
    {
        if (IsFailed || width < 1 || height < 1)
            return false;

        if (!_infoTraced)
        {
            var version = PtrToUtf8(WebGlNative.glGetString(GL_VERSION));
            if (version is null)
                return Fail("no current WebGL context on render thread");
            Trace($"version='{version}' renderer='{PtrToUtf8(WebGlNative.glGetString(GL_RENDERER))}'");
            _infoTraced = true;
        }

        if (_fbo != 0 && width == _pixelW && height == _pixelH)
        {
            WebGlNative.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
            return true;
        }

        // 等旧纹理上的渲染结束再删除（同上下文下其实有序，防御性保留）
        WebGlNative.glFinish();

        DeleteOutput();

        WebGlNative.glGenTextures(1, out _glTexture);
        WebGlNative.glBindTexture(GL_TEXTURE_2D, _glTexture);
        WebGlNative.glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        WebGlNative.glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        WebGlNative.glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
        WebGlNative.glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
        WebGlNative.glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, (int)width, (int)height, 0,
            GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
        var texErr = WebGlNative.glGetError();
        if (texErr != 0 || _glTexture == 0)
            return Fail($"output texture alloc failed 0x{texErr:X}");

        WebGlNative.glGenFramebuffers(1, out _fbo);
        WebGlNative.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
        WebGlNative.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, _glTexture, 0);
        var status = WebGlNative.glCheckFramebufferStatus(GL_FRAMEBUFFER);
        if (status != GL_FRAMEBUFFER_COMPLETE)
            return Fail($"framebuffer incomplete 0x{status:X}");

        _pixelW = width;
        _pixelH = height;
        Trace($"output {width}x{height} fbo={_fbo} tex={_glTexture}");
        return true;
    }

    /// <summary>帧结束：上报残留 GL 错误。不解除 current——上下文归 Avalonia 合成器所有。</summary>
    public void FinishFrame()
    {
        if (IsFailed)
            return;

        var count = 0;
        int first = 0, err;
        while ((err = WebGlNative.glGetError()) != 0)
        {
            if (count == 0)
                first = err;
            count++;
            if (count > 16)
                break;
        }

        if (count > 0)
            Trace($"glError 0x{first:X} x{count}");
    }

    /// <summary>把输出纹理经 lease 合成到 Skia 画布（渲染线程调用）。</summary>
    public void Compose(ImmediateDrawingContext drawingContext,
        double logicalWidth, double logicalHeight)
    {
        if (IsFailed || _glTexture == 0)
            return;

        var feature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
        {
            Fail("no ISkiaSharpApiLeaseFeature (Skia compositor required)");
            return;
        }

        using var lease = feature.Lease();
        var gr = lease.GrContext;
        if (gr is null || lease.SkCanvas is null)
        {
            Fail("skia lease empty");
            return;
        }

        // SkiaSharp 3.x 移除了 SkglEnum，直接传 GL 枚举值。
        // 必须显式给出内部格式：Skia 在桌面 GL 上会用 glGetTexLevelParameteriv 反查纹理格式，
        // 而 WebGL2 无此入口点，缺省 format 会让 GrBackendTexture 导入失败（FromTexture 返回 null）。
        var info = new GRGlTextureInfo((uint)GL_TEXTURE_2D, _glTexture, (uint)GL_RGBA8);
        using var backend = new GRBackendTexture((int)_pixelW, (int)_pixelH, false, info);
        // GL 纹理为左下原点，按 BottomLeft 导入由 Skia 负责翻转。
        using var image = SKImage.FromTexture(gr, backend, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        if (image is null)
        {
            Fail("SKImage.FromTexture returned null");
            return;
        }

        lease.SkCanvas.DrawImage(image, new SKRect(0, 0, (float)logicalWidth, (float)logicalHeight));
    }

    /// <summary>宿主捕获到渲染异常时调用，使会话进入不可恢复状态，避免每帧重复抛出。</summary>
    public void FailFromOwner(string message) => Fail("owner: " + message);

    /// <summary>
    /// 在渲染线程上释放 GPU 资源（上下文由合成器持有且仍然有效）：先经
    /// <paramref name="releaseGpuResources"/> 逐个归还管线的 GL 对象，再删除宿主自身的
    /// FBO/纹理。只能从绘制操作或 Compositor 服务器任务中调用。
    /// </summary>
    public bool ReleaseOnRenderThread(Action? releaseGpuResources = null)
    {
        if (IsFailed)
        {
            DeleteOutput();
            return false;
        }

        try
        {
            releaseGpuResources?.Invoke();
        }
        catch (Exception ex)
        {
            Trace("release failed: " + ex);
        }

        WebGlNative.glFinish();
        DeleteOutput();
        _infoTraced = false;
        return true;
    }

    private void DeleteOutput()
    {
        if (_fbo != 0)
            WebGlNative.glDeleteFramebuffers(1, ref _fbo);
        if (_glTexture != 0)
            WebGlNative.glDeleteTextures(1, ref _glTexture);
        _fbo = 0;
        _glTexture = 0;
        _pixelW = 0;
        _pixelH = 0;
    }

    private bool Fail(string message)
    {
        IsFailed = true;
        Trace("FAIL " + message);
        return false;
    }

    private static void Trace(string message) => Console.WriteLine("[aura3d-webgl] " + message);

    private static string? PtrToUtf8(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
}
#endif
