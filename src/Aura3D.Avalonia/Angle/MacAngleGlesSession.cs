using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable AVA1700 // ISkiaSharpApiLeaseFeature 为 Avalonia 标注的 Unstable API

namespace Aura3D.Avalonia.Angle;

/// <summary>
/// 形态 B 的自持 ANGLE（Metal 后端）GLES 会话，供 macOS 保持 GL 合成器时使用。
/// 输出通路：IOSurface('BGRA') → eglCreatePbufferFromClientBuffer(EGL_IOSURFACE_ANGLE)
/// → 引擎直接渲进该 pbuffer 的默认帧缓冲（FBO 0）；合成时在 Avalonia 的 NSOpenGL 上下文里
/// 经 CGLTexImageIOSurface2D 导入同一张 IOSurface，包成 GRBackendTexture(GL) 画进 Skia 画布
/// （GL 内容为左下原点，绘制时纵向翻转）。
/// 帧内时序单线程串行（EnsureOutput → 渲染 → FinishFrame → Compose 同持 <see cref="SyncRoot"/>），
/// 写侧 glFinish 保证 IOSurface 内容就绪后才导入，无异步回收，故不引入 use-count 握手。
/// </summary>
internal sealed unsafe class MacAngleGlesSession
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
    private const int EGL_PLATFORM_ANGLE_ANGLE = 0x3202;
    private const int EGL_PLATFORM_ANGLE_TYPE_ANGLE = 0x3203;
    private const int EGL_PLATFORM_ANGLE_TYPE_METAL_ANGLE = 0x3489;
    private const int EGL_IOSURFACE_ANGLE = 0x3454;
    private const int EGL_IOSURFACE_PLANE_ANGLE = 0x345A;
    // EGL_TEXTURE_TARGET 的标准值是 0x3081（0x4006 是笔误，会被当作未知键忽略，
    // 进而触发 "texture target mismatch" 的 EGL_BAD_ATTRIBUTE）。
    private const int EGL_TEXTURE_TARGET = 0x3081;
    private const int EGL_TEXTURE_INTERNAL_FORMAT_ANGLE = 0x345D;
    private const int EGL_TEXTURE_FORMAT = 0x3080;
    private const int EGL_TEXTURE_TYPE_ANGLE = 0x345C;
    private const int EGL_TEXTURE_RGBA = 0x305E;
    private const int EGL_TEXTURE_2D = 0x305F;

    // GLES tokens
    private const int GL_VERSION = 0x1F02;
    private const int GL_RENDERER = 0x1F01;
    private const int GL_BGRA_EXT = 0x80E1;
    private const int GL_UNSIGNED_BYTE = 0x1401;

    // 合成侧（系统 OpenGL 框架）
    private const uint GL_TEXTURE_RECTANGLE = 0x84F5;
    private const uint GL_TEXTURE_BINDING_RECTANGLE = 0x84F6;
    private const uint GL_TEXTURE_2D = 0x0DE1;
    private const uint GL_TEXTURE_BINDING_2D = 0x8069;
    private const uint GL_ACTIVE_TEXTURE = 0x84E0;
    private const uint GL_TEXTURE0 = 0x84C0;
    private const uint GL_RGBA8 = 0x8058; // 同时用作 GRGlTextureInfo.Format
    private const int GL_UNSIGNED_INT_8_8_8_8_REV = 0x8367;

    /// <summary>IOSurface 像素格式四字码 'BGRA'，ANGLE 的 Metal 后端按它选择 MTLPixelFormat。</summary>
    private const int IoSurfacePixelFormatBgra = 0x42475241;

    private nint _display;
    private nint _context;
    private nint _placeholderSurface;
    private nint _config;
    private nint _ioSurface;
    private nint _outputSurface;
    private uint _pixelW;
    private uint _pixelH;
    private bool _ready;

    // 合成侧持久资源：GL 纹理在 Avalonia 上下文里创建一次，每帧重新导入 IOSurface 内容。
    private uint _composeTexture;
    private uint _composeTexW;
    private uint _composeTexH;

    /// <summary>
    /// 串行化整段 GLES 帧与 <see cref="Release"/>。EGL 上下文同一时刻只能被一个线程持有，
    /// 宿主必须把「EnsureOutput → 渲染 → FinishFrame → Compose」整段放在该锁内。
    /// </summary>
    public object SyncRoot { get; } = new();

    /// <summary>初始化或渲染出现不可恢复错误。失败后宿主停止驱动本会话。</summary>
    public bool IsFailed { get; private set; }

    /// <summary>当前输出帧缓冲。pbuffer 通路下引擎渲到默认帧缓冲，恒为 0。</summary>
    public uint OutputFrameBufferId => 0;

    /// <summary>喂给 RenderPipeline.Initialize 的入口点解析。</summary>
    public static nint GetProcAddress(string name)
    {
        var utf8 = Encoding.UTF8.GetBytes(name);
        var buf = Marshal.AllocHGlobal(utf8.Length + 1);
        try
        {
            Marshal.Copy(utf8, 0, buf, utf8.Length);
            Marshal.WriteByte(buf, utf8.Length, 0);

            var p = MacAngleNative.EglGetProcAddress((byte*)buf);
            if (p == nint.Zero)
                p = NativeLibrary.GetExport(NativeLibrary.GetMainProgramHandle(), name);
            return p;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    /// <summary>确保 display/上下文已创建并保持当前线程 current。失败进入 <see cref="IsFailed"/>。</summary>
    public bool EnsureReady()
    {
        if (IsFailed)
            return false;
        if (_ready)
            return MacAngleNative.EglMakeCurrent(_display, _placeholderSurface, _placeholderSurface, _context) != 0;

        if (!MacAngleNative.Resolve())
            return Fail(MacAngleNative.LoadError ?? "native symbols unavailable");

        int* displayAttribs = stackalloc int[]
        {
            EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_METAL_ANGLE, EGL_NONE,
        };
        _display = MacAngleNative.EglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, nint.Zero, displayAttribs);
        if (_display == nint.Zero)
            return Fail("no ANGLE Metal display");

        int maj = 0, min = 0;
        if (MacAngleNative.EglInitialize(_display, &maj, &min) == 0 || maj < 1)
            return Fail($"eglInitialize 0x{MacAngleNative.EglGetError():X}");

        var clientExts = PtrToUtf8(MacAngleNative.EglQueryString(_display, EGL_EXTENSIONS)) ?? "";
        if (!clientExts.Contains("EGL_ANGLE_iosurface_client_buffer"))
            return Fail($"ANGLE Metal build lacks EGL_ANGLE_iosurface_client_buffer; extensions='{clientExts}'");

        MacAngleNative.EglBindAPI(EGL_OPENGL_ES_API);

        int n = 0;
        nint config = nint.Zero;
        int* configAttribs = stackalloc int[]
        {
            EGL_SURFACE_TYPE, EGL_PBUFFER_BIT,
            EGL_RENDERABLE_TYPE, EGL_OPENGL_ES3_BIT,
            EGL_RED_SIZE, 8, EGL_GREEN_SIZE, 8, EGL_BLUE_SIZE, 8, EGL_ALPHA_SIZE, 8,
            EGL_NONE,
        };
        if (MacAngleNative.EglChooseConfig(_display, configAttribs, &config, 1, &n) == 0 || n < 1)
            return Fail($"eglChooseConfig 0x{MacAngleNative.EglGetError():X}");
        _config = config;

        int* contextAttribs = stackalloc int[] { EGL_CONTEXT_CLIENT_VERSION, 3, EGL_NONE };
        _context = MacAngleNative.EglCreateContext(_display, _config, nint.Zero, contextAttribs);
        if (_context == nint.Zero)
            return Fail($"eglCreateContext 0x{MacAngleNative.EglGetError():X}");

        // 占位 pbuffer：仅在首个输出就位前充当 current surface。
        int* placeholderAttribs = stackalloc int[] { EGL_WIDTH, 1, EGL_HEIGHT, 1, EGL_NONE };
        _placeholderSurface = MacAngleNative.EglCreatePbufferSurface(_display, _config, placeholderAttribs);
        if (_placeholderSurface == nint.Zero)
            return Fail($"eglCreatePbufferSurface 0x{MacAngleNative.EglGetError():X}");

        if (MacAngleNative.EglMakeCurrent(_display, _placeholderSurface, _placeholderSurface, _context) == 0)
            return Fail($"eglMakeCurrent 0x{MacAngleNative.EglGetError():X}");

        _ready = true;
        Trace($"egl {maj}.{min} ok, version='{PtrToUtf8((nint)MacAngleNative.GlGetString(GL_VERSION))}' " +
              $"renderer='{PtrToUtf8((nint)MacAngleNative.GlGetString(GL_RENDERER))}'");
        return true;
    }

    /// <summary>
    /// 按像素尺寸（重）建 IOSurface 输出并把它所在的 pbuffer 设为 current。尺寸未变时仅 make-current。
    /// </summary>
    public bool EnsureOutput(uint width, uint height)
    {
        if (IsFailed || width < 1 || height < 1)
            return false;

        if (!EnsureReady())
            return false;

        if (_outputSurface != nint.Zero && width == _pixelW && height == _pixelH)
        {
            MacAngleNative.EglMakeCurrent(_display, _outputSurface, _outputSurface, _context);
            return true;
        }

        // 等旧表面上的渲染结束再删除。
        MacAngleNative.GlFinish();
        MacAngleNative.EglMakeCurrent(_display, _placeholderSurface, _placeholderSurface, _context);

        if (_outputSurface != nint.Zero)
            MacAngleNative.EglDestroySurface(_display, _outputSurface);
        if (_ioSurface != nint.Zero)
            MacAngleNative.IoSurfaceRelease(_ioSurface);
        _outputSurface = nint.Zero;
        _ioSurface = nint.Zero;

        _ioSurface = MacAngleNative.IoSurfaceCreateBgra((int)width, (int)height);
        if (_ioSurface == nint.Zero)
            return Fail("IOSurfaceCreate failed");

        int* attribs = stackalloc int[]
        {
            EGL_WIDTH, (int)width,
            EGL_HEIGHT, (int)height,
            EGL_IOSURFACE_PLANE_ANGLE, 0,
            EGL_TEXTURE_TARGET, EGL_TEXTURE_2D,
            EGL_TEXTURE_INTERNAL_FORMAT_ANGLE, GL_BGRA_EXT,
            EGL_TEXTURE_FORMAT, EGL_TEXTURE_RGBA,
            EGL_TEXTURE_TYPE_ANGLE, GL_UNSIGNED_BYTE,
            EGL_NONE,
        };
        _outputSurface = MacAngleNative.EglCreatePbufferFromClientBuffer(
            _display, EGL_IOSURFACE_ANGLE, _ioSurface, _config, attribs);
        if (_outputSurface == nint.Zero)
            return Fail($"eglCreatePbufferFromClientBuffer 0x{MacAngleNative.EglGetError():X}");

        if (MacAngleNative.EglMakeCurrent(_display, _outputSurface, _outputSurface, _context) == 0)
            return Fail($"eglMakeCurrent(output) 0x{MacAngleNative.EglGetError():X}");

        _pixelW = width;
        _pixelH = height;
        Trace($"output {width}x{height} ioSurface=0x{_ioSurface:X}");
        return true;
    }

    /// <summary>
    /// 帧结束：glFinish 保证 IOSurface 内容对合成侧可见，上报残留 GL 错误，
    /// 并解除上下文绑定（分离时 UI 线程回收才能合法接管）。
    /// </summary>
    public void FinishFrame()
    {
        if (IsFailed || !_ready)
            return;

        MacAngleNative.GlFinish();

        var count = 0;
        int first = 0, err;
        while ((err = MacAngleNative.GlGetError()) != 0)
        {
            if (count == 0)
                first = err;
            count++;
            if (count > 16)
                break;
        }

        if (count > 0)
            Trace($"glError 0x{first:X} x{count}");

        MacAngleNative.EglMakeCurrent(_display, nint.Zero, nint.Zero, nint.Zero);
    }

    /// <summary>
    /// 把 IOSurface 内容经 lease 合成到 Skia 画布。仅在 GL 合成器下有效（调用时 Avalonia 的
    /// NSOpenGL 上下文必须 current）；渲染线程调用，须在 <see cref="SyncRoot"/> 内。
    /// </summary>
    public void Compose(ImmediateDrawingContext drawingContext, double logicalWidth, double logicalHeight)
    {
        if (IsFailed || _ioSurface == nint.Zero)
            return;

        var feature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
        {
            Fail("no ISkiaSharpApiLeaseFeature (GL compositor required)");
            return;
        }

        using var lease = feature.Lease();
        var gr = lease.GrContext;
        if (gr is null || lease.SkCanvas is null)
        {
            Fail("skia lease empty");
            return;
        }

        var cgl = MacAngleNative.CglGetCurrentContext();
        if (cgl == nint.Zero)
        {
            Fail("no current CGL context during compose (compositor is not GL)");
            return;
        }

        // 每帧把 IOSurface 的最新内容重新导入同一张纹理（这就是跨 API 的同步握手）。
        // 保存/恢复 Skia 正在使用的纹理绑定，避免污染它的状态缓存。
        int* intOut = stackalloc int[1];
        MacAngleNative.GlGetIntegervNative(GL_ACTIVE_TEXTURE, intOut);
        var prevActive = (uint)intOut[0];
        MacAngleNative.GlGetIntegervNative(GL_TEXTURE_BINDING_RECTANGLE, intOut);
        var prevRectBinding = (uint)intOut[0];
        MacAngleNative.GlGetIntegervNative(GL_TEXTURE_BINDING_2D, intOut);
        var prevTex2dBinding = (uint)intOut[0];

        if (_composeTexture == 0 || _composeTexW != _pixelW || _composeTexH != _pixelH)
        {
            if (_composeTexture != 0)
            {
                uint old = _composeTexture;
                MacAngleNative.GlDeleteTexturesNative(1, &old);
            }
            uint tex = 0;
            MacAngleNative.GlGenTexturesNative(1, &tex);
            _composeTexture = tex;
            _composeTexW = _pixelW;
            _composeTexH = _pixelH;
        }

        MacAngleNative.GlActiveTextureNative(GL_TEXTURE0);
        MacAngleNative.GlBindTextureNative(GL_TEXTURE_RECTANGLE, _composeTexture);
        var cglErr = MacAngleNative.CglTexImageIOSurface2D(cgl, GL_TEXTURE_RECTANGLE, GL_RGBA8,
            (int)_pixelW, (int)_pixelH, GL_BGRA_EXT, GL_UNSIGNED_INT_8_8_8_8_REV, _ioSurface, 0);
        if (cglErr != 0)
        {
            Fail($"CGLTexImageIOSurface2D error {cglErr}");
            return;
        }

        var glInfo = new GRGlTextureInfo
        {
            Id = (uint)_composeTexture,
            Target = GL_TEXTURE_RECTANGLE,
            Format = GL_RGBA8,
        };
        using var backend = new GRBackendTexture((int)_pixelW, (int)_pixelH, false, glInfo);
        using var image = SKImage.FromTexture(gr, backend, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
        if (image is null)
        {
            Fail("SKImage.FromTexture returned null");
            return;
        }

        var canvas = lease.SkCanvas;
        canvas.Save();
        // GL 内容为左下原点，按 TopLeft 导入后需纵向翻转。
        canvas.Translate(0, (float)logicalHeight);
        canvas.Scale(1, -1);
        canvas.DrawImage(image, new SKRect(0, 0, (float)logicalWidth, (float)logicalHeight));
        canvas.Restore();

        // 确保合成侧真的读过 IOSurface 再交还控制权。
        gr.Flush();

        // 恢复 Skia 的绑定状态。
        MacAngleNative.GlActiveTextureNative(prevActive);
        MacAngleNative.GlBindTextureNative(GL_TEXTURE_RECTANGLE, prevRectBinding);
        MacAngleNative.GlBindTextureNative(GL_TEXTURE_2D, prevTex2dBinding);
    }

    /// <summary>宿主捕获到渲染异常时调用，使会话进入不可恢复状态，避免每帧重复抛出。</summary>
    public void FailFromOwner(string message) => Fail("owner: " + message);

    /// <summary>
    /// 销毁会话的全部 EGL/IOSurface 对象，幂等。帧间已解除 current，UI 线程回收可以合法接管：
    /// 先在 ANGLE 上下文上执行 <paramref name="releaseGpuResources"/> 逐个归还管线的 GL 对象，
    /// 再销毁输出表面与 IOSurface。与帧渲染互斥，因此会等待进行中的那一帧结束。
    /// </summary>
    public bool Release(Action? releaseGpuResources = null)
    {
        lock (SyncRoot)
        {
            if (_display == nint.Zero)
                return false;

            var taken = _ready &&
                MacAngleNative.EglMakeCurrent(_display, _placeholderSurface, _placeholderSurface, _context) != 0;

            if (taken)
            {
                try
                {
                    releaseGpuResources?.Invoke();
                }
                catch (Exception ex)
                {
                    Trace("release failed: " + ex);
                }

                MacAngleNative.EglMakeCurrent(_display, nint.Zero, nint.Zero, nint.Zero);
            }

            if (_outputSurface != nint.Zero)
                MacAngleNative.EglDestroySurface(_display, _outputSurface);
            if (_ioSurface != nint.Zero)
                MacAngleNative.IoSurfaceRelease(_ioSurface);
            MacAngleNative.EglDestroySurface(_display, _placeholderSurface);
            MacAngleNative.EglDestroyContext(_display, _context);
            // 有意不调 eglTerminate：ANGLE 缓存 platform display，同进程内其他会话可能仍依赖它。

            _outputSurface = _ioSurface = nint.Zero;
            _placeholderSurface = _context = _display = _config = nint.Zero;
            _pixelW = 0;
            _pixelH = 0;
            _ready = false;

            return taken;
        }
    }

    private bool Fail(string message)
    {
        IsFailed = true;
        Trace("FAIL " + message);
        return false;
    }

    private static void Trace(string message) => Console.WriteLine("[aura3d-macangle] " + message);

    private static string? PtrToUtf8(nint p) => p == nint.Zero ? null : Marshal.PtrToStringUTF8(p);
}
