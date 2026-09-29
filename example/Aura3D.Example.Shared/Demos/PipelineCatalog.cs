using Aura3D.Core.Renderers;
using Aura3D.Examples.Localization;
using System;
using System.Collections.Generic;

namespace Aura3D.Examples;

/// <summary>
/// 引擎可用的渲染管线。演示页用它声明所需管线，宿主据此装配 <see cref="Aura3D.Avalonia.Aura3DView"/>。
/// </summary>
public enum PipelineKind
{
    /// <summary>核心内置的前向 Blinn-Phong 管线。</summary>
    BlinnPhong,

    /// <summary>无光照管线，只做纹理/顶点色直出，用于图元与调试可视化。</summary>
    NoLight,

    /// <summary>延迟 PBR 管线（GBuffer + IBL + 逐灯 pass）。</summary>
    PBRDeferred,

    /// <summary>前向 PBR 管线。</summary>
    PBRForward,

    /// <summary>卡通描边管线。</summary>
    CelShading,
}

/// <summary>
/// <see cref="PipelineKind"/> 到具体管线类型的映射表。这里集中了全部泛型实例化点，
/// 演示页因此不需要写 XAML 的 <c>x:TypeArguments</c>，管线也能在运行时切换。
/// </summary>
public static class PipelineCatalog
{
    /// <summary>
    /// 取该管线的 <see cref="RenderPipeline"/> 创建委托，交给 <c>Aura3DView.CreateRenderPipeline</c>。
    /// </summary>
    public static Func<Core.Scenes.Scene, RenderPipeline> FactoryOf(PipelineKind kind) => kind switch
    {
        PipelineKind.BlinnPhong => Core.Renderers.BlinnPhongPipeline.CreateInstance,
        PipelineKind.NoLight => Core.Renderers.NoLightPipeline.CreateInstance,
        PipelineKind.PBRDeferred => Aura3D.Pipeline.PBR.PBRDeferredPipeline.CreateInstance,
        PipelineKind.PBRForward => Aura3D.Pipeline.PBRForward.PBRForwardPipeline.CreateInstance,
        PipelineKind.CelShading => Aura3D.Pipeline.CelShading.CelShadingPipeline.CreateInstance,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// 该管线是否依赖 PBR 的 IBL 资源（决定演示页要不要准备 HDRI/辐照度图）。
    /// </summary>
    public static bool UsesIbl(PipelineKind kind) =>
        kind is PipelineKind.PBRDeferred or PipelineKind.PBRForward;

    /// <summary>
    /// UI 上显示的管线名，按当前语言取。
    /// </summary>
    public static string DisplayName(this PipelineKind kind) => kind switch
    {
        PipelineKind.BlinnPhong => Strings.Keys.Pipeline_BlinnPhong.T(),
        PipelineKind.NoLight => Strings.Keys.Pipeline_NoLight.T(),
        PipelineKind.PBRDeferred => Strings.Keys.Pipeline_PBRDeferred.T(),
        PipelineKind.PBRForward => Strings.Keys.Pipeline_PBRForward.T(),
        PipelineKind.CelShading => Strings.Keys.Pipeline_CelShading.T(),
        _ => kind.ToString(),
    };

    /// <summary>
    /// 演示页可用的管线候选：按 <see cref="PipelineKind"/> 声明允许集合，宿主渲染成下拉框。
    /// </summary>
    public static IReadOnlyList<PipelineKind> All { get; } =
    [
        PipelineKind.BlinnPhong,
        PipelineKind.NoLight,
        PipelineKind.PBRDeferred,
        PipelineKind.PBRForward,
        PipelineKind.CelShading,
    ];
}
