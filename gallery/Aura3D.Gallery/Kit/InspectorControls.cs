using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Aura3D.Gallery.Kit;

/// <summary>
/// 检视面板行控件共用的事件参数：一个装箱后的新值。
/// 演示页的 XAML 事件处理写 <c>e.ValueAs&lt;double&gt;()</c> 取值。
/// </summary>
public sealed class InspectorValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>创建事件参数。</summary>
    public InspectorValueChangedEventArgs(RoutedEvent routedEvent, object? value)
        : base(routedEvent) => Value = value;

    /// <summary>这一行的新值（滑杆是 double、开关是 bool、下拉是选中的项）。</summary>
    public object? Value { get; }

    /// <summary>按值类型取值。</summary>
    public T ValueAs<T>() => (T)Value!;
}

/// <summary>
/// 检视面板的分组标题。AXAML 里写 <c>&lt;kit:Section Title="级联阴影"/&gt;</c>。
/// </summary>
public class Section : TemplatedControl
{
    /// <summary>标题文本。</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<Section, string>(nameof(Title));

    /// <summary>标题文本。</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}

/// <summary>
/// 一行灰色小字说明。演示页用它写「这个开关只管得着哪一半」一类口径说明。
/// </summary>
public class Hint : TemplatedControl
{
    /// <summary>说明文本。</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<Hint, string>(nameof(Text));

    /// <summary>说明文本。</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}

/// <summary>
/// 一行滑杆：标题与当前值同一行，滑杆独占下一行（窄屏不会被挤成两行标题）。
/// AXAML 用法：
/// <code>
/// &lt;kit:SliderRow Label="级联数" Minimum="1" Maximum="4" Value="4" ValueChanged="OnCascadesChanged"/&gt;
/// </code>
/// 也可以 <c>Value="{Binding Cascades, Mode=TwoWay}"</c> 直接绑到演示页的属性上。
/// </summary>
public class SliderRow : TemplatedControl
{
    /// <summary>依赖的属性：行标题。</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SliderRow, string>(nameof(Label));

    /// <summary>依赖的属性：最小值。</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<SliderRow, double>(nameof(Minimum));

    /// <summary>依赖的属性：最大值。</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<SliderRow, double>(nameof(Maximum), 10d);

    /// <summary>依赖的属性：当前值，双向。</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SliderRow, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>依赖的属性：当前值的显示格式，默认两位小数。</summary>
    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<SliderRow, string>(nameof(Format), "0.00");

    /// <summary>依赖的属性：是否显示刻度。</summary>
    public static readonly StyledProperty<bool> ShowTicksProperty =
        AvaloniaProperty.Register<SliderRow, bool>(nameof(ShowTicks));

    /// <summary>依赖的属性：按 <see cref="Format"/> 排好的当前值文本，供标题行右端显示。</summary>
    public static readonly StyledProperty<string> DisplayValueProperty =
        AvaloniaProperty.Register<SliderRow, string>(nameof(DisplayValue));

    /// <summary>依赖的属性：显示刻度时的间隔。</summary>
    public static readonly StyledProperty<double> TickStepProperty =
        AvaloniaProperty.Register<SliderRow, double>(nameof(TickStep), 1d);

    /// <summary>用户拖动滑杆后触发。代码里改 <see cref="Value"/> 不会触发，避免回环。</summary>
    public static readonly RoutedEvent<InspectorValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<SliderRow, InspectorValueChangedEventArgs>(
            nameof(ValueChanged), RoutingStrategies.Direct);

    static SliderRow()
    {
        ValueProperty.Changed.AddClassHandler<SliderRow>((row, _) => row.UpdateDisplayValue());
        FormatProperty.Changed.AddClassHandler<SliderRow>((row, _) => row.UpdateDisplayValue());
        MinimumProperty.Changed.AddClassHandler<SliderRow>((row, _) => row.UpdateTickStep());
        MaximumProperty.Changed.AddClassHandler<SliderRow>((row, _) => row.UpdateTickStep());
    }

    /// <summary>用户拖动滑杆后触发。</summary>
    public event EventHandler<InspectorValueChangedEventArgs> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>按 <see cref="Format"/> 排好的当前值文本。</summary>
    public string DisplayValue
    {
        get => GetValue(DisplayValueProperty);
        private set => SetValue(DisplayValueProperty, value);
    }

    /// <summary>显示刻度时的间隔。</summary>
    public double TickStep
    {
        get => GetValue(TickStepProperty);
        private set => SetValue(TickStepProperty, value);
    }

