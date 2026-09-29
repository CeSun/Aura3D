using Aura3D.Avalonia;
using Avalonia;

namespace Aura3D.Examples.Kit;

/// <summary>
/// 能整份写在 AXAML 里的引擎视图。<br/>
/// <see cref="Aura3DView"/> 选管线靠给 <c>CreateRenderPipeline</c> 赋委托，那是 C# 才写得出的东西；
/// 这里把它包成一个枚举属性，于是宿主页面可以直接写
/// <c>&lt;kit:DemoView Pipeline="PBRForward" EnablePicking="True"/&gt;</c>，
/// 换管线只是改一个属性（改完要 Reload，因为管线类型决定着色器变体）。
/// </summary>
public class DemoView : Aura3DView
{
    /// <summary>依赖的属性：本页用哪条渲染管线。</summary>
    public static readonly StyledProperty<PipelineKind> PipelineProperty =
        AvaloniaProperty.Register<DemoView, PipelineKind>(nameof(Pipeline), PipelineKind.BlinnPhong);

    static DemoView()
    {
        PipelineProperty.Changed.AddClassHandler<DemoView>((view, _) =>
            view.CreateRenderPipeline = PipelineCatalog.FactoryOf(view.Pipeline));
    }

    /// <summary>
    /// 创建视图。默认走 <see cref="PipelineKind.BlinnPhong"/>，XAML 里改 <see cref="Pipeline"/> 即换管线。
    /// </summary>
    public DemoView()
    {
        // XAML 里写的管线如果正好等于默认值就不会触发变更回调，所以构造时先按当前值配一次。
        CreateRenderPipeline = PipelineCatalog.FactoryOf(Pipeline);
    }

    /// <summary>本页用哪条渲染管线。AXAML 里直接写枚举名。</summary>
    public PipelineKind Pipeline
    {
        get => GetValue(PipelineProperty);
        set => SetValue(PipelineProperty, value);
    }
}
