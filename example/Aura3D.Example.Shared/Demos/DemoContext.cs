using Aura3D.Avalonia;
using Aura3D.Core.Renderers;
using Aura3D.Examples.Assets;
using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace Aura3D.Examples;

/// <summary>
/// 宿主提供给演示页的运行环境与回调。演示页通过它拿到视图与场景、驱动渲染、
/// 以及在运行时切换渲染管线，而不需要知道外壳与导航的存在。
/// </summary>
public sealed class DemoContext
{
    private readonly Action<PipelineKind> requestPipelineChange;

    internal DemoContext(
        IAssetProvider assets,
        PipelineKind defaultPipeline,
        Action<PipelineKind> onReload)
    {
        Assets = assets;
        RequestedPipeline = defaultPipeline;

        // 网格与坐标轴 gizmo 由 DebugDrawPass 画，而那个 pass 在 Settings.Debug.Enable 为假时直接早退。
        // 示例外壳到处都在用 ShowGrid / ShowAxisGizmo，所以这里统一打开总开关，
        // 各页只调 ShowXxx 细分项，避免出现"勾了没反应"的误导。
        Settings.Debug.Enable = true;

        requestPipelineChange = onReload;
    }

    /// <summary>资产取用后端。</summary>
    public IAssetProvider Assets { get; }

    /// <summary>
    /// 当前功能页要求的渲染管线。<see cref="AttachView"/> 之前可以改写它来换默认管线。
    /// </summary>
    public PipelineKind RequestedPipeline { get; set; }

    /// <summary>
    /// 是否允许用户在检视面板里自行切换管线。演示页如果依赖某条管线的专属特性
    /// （点云、卡通材质扩展），应当设为 <c>false</c>，否则切到 NoLight 会得到全黑的误导结果。
    /// </summary>
    public bool AllowPipelineSwitching { get; set; } = true;

    /// <summary>
    /// 渲染管线的可配置设置。必须在 <see cref="CreateView"/> 之前改完：
    /// 光源上限一类参数会决定着色器变体，管线建好后再改不生效，需要 <see cref="Reload"/>。
    /// </summary>
    public PipelineSettings Settings { get; } = new();

    /// <summary>当前视图，<see cref="CreateView"/> 之后可用。</summary>
    public Aura3DView? View { get; private set; }

    /// <summary>当前场景，视图的首帧初始化回调之后可用。</summary>
    public Aura3D.Core.Scenes.Scene? Scene => View?.Scene;

    /// <summary>当前相机控制器，随视图一并创建。</summary>
    public CameraController? CameraController { get; private set; }

    /// <summary>
    /// 把演示页 XAML 里声明的视图接到本上下文：按 <see cref="RequestedPipeline"/> 装配管线
    /// （AXAML 写的 <c>Pipeline=</c> 只是初值，宿主换管线时以这里为准）、共用同一份
    /// <see cref="Settings"/>，并建出相机控制器。此时还没有 GL 上下文，也不该建场景——
    /// 场景要等 <see cref="Demo.BuildScene"/> 在渲染线程回调里组装。
    /// </summary>
    public void AttachView(Aura3DView view)
    {
        if (view is Kit.DemoView demoView)
            demoView.Pipeline = RequestedPipeline;

        view.PipelineSettings = Settings;

        View = view;
        CameraController = new CameraController(view);
    }

    /// <summary>
    /// 请求以另一条管线（或另一套 <see cref="Settings"/>）重建整页。宿主会重走一遍
    /// 建视图 → 取资产 → 建场景；已下载的资产字节有缓存，代价只剩解码与着色器编译。
    /// </summary>
    public void Reload(PipelineKind pipeline)
    {
        RequestedPipeline = pipeline;

        requestPipelineChange(pipeline);
    }

    /// <summary>
    /// 按需渲染时用：<c>AutoRequestNextFrameRendering</c> 设为 false 后靠这个请求下一帧。
    /// </summary>
    public void RequestFrame() => View?.RequestNextFrameRendering();

    /// <summary>
    /// 场景参数在 UI 线程被改完之后调用：排一帧重绘。GL 调用仍然只发生在渲染回调内，
    /// 这里只是让合成器再调度一次。
    /// </summary>
    public void InvalidateRender() => RequestFrame();

    internal void DisposePerViewResources()
    {
        CameraController?.Dispose();

        CameraController = null;
        View = null;
    }
}