    /// <summary>行标题。</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>最小值。</summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>最大值。</summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>当前值。</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>当前值的显示格式。</summary>
    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>是否显示刻度。</summary>
    public bool ShowTicks
    {
        get => GetValue(ShowTicksProperty);
        set => SetValue(ShowTicksProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<Slider>("PART_Slider") is { } slider)
        {
            slider.ValueChanged += (_, args) => RaiseValueChanged(args.NewValue);
        }
    }

    private void RaiseValueChanged(double value) =>
        RaiseEvent(new InspectorValueChangedEventArgs(ValueChangedEvent, value) { Source = this });

    private void UpdateDisplayValue() =>
        SetCurrentValue(DisplayValueProperty, Value.ToString(Format, CultureInfo.CurrentCulture));

    private void UpdateTickStep() =>
        SetCurrentValue(TickStepProperty, Math.Max((Maximum - Minimum) / 10.0, 0.001));
}

/// <summary>
/// 一行开关：标题在左、开关在右。
/// </summary>
public class ToggleRow : TemplatedControl
{
    /// <summary>依赖的属性：行标题。</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ToggleRow, string>(nameof(Label));

    /// <summary>依赖的属性：开关状态，双向。</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<ToggleRow, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>用户拨动后触发。</summary>
    public static readonly RoutedEvent<InspectorValueChangedEventArgs> ToggledEvent =
        RoutedEvent.Register<ToggleRow, InspectorValueChangedEventArgs>(
            nameof(Toggled), RoutingStrategies.Direct);

    /// <summary>用户拨动后触发。</summary>
    public event EventHandler<InspectorValueChangedEventArgs> Toggled
    {
        add => AddHandler(ToggledEvent, value);
        remove => RemoveHandler(ToggledEvent, value);
    }

    /// <summary>行标题。</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>开关状态。</summary>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<ToggleSwitch>("PART_Toggle") is { } toggle)
        {
            toggle.IsCheckedChanged += (_, _) =>
                RaiseEvent(new InspectorValueChangedEventArgs(ToggledEvent, toggle.IsChecked == true)
                {
                    Source = this,
                });
        }
    }
}

/// <summary>
/// 一行下拉：标题在左、下拉在右，选项与选中项都是透传绑定。
/// </summary>
public class ComboRow : TemplatedControl
{
    /// <summary>依赖的属性：行标题。</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ComboRow, string>(nameof(Label));

    /// <summary>依赖的属性：选项集合。</summary>
    public static readonly StyledProperty<IList?> OptionsProperty =
        AvaloniaProperty.Register<ComboRow, IList?>(nameof(Options));

    /// <summary>依赖的属性：选中项，双向。</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<ComboRow, object?>(
            nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>用户换选项后触发，<c>Value</c> 是新的选中项。</summary>
    public static readonly RoutedEvent<InspectorValueChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<ComboRow, InspectorValueChangedEventArgs>(
            nameof(SelectionChanged), RoutingStrategies.Direct);

    /// <summary>用户换选项后触发。</summary>
    public event EventHandler<InspectorValueChangedEventArgs> SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>行标题。</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>选项集合。</summary>
    public IList? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>选中项。</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<ComboBox>("PART_Combo") is { } combo)
        {
            combo.SelectionChanged += (_, _) =>
                RaiseEvent(new InspectorValueChangedEventArgs(SelectionChangedEvent, combo.SelectedItem)
                {
                    Source = this,
                });
        }
    }
}

/// <summary>
/// 一行只读读数：标题在上、等宽字体的值在下，值由演示页每帧改写。
/// AXAML 里绑到演示页自己的属性：<c>&lt;kit:ReadoutRow Label="拾取结果" Text="{Binding PickText}"/&gt;</c>。
/// </summary>
public class ReadoutRow : TemplatedControl
{
    /// <summary>依赖的属性：行标题。</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ReadoutRow, string>(nameof(Label));

    /// <summary>依赖的属性：读数文本。</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ReadoutRow, string>(nameof(Text), "—");

    /// <summary>行标题。</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>读数文本。</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}

/// <summary>
/// 检视面板的容器：右侧浮层、可折叠、内部滚动。内容是演示页 XAML 里那一列行控件。
/// </summary>
public class InspectorPanel : ContentControl
{
    /// <summary>依赖的属性：面板是否展开。</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<InspectorPanel, bool>(nameof(IsExpanded), true);

    /// <summary>面板是否展开。窄屏下演示页不需要管这个，外壳按宽度收放。</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }
}
