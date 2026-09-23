#if ANGLE_HOST
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Metal;
using SkiaSharp;

#pragma warning disable AVA1700 // ISkiaSharpApiLeaseFeature 为 Avalonia 标注的 Unstable API

namespace Aura3D.Avalonia.Angle;

/// <summary>
/// 自持的 ANGLE（Metal 后端）GLES 上下文与输出纹理。
/// 输出通路：MTLTexture(Shared/RGBA8) → eglCreateImageKHR(EGL_METAL_TEXTURE_ANGLE)
/// → GL 纹理 → FBO；同一张 MTLTexture 经 Avalonia.Skia 的 lease 以 GRBackendTexture
/// 导入合成侧（TopLeft 朝向，GL 侧为左下原点，绘制时纵向翻转）。
/// 每帧以 glFinish 保证纹理内容就绪；升级为 EGL_ANGLE_metal_shared_event_sync 是后续项。
/// </summary>
internal sealed class AngleGlesSession
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

    private IntPtr _display;
    private IntPtr _context;
    private IntPtr _surface;
    private IntPtr _config;
    private IntPtr _eglImage;
    private IMTLTexture? _texture;
    private uint _glTexture;
    private uint _fbo;
    private uint _pixelW;
    private uint _pixelH;
    private bool _ready;

    /// <summary>初始化或渲染出现不可恢复错误。失败后宿主停止驱动本会话。</summary>
    public bool IsFailed { get; private set; }

    /// <summary>当前输出帧缓冲，供 <see cref="Aura3DViewBase"/> 写入 RenderSurface。</summary>
    public uint OutputFrameBufferId => _fbo;

    /// <summary>喂给 RenderPipeline.Initialize 的入口点解析。</summary>
    public static nint GetProcAddress(string name)
    {
        var p = AngleNative.eglGetProcAddress(name);
        if (p == IntPtr.Zero || p == new IntPtr(-1))
            p = AngleNative.dlsym(new IntPtr(-2), name); // RTLD_DEFAULT，覆盖 ANGLE 直接导出的核心符号
        return p;
    }

    /// <summary>确保 display/上下文已创建并保持当前线程 current。失败进入 <see cref="IsFailed"/>。</summary>
    public bool EnsureReady()
    {
        if (IsFailed)
            return false;
        if (_ready)
            return AngleNative.eglMakeCurrent(_display, _surface, _surface, _context);

        _display = AngleNative.eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, IntPtr.Zero,
            new[] { EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_METAL_ANGLE, EGL_NONE });
        if (_display == IntPtr.Zero)
            return Fail("no ANGLE Metal display");

        if (!AngleNative.eglInitialize(_display, out var maj, out var min) || maj < 1)
            return Fail($"eglInitialize 0x{AngleNative.eglGetError():X}");

        var clientExts = PtrToUtf8(AngleNative.eglQueryString(_display, EGL_EXTENSIONS)) ?? "";
        if (!clientExts.Contains("EGL_KHR_image_base") ||
            !clientExts.Contains("EGL_ANGLE_metal_texture_client_buffer"))
            return Fail("missing EGL extension (EGL_KHR_image_base / EGL_ANGLE_metal_texture_client_buffer)");

        AngleNative.eglBindAPI(EGL_OPENGL_ES_API);

        int[] configAttribs =
        {
            EGL_SURFACE_TYPE, EGL_PBUFFER_BIT,
            EGL_RENDERABLE_TYPE, EGL_OPENGL_ES3_BIT,
            EGL_RED_SIZE, 8, EGL_GREEN_SIZE, 8, EGL_BLUE_SIZE, 8, EGL_ALPHA_SIZE, 8,
            EGL_NONE,
        };
        if (!AngleNative.eglChooseConfig(_display, configAttribs, out _config, 1, out var n) || n < 1)
            return Fail($"eglChooseConfig 0x{AngleNative.eglGetError():X}");

        _context = AngleNative.eglCreateContext(_display, _config, IntPtr.Zero,
            new[] { EGL_CONTEXT_CLIENT_VERSION, 3, EGL_NONE });
        if (_context == IntPtr.Zero)
            return Fail($"eglCreateContext 0x{AngleNative.eglGetError():X}");

        // 输出走 EGLImage 绑定的 MTLTexture，pbuffer 仅作为 current draw/read surface。
        _surface = AngleNative.eglCreatePbufferSurface(_display, _config,
            new[] { EGL_WIDTH, 1, EGL_HEIGHT, 1, EGL_NONE });
        if (_surface == IntPtr.Zero)
            return Fail($"eglCreatePbufferSurface 0x{AngleNative.eglGetError():X}");

        if (!AngleNative.eglMakeCurrent(_display, _surface, _surface, _context))
            return Fail($"eglMakeCurrent 0x{AngleNative.eglGetError():X}");

        _ready = true;
        Trace($"egl {maj}.{min} ok, version='{PtrToUtf8(AngleNative.glGetString(GL_VERSION))}' " +
              $"renderer='{PtrToUtf8(AngleNative.glGetString(GL_RENDERER))}'");
        return true;
    }

    /// <summary>
    /// 按像素尺寸（重）建输出纹理并把渲染目标 FBO 绑定为当前。尺寸未变时仅做 make-current + bind。
    /// </summary>
    public bool EnsureOutput(uint width, uint height)
    {
        if (IsFailed || width < 1 || height < 1)
            return false;

        if (!EnsureReady())
            return false;

        if (_texture != null && width == _pixelW && height == _pixelH)
        {
            AngleNative.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
            return true;
        }

        // 等旧纹理上的渲染结束再删除
        AngleNative.glFinish();

        if (_fbo != 0)
            AngleNative.glDeleteFramebuffers(1, ref _fbo);
        if (_glTexture != 0)
            AngleNative.glDeleteTextures(1, ref _glTexture);
        if (_eglImage != IntPtr.Zero)
            AngleNative.eglDestroyImageKHR(_display, _eglImage);
        _texture?.Dispose();
        _fbo = 0;
        _glTexture = 0;
        _eglImage = IntPtr.Zero;
        _texture = null;

        var device = MTLDevice.SystemDefault;
        if (device is null)
            return Fail("no MTLDevice");

        var descriptor = new MTLTextureDescriptor
        {
            TextureType = MTLTextureType.k2D,
            Width = (nuint)width,
            Height = (nuint)height,
            MipmapLevelCount = 1,
            PixelFormat = MTLPixelFormat.RGBA8Unorm,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared,
        };
        _texture = device.CreateTexture(descriptor);
        if (_texture is null)
            return Fail("MTLDevice.CreateTexture returned null");

        _eglImage = AngleNative.eglCreateImageKHR(_display, IntPtr.Zero, EGL_METAL_TEXTURE_ANGLE,
            _texture.Handle, null);
        if (_eglImage == IntPtr.Zero)
            return Fail($"eglCreateImageKHR 0x{AngleNative.eglGetError():X}");

        AngleNative.glGenTextures(1, out _glTexture);
        AngleNative.glBindTexture(GL_TEXTURE_2D, _glTexture);
        AngleNative.glEGLImageTargetTexture2DOES(GL_TEXTURE_2D, _eglImage);
        var glErr = AngleNative.glGetError();
        if (glErr != 0)
            return Fail($"glEGLImageTargetTexture2DOES 0x{glErr:X}");

        AngleNative.glGenFramebuffers(1, out _fbo);
        AngleNative.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
        AngleNative.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, _glTexture, 0);
        var status = AngleNative.glCheckFramebufferStatus(GL_FRAMEBUFFER);
        if (status != GL_FRAMEBUFFER_COMPLETE)
            return Fail($"framebuffer incomplete 0x{status:X}");

        _pixelW = width;
        _pixelH = height;
        Trace($"output {width}x{height} fbo={_fbo}");
        return true;
    }

    /// <summary>帧结束：glFinish 保证纹理内容就绪（正式同步机制待 shared event 升级），并上报残留 GL 错误。</summary>
    public void FinishFrame()
    {
        AngleNative.glFinish();

        var count = 0;
        int first = 0, err;
        while ((err = AngleNative.glGetError()) != 0)
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
        if (IsFailed || _texture is null)
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

        var info = new GRMtlTextureInfo(_texture.Handle);
        using var backend = new GRBackendTexture((int)_pixelW, (int)_pixelH, false, info);
        using var image = SKImage.FromTexture(gr, backend, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888);
        if (image is null)
        {
            Fail("SKImage.FromTexture returned null");
            return;
        }

        var canvas = lease.SkCanvas;
        canvas.Save();
        // GL 纹理为左下原点，按 TopLeft 导入后需纵向翻转。
        canvas.Translate(0, (float)logicalHeight);
        canvas.Scale(1, -1);
        canvas.DrawImage(image, new SKRect(0, 0, (float)logicalWidth, (float)logicalHeight));
        canvas.Restore();
    }

    public bool TryMakeCurrent() =>
        _ready && !IsFailed && AngleNative.eglMakeCurrent(_display, _surface, _surface, _context);

    /// <summary>宿主捕获到渲染异常时调用，使会话进入不可恢复状态，避免每帧重复抛出。</summary>
    public void FailFromOwner(string message) => Fail("owner: " + message);

    /// <summary>会话脱离使用后可显式销毁原生资源。</summary>
    public void Dispose()
    {
        if (!_ready)
            return;

        AngleNative.eglMakeCurrent(_display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_fbo != 0)
            AngleNative.glDeleteFramebuffers(1, ref _fbo);
        if (_glTexture != 0)
            AngleNative.glDeleteTextures(1, ref _glTexture);
        if (_eglImage != IntPtr.Zero)
            AngleNative.eglDestroyImageKHR(_display, _eglImage);
        _texture?.Dispose();
    }

    private bool Fail(string message)
    {
        IsFailed = true;
        Trace("FAIL " + message);
        return false;
    }

    private static void Trace(string message) => Console.WriteLine("[aura3d-angle] " + message);

    private static string? PtrToUtf8(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
}
#endif
