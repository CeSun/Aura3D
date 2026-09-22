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
/// 临时探针：验证 iOS 默认 Metal 模式下，自建 MTLTexture 可以零拷贝经 Skia lease 合成上屏。
/// 验证结束后连同 App.RootViewFactory 钩子一起删除。
/// </summary>
public sealed class MetalLeaseProbe : Control
{
    private const int TextureSize = 256;
    private const int BytesPerPixel = 4;
    private const int MarkerThickness = 24;
    private const int BandThickness = 16;
    private const double ImageX = 40;
    private const double ImageY = 150;

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
    private readonly byte[] _pixels = new byte[TextureSize * TextureSize * BytesPerPixel];
    private readonly MTLRegion _region = MTLRegion.Create2D((nint)0, (nint)0, (nint)TextureSize, (nint)TextureSize);

    private GCHandle _pixelsHandle;
    private IMTLTexture? _texture;
    private int _frame;
    private int _imported;
    private int _failed;
    private DispatcherTimer? _timer;

    public MetalLeaseProbe()
    {
        _operation = new LeaseOperation(this);
        _pixelsHandle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
        Log("probe ready");
    }

    /// <summary>探针控件 + 说明文字，作为整屏根视图。</summary>
    public Control BuildPage()
    {
        var page = new Grid { Background = Brushes.White };
        page.Children.Add(this);
        page.Children.Add(MakeLabel("GRSurfaceOrigin.TopLeft", ImageX, ImageY - 22));
        page.Children.Add(MakeLabel("GRSurfaceOrigin.BottomLeft", ImageX, ImageY + TextureSize + 24 - 22));
        page.Children.Add(_status);
        return page;
    }

    private static TextBlock MakeLabel(string text, double x, double y) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = Brushes.Black,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        Margin = new Thickness(x, y, 0, 0),
    };

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
            Log("no ISkiaSharpApiLeaseFeature");
            return;
        }

        try
        {
            using var lease = feature.Lease();
            var grContext = lease.GrContext;
            if (grContext is null || lease.SkCanvas is null)
            {
                Log($"lease empty canvas={lease.SkCanvas is not null} gr={grContext is not null}");
                return;
            }

            if (_frame == 0)
            {
                Log($"lease backend={grContext.Backend} canvas={lease.SkCanvas.GetType().Name} " +
                    $"surface={lease.SkSurface?.GetType().Name ?? "null"}");
            }

            EnsureTexture();
            if (_texture is null)
            {
                return;
            }

            if (!Upload())
            {
                return;
            }

            var info = new GRMtlTextureInfo(_texture.Handle);
            var backend = new GRBackendTexture(TextureSize, TextureSize, false, info);

            Draw(lease.SkCanvas, grContext, backend, GRSurfaceOrigin.TopLeft, ImageX, ImageY, "TopLeft");
            Draw(lease.SkCanvas, grContext, backend, GRSurfaceOrigin.BottomLeft, ImageX,
                ImageY + TextureSize + 24, "BottomLeft");

            _frame++;
            if (_frame is 1 or 30 or 120 or 300 or 600 or 900)
            {
                Log($"frame={_frame} imported={_imported} failed={_failed}");
            }
        }
        catch (Exception ex)
        {
            _failed++;
            Log($"EX {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Draw(SKCanvas canvas, GRContext grContext, GRBackendTexture backend, GRSurfaceOrigin origin,
        double x, double y, string label)
    {
        try
        {
            using var image = SKImage.FromTexture(grContext, backend, origin, SKColorType.Rgba8888);
            if (image is null)
            {
                _failed++;
                Log($"{label}: image null");
                return;
            }

            canvas.DrawImage(image, new SKRect((float)x, (float)y, (float)(x + TextureSize), (float)(y + TextureSize)));
            _imported++;
        }
        catch (Exception ex)
        {
            _failed++;
            Log($"{label}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void EnsureTexture()
    {
        if (_texture is not null)
        {
            return;
        }

        var device = MTLDevice.SystemDefault;
        if (device is null)
        {
            Log("MTLDevice.SystemDefault null");
            return;
        }

        var descriptor = new MTLTextureDescriptor
        {
            TextureType = MTLTextureType.k2D,
            Width = TextureSize,
            Height = TextureSize,
            MipmapLevelCount = 1,
            PixelFormat = MTLPixelFormat.RGBA8Unorm,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared,
        };

        _texture = device.CreateTexture(descriptor);
        Log(_texture is null
            ? "CreateTexture null"
            : $"texture=0x{_texture.Handle:x} device=0x{device.Handle:x}");
    }

    private bool Upload()
    {
        Fill(_frame * 3);
        try
        {
            _texture!.ReplaceRegion(_region, UIntPtr.Zero, _pixelsHandle.AddrOfPinnedObject(),
                (nuint)(TextureSize * BytesPerPixel));
            return true;
        }
        catch (Exception ex)
        {
            Log($"ReplaceRegion {ex.GetType().Name}: {ex.Message}");
            _texture = null;
            return false;
        }
    }

    /// <summary>
    /// 左上红、左下蓝、左侧绿用于判定期望的 Y 朝向；通道互换会让红蓝对调，因此同一张图能同时诊断朝向与通道序。
    /// </summary>
    private void Fill(int bandOffset)
    {
        var band = bandOffset % (TextureSize - BandThickness);
        for (var y = 0; y < TextureSize; y++)
        {
            for (var x = 0; x < TextureSize; x++)
            {
                byte r = 40, g = 40, b = 60;
                if (y < MarkerThickness)
                {
                    (r, g, b) = (255, 0, 0);
                }
                else if (y >= TextureSize - MarkerThickness)
                {
                    (r, g, b) = (0, 0, 255);
                }
                else if (x < MarkerThickness)
                {
                    (r, g, b) = (0, 255, 0);
                }
                else if (y >= band && y < band + BandThickness)
                {
                    (r, g, b) = (255, 255, 0);
                }
                else
                {
                    g = (byte)(y % 256);
                }

                var i = (y * TextureSize + x) * BytesPerPixel;
                _pixels[i] = r;
                _pixels[i + 1] = g;
                _pixels[i + 2] = b;
                _pixels[i + 3] = 255;
            }
        }
    }

    /// <summary>绘制回调在渲染线程，写 UI 属性必须切回 UI 线程。</summary>
    private void Log(string message)
    {
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

    private sealed class LeaseOperation : ICustomDrawOperation
    {
        private readonly MetalLeaseProbe _owner;

        public LeaseOperation(MetalLeaseProbe owner) => _owner = owner;

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
