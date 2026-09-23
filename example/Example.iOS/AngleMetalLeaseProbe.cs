using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Metal;
using SkiaSharp;

#pragma warning disable AVA1700 // Unstable API

namespace Example.iOS;

/// <summary>
/// T3 探针：验证「自建 MTLTexture → eglCreateImageKHR 导入为 GL 纹理 → ANGLE 渲染 → 同一纹理经 Skia lease 上屏」闭环。
/// 验证结束后连同 App.RootViewFactory 钩子一起删除。
/// </summary>
public sealed class AngleMetalLeaseProbe : Control
{
    private const int Size = 256;
    private const int BandThickness = 32;
    private const double ImageX = 40;
    private const double ImageY = 150;

    // EGL tokens
    private const int EGL_SUCCESS = 0x3000;
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
    private const int GL_COLOR_BUFFER_BIT = 0x00004000;
    private const int GL_SCISSOR_TEST = 0x0C11;
    private const int GL_EXTENSIONS = 0x1F03;

    private readonly LeaseOperation _operation;
    private readonly TextBlock _status = new()
    {
        Margin = new Thickness(10),
        FontSize = 13,
        Foreground = Brushes.Black,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
    };

    private readonly Queue<string> _lines = new();

    private IMTLTexture? _texture;
    private IntPtr _display;
    private IntPtr _context;
    private IntPtr _surface;
    private IntPtr _config;
    private IntPtr _eglImage;
    private uint _glTexture;
    private uint _fbo;
    private bool _angleFailed;
    private int _frame;
    private int _imported;
    private int _failed;
    private DispatcherTimer? _timer;

    public AngleMetalLeaseProbe()
    {
        _operation = new LeaseOperation(this);
        Log("angle probe ready");
    }

    public Control BuildPage()
    {
        var page = new Grid { Background = Brushes.White };
        page.Children.Add(this);
        page.Children.Add(new TextBlock
        {
            Text = "ANGLE Metal backend: magenta clear + green moving band",
            FontSize = 13,
            Foreground = Brushes.Black,
            Margin = new Thickness(ImageX, ImageY - 22, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        });
        page.Children.Add(_status);
        return page;
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
        var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
        {
            Log("no lease feature");
            return;
        }

        try
        {
            if (_angleFailed)
            {
                return;
            }

            if (_texture is null && !InitAngle())
            {
                return;
            }

            RenderWithAngle();

            using var lease = feature.Lease();
            var gr = lease.GrContext;
            if (gr is null || lease.SkCanvas is null)
            {
                Log("lease empty");
                return;
            }

            if (_frame == 0)
            {
                Log($"lease backend={gr.Backend}");
            }

            var info = new GRMtlTextureInfo(_texture!.Handle);
            using var backend = new GRBackendTexture(Size, Size, false, info);
            using var image = SKImage.FromTexture(gr, backend, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888);
            if (image is null)
            {
                _failed++;
                Log("image null");
                return;
            }

            lease.SkCanvas.DrawImage(image, new SKRect((float)ImageX, (float)ImageY,
                (float)(ImageX + Size), (float)(ImageY + Size)));
            _imported++;

            _frame++;
            if (_frame is 1 or 30 or 120 or 300 or 600 or 900)
            {
                Log($"frame={_frame} imported={_imported} failed={_failed}");
            }
        }
        catch (Exception ex)
        {
            _failed++;
            Log($"EX {ex}");
        }
    }

    private bool InitAngle()
    {
        _angleFailed = true; // 任何一步失败都不再重试，日志里有原因

        _display = Native.eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, IntPtr.Zero,
            new[] { EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_METAL_ANGLE, EGL_NONE });
        if (_display == IntPtr.Zero)
        {
            _display = Native.eglGetDisplay(IntPtr.Zero); // 兜底：默认 display
        }

        if (_display == IntPtr.Zero)
        {
            Log("no EGL display");
            return false;
        }

        if (!Native.eglInitialize(_display, out var maj, out var min) || maj < 1)
        {
            Log($"eglInitialize failed err=0x{Native.eglGetError():X}");
            return false;
        }

        var clientExts = PtrToUtf8(Native.eglQueryString(_display, EGL_EXTENSIONS)) ?? "";
        Log($"egl {maj}.{min}");
        if (!clientExts.Contains("EGL_KHR_image_base") ||
            !clientExts.Contains("EGL_ANGLE_metal_texture_client_buffer"))
        {
            Log("missing ext: " + clientExts);
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
        if (!Native.eglChooseConfig(_display, configAttribs, out var cfg, 1, out var n) || n < 1)
        {
            Log($"chooseConfig failed err=0x{Native.eglGetError():X}");
            return false;
        }

        _config = cfg;

        for (var version = 3; version >= 2; version--)
        {
            _context = Native.eglCreateContext(_display, _config, IntPtr.Zero,
                new[] { EGL_CONTEXT_CLIENT_VERSION, version, EGL_NONE });
            if (_context != IntPtr.Zero)
            {
                Log($"ctx ES{version}");
                break;
            }
        }

        if (_context == IntPtr.Zero)
        {
            Log($"createContext failed err=0x{Native.eglGetError():X}");
            return false;
        }

        _surface = Native.eglCreatePbufferSurface(_display, _config,
            new[] { EGL_WIDTH, 1, EGL_HEIGHT, 1, EGL_NONE });
        if (_surface == IntPtr.Zero ||
            !Native.eglMakeCurrent(_display, _surface, _surface, _context))
        {
            Log($"surface/makecurrent failed err=0x{Native.eglGetError():X}");
            return false;
        }

        var device = MTLDevice.SystemDefault;
        if (device is null)
        {
            Log("no MTLDevice");
            return false;
        }

        var descriptor = new MTLTextureDescriptor
        {
            TextureType = MTLTextureType.k2D,
            Width = Size,
            Height = Size,
            MipmapLevelCount = 1,
            PixelFormat = MTLPixelFormat.RGBA8Unorm,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared,
        };
        _texture = device.CreateTexture(descriptor);
        if (_texture is null)
        {
            Log("CreateTexture null");
            return false;
        }

        _eglImage = Native.eglCreateImageKHR(_display, IntPtr.Zero, EGL_METAL_TEXTURE_ANGLE, _texture.Handle, null);
        if (_eglImage == IntPtr.Zero)
        {
            // EGL_BAD_PARAMETER=0x300D：device 不匹配等
            Log($"createImage failed err=0x{Native.eglGetError():X}");
            return false;
        }

        Native.glGenTextures(1, out _glTexture);
        Native.glBindTexture(GL_TEXTURE_2D, _glTexture);
        while (Native.glGetError() != 0)
        {
        }

        var glRenderer = PtrToUtf8(Native.glGetString(0x1F01)) ?? "(null)";
        Log($"gl renderer='{glRenderer}'");
        Native.glEGLImageTargetTexture2DOES(GL_TEXTURE_2D, _eglImage);
        var glErr = Native.glGetError();
        if (glErr != 0)
        {
            Log($"imageTarget glError 0x{glErr:X}");
            return false;
        }

        Native.glGenFramebuffers(1, out _fbo);
        Native.glBindFramebuffer(GL_FRAMEBUFFER, _fbo);
        Native.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, _glTexture, 0);
        var status = Native.glCheckFramebufferStatus(GL_FRAMEBUFFER);
        if (status != GL_FRAMEBUFFER_COMPLETE)
        {
            Log($"fbo incomplete 0x{status:X}");
            return false;
        }

        _angleFailed = false;
        Log($"angle ready tex=0x{_texture.Handle:x} img=0x{_eglImage:x}");
        return true;
    }

