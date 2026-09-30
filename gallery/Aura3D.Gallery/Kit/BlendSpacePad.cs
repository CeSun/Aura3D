using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace Aura3D.Gallery.Kit;

/// <summary>
/// 混合空间的二维编辑板：两轴都是 [-1,+1]，采样点摆在矩形里（本页是十字布局），
/// 蓝色滑块就是当前轴值，按住拖动实时改轴；事件只在用户拖动时触发，
/// 程序改 <see cref="AxisX"/> / <see cref="AxisY"/>（比如自动扫掠）只挪滑块、不发事件，
/// 这样「扫掠驱动 UI」和「用户接管」两条路不会互相打架。
/// 用 Canvas + 形状组合而不是自绘 Render，少依赖一套文本测量 API。
/// </summary>
public sealed class BlendSpacePad : Canvas
{
    /// <summary>一个采样点：轴值坐标 + 剪辑名标签。</summary>
    public sealed record SamplePoint(double X, double Y, string Label);

    // 四周留白：给点位的文字标签留出绘制空间，同时防止边缘的滑块被裁掉一半
    private const double Inset = 22;

    private static readonly IBrush FrameFill = new SolidColorBrush(Color.FromArgb(12, 0, 0, 0));
    private static readonly IBrush FrameStroke = new SolidColorBrush(Color.FromArgb(45, 0, 0, 0));
    private static readonly IBrush GridStroke = new SolidColorBrush(Color.FromArgb(38, 0, 0, 0));
    private static readonly IBrush DotFill = new SolidColorBrush(Color.FromArgb(170, 96, 96, 96));
    private static readonly IBrush DotStroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(210, 70, 70, 70));
    private static readonly IBrush ThumbFill = new SolidColorBrush(Color.FromRgb(47, 124, 246));
    private static readonly IBrush ThumbStroke = Brushes.White;

    private readonly List<SamplePoint> samples = [];

    private Ellipse? thumb;
    private bool layoutDirty = true;

    /// <summary>用户拖动滑块时触发；程序写入轴值不触发。参数是 clamp 后的 (X∈[0,1]，Y∈[-1,1])。</summary>
    public event EventHandler<(double X, double Y)>? AxisChanged;

    public BlendSpacePad()
    {
        MinHeight = 210;

        // Canvas 默认 Background 为 null 不参与命中测试，铺一层透明才接得住鼠标
        Background = Brushes.Transparent;

        Cursor = new Cursor(StandardCursorType.Hand);

        // 首次 SetSamples 时多半还没布局，记个脏标记等第一次有尺寸了再画
        LayoutUpdated += (_, _) =>
        {
            if (layoutDirty && Bounds.Width > 10)
            {
                layoutDirty = false;

                Rebuild();
            }
        };
    }

    /// <summary>设置采样点集合并重画。重复调用即可整体替换。</summary>
    public void SetSamples(IEnumerable<SamplePoint> points)
    {
        samples.Clear();
        samples.AddRange(points);

        Rebuild();
    }

    /// <summary>横轴，-1（左）到 +1（右）。写属性只挪滑块。</summary>
    public double AxisX { get; private set; }

    /// <summary>纵轴，-1（下）到 +1（上）。写属性只挪滑块。</summary>
    public double AxisY { get; private set; }

    /// <summary>程序侧设置轴值（自动扫掠、Reset 用），不发事件。</summary>
    public void SetAxis(double x, double y)
    {
        AxisX = Math.Clamp(x, -1, 1);
        AxisY = Math.Clamp(y, -1, 1);

        PositionThumb();
    }

    private void Rebuild()
    {
        Children.Clear();
        thumb = null;

        var w = Bounds.Width;
        var h = Bounds.Height;

        if (w < 10 || h < 10)
        {
            layoutDirty = true;

            return;
        }

        Children.Add(new Border
        {
            Width = w,
            Height = h,
            Background = FrameFill,
            BorderBrush = FrameStroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        });

        // 两条中轴线：横线是前后轴（Y=0），竖线是左右轴（X=0），采样点全摆在十字上
        double centerX = w / 2;
        double centerY = YToPixel(0, h);

        Children.Add(new Line { StartPoint = new Point(Inset, centerY), EndPoint = new Point(w - Inset, centerY), Stroke = GridStroke, StrokeThickness = 1 });
        Children.Add(new Line { StartPoint = new Point(centerX, Inset), EndPoint = new Point(centerX, h - Inset), Stroke = GridStroke, StrokeThickness = 1 });

        foreach (var sample in samples)
        {
            var (px, py) = ToPixel(sample.X, sample.Y, w, h);

            var dot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = DotFill,
                Stroke = DotStroke,
                StrokeThickness = 1,
            };

            SetLeft(dot, px - 5);
            SetTop(dot, py - 5);

            Children.Add(dot);

            // 标签默认画在点下方，贴近上边缘时翻到下方、贴近下边缘时翻到上方
            double labelTop = py + 8;

            if (py < 42)
                labelTop = py + 8;
            else if (py > h - 26)
                labelTop = py - 24;

            var label = new TextBlock
            {
                Text = sample.Label,
                FontSize = 10,
                Foreground = LabelBrush,
                Width = 110,
                TextAlignment = TextAlignment.Center,
            };

            SetLeft(label, px - 55);
            SetTop(label, labelTop);

            Children.Add(label);
        }

        thumb = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = ThumbFill,
            Stroke = ThumbStroke,
            StrokeThickness = 2,
        };

        Children.Add(thumb);

        PositionThumb();
    }

    private void PositionThumb()
    {
        if (thumb == null)
            return;

        var (px, py) = ToPixel(AxisX, AxisY, Bounds.Width, Bounds.Height);

        SetLeft(thumb, px - 8);
        SetTop(thumb, py - 8);
    }

    private (double X, double Y) ToPixel(double axisX, double axisY, double w, double h)
    {
        double x = XToPixel(axisX, w);
        double y = YToPixel(axisY, h);

        return (x, y);
    }

    private double XToPixel(double axisX, double w) => w / 2 + Math.Clamp(axisX, -1, 1) * Math.Max(1, w / 2 - Inset);

    private double YToPixel(double axisY, double h) => h / 2 - Math.Clamp(axisY, -1, 1) * Math.Max(1, h / 2 - Inset);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed == false)
            return;

        e.Pointer.Capture(this);
        e.Handled = true;

        MoveTo(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (e.Pointer.Captured != this)
            return;

        MoveTo(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (e.Pointer.Captured == this)
            e.Pointer.Capture(null);

        e.Handled = true;
    }

    private void MoveTo(PointerEventArgs e)
    {
        var p = e.GetPosition(this);

        double x = (p.X - Bounds.Width / 2) / Math.Max(1, Bounds.Width / 2 - Inset);
        double y = (Bounds.Height / 2 - p.Y) / Math.Max(1, Bounds.Height / 2 - Inset);

        AxisX = Math.Clamp(x, -1, 1);
        AxisY = Math.Clamp(y, -1, 1);

        PositionThumb();

        AxisChanged?.Invoke(this, (AxisX, AxisY));
    }
}