    private void RenderWithAngle()
    {
        if (!Native.eglMakeCurrent(_display, _surface, _surface, _context))
        {
            Log($"makecurrent 0x{Native.eglGetError():X}");
            return;
        }

        Native.glViewport(0, 0, Size, Size);
        Native.glClearColor(1f, 0f, 1f, 1f); // 洋红
        Native.glClear(GL_COLOR_BUFFER_BIT);

        var band = (_frame * 4) % (Size - BandThickness);
        Native.glEnable(GL_SCISSOR_TEST);
        Native.glScissor(0, band, Size, BandThickness);
        Native.glClearColor(0f, 1f, 0f, 1f); // 绿色移动带
        Native.glClear(GL_COLOR_BUFFER_BIT);
        Native.glDisable(GL_SCISSOR_TEST);

        Native.glFinish(); // T3 用同步 finish，正式实现换 shared event
    }

    private static string? PtrToUtf8(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

    /// <summary>绘制回调在渲染线程，写 UI 属性必须切回 UI 线程。</summary>
    private void Log(string message)
    {
        Console.WriteLine("[probe] " + message);
        Dispatcher.UIThread.Post(() =>
        {
            _lines.Enqueue(message);
            while (_lines.Count > 8)
            {
                _lines.Dequeue();
            }

            _status.Text = string.Join(Environment.NewLine, _lines);
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

        [DllImport(Lib)] public static extern void glEGLImageTargetTexture2DOES(int target, IntPtr image);

        [DllImport(Lib)] public static extern IntPtr glGetString(int name);

        [DllImport(Lib)] public static extern void glGenTextures(int n, out uint tex);

        [DllImport(Lib)] public static extern void glBindTexture(int target, uint tex);

        [DllImport(Lib)] public static extern void glGenFramebuffers(int n, out uint fbo);

        [DllImport(Lib)] public static extern void glBindFramebuffer(int target, uint fbo);

        [DllImport(Lib)] public static extern void glFramebufferTexture2D(int target, int attachment,
            int texTarget, uint tex, int level);

        [DllImport(Lib)] public static extern int glCheckFramebufferStatus(int target);

        [DllImport(Lib)] public static extern int glGetError();

        [DllImport(Lib)] public static extern void glViewport(int x, int y, int w, int h);

        [DllImport(Lib)] public static extern void glClearColor(float r, float g, float b, float a);

        [DllImport(Lib)] public static extern void glClear(uint mask);

        [DllImport(Lib)] public static extern void glScissor(int x, int y, int w, int h);

        [DllImport(Lib)] public static extern void glEnable(int name);

        [DllImport(Lib)] public static extern void glDisable(int name);

        [DllImport(Lib)] public static extern void glFlush();

        [DllImport(Lib)] public static extern void glFinish();
    }

    private sealed class LeaseOperation : ICustomDrawOperation
    {
        private readonly AngleMetalLeaseProbe _owner;

        public LeaseOperation(AngleMetalLeaseProbe owner) => _owner = owner;

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
