using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Gallery.Kit;
using Aura3D.Gallery.Localization;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// 视图层的生命周期与 GPU 资源归还：五个场景事件、按需渲染、
/// <c>ReleaseGpuResources</c> / <c>SimulateContextLost</c> / <c>DestroyScene</c> 三条回收路径各自的后果。
/// 这页是唯一会真的把场景销毁掉的示例，所以它也演示了应用侧必须自己接住 <c>SceneInitialized</c>。
/// 参数行的排版全在 <c>GpuLifecycleDemo.axaml</c> 里，这里只剩场景组装、视图事件订阅与按钮回调。
/// </summary>
public sealed partial class GpuLifecycleDemo : Demo
{
    private readonly List<string> lines = [];

    private Aura3DView? view;
    private Node? spinner;

    private int frames;
    private int initializedCount;
    private int updatedCount;
    private int lostCount;
    private int restoredCount;
    private int destroyedCount;

    private bool pendingRebuild;
    private double worstFrame;
    private string lastAction = Strings.Keys.GpuLifecycle_ActionInitial.T();

    /// <summary>
    /// 建页：装配 XAML。这一页的参数行全是动作按钮与一个开关，初值与引擎默认一致，不需要回写。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public GpuLifecycleDemo(DemoContext context) : base(context)
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void ViewAttached(Aura3DView created)
    {
        view = created;

        created.SceneInitialized += OnSceneInitialized;
        created.SceneUpdated += OnSceneUpdated;
        created.ContextLost += OnContextLost;
        created.ContextRestored += OnContextRestored;
        created.SceneDestroyed += OnSceneDestroyed;
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        // 场景可能是刚被 DestroyScene 换掉的新实例，节点字段全部作废，重建从这里开始。
        spinner = null;
        pendingRebuild = false;

        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 3.2f, 11f);
        scene.MainCamera.LookAt(new Vector3(0, 1.2f, 0));
        // 40×40 地面 + 拉远余量，抬过默认 far 100。
        scene.MainCamera.FarPlane = 150f;

        var sun = new DirectionalLight { LightColor = System.Drawing.Color.White };

        sun.RotationDegrees = new Vector3(-46f, -30f, 0);

        scene.AddNode(sun);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(40f, 40f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(128, 16));

        scene.AddNode(ground);

        spinner = new Node { Name = "Spinner" };

        scene.AddNode(spinner);

        for (int i = 0; i < 4; i++)
        {
            var box = new Mesh
            {
                Name = $"Box{i}",
                Geometry = new BoxGeometry(1.1f, 1.1f, 1.1f),
                Material = new Material(),
            };

            box.Material.SetTexture("BaseColor", Procedural.Checker(64, 2 + i * 2));

            var angle = i * MathF.PI / 2f;

            box.Position = new Vector3(MathF.Cos(angle) * 2.6f, 0.85f, MathF.Sin(angle) * 2.6f);

            spinner.AddChild(box, AttachToParentRule.KeepLocal);
        }

        Log(Strings.Keys.GpuLifecycle_LogBuildScene.Format(initializedCount, scene.Nodes.Count));

        Report();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (pendingRebuild)
            BuildScene();

        frames++;

        if (deltaTime > worstFrame)
            worstFrame = deltaTime;

        if (spinner != null)
            spinner.RotationDegrees = new Vector3(0, spinner.RotationDegrees.Y + (float)(deltaTime * 40), 0);

        Report();
    }

    private void OnReleaseGpuResources(object? sender, RoutedEventArgs e)
    {
        lastAction = "ReleaseGpuResources";

        view?.ReleaseGpuResources();
        Context.RequestFrame();
    }

    private void OnSimulateContextLost(object? sender, RoutedEventArgs e)
    {
        lastAction = "SimulateContextLost";
        worstFrame = 0;

        view?.SimulateContextLost();
        Context.RequestFrame();
    }

    private void OnDestroyScene(object? sender, RoutedEventArgs e)
    {
        lastAction = "DestroyScene";

        view?.DestroyScene();
        Context.RequestFrame();
    }

    private void OnClearLog(object? sender, RoutedEventArgs e)
    {
        lines.Clear();

        Report();
    }

    private void OnContinuousRenderToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        var on = e.ValueAs<bool>();

        if (view != null)
            view.AutoRequestNextFrameRendering = on;

        lastAction = on
            ? Strings.Keys.GpuLifecycle_ActionContinuous.T()
            : Strings.Keys.GpuLifecycle_ActionOnDemand.T();

        Context.RequestFrame();
    }

    private void OnRequestOneFrame(object? sender, RoutedEventArgs e)
    {
        lastAction = Strings.Keys.GpuLifecycle_ActionManualFrame.T();

        Context.RequestFrame();
    }

    /// <inheritdoc />
    public override void Unload()
    {
        if (view == null)
            return;

        view.SceneInitialized -= OnSceneInitialized;
        view.SceneUpdated -= OnSceneUpdated;
        view.ContextLost -= OnContextLost;
        view.ContextRestored -= OnContextRestored;
        view.SceneDestroyed -= OnSceneDestroyed;
    }

    private void OnSceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        initializedCount++;

        // 外壳只在第一个场景的首帧调 BuildScene，之后的新场景由这一位接手。
        pendingRebuild = true;

        Log(Strings.Keys.GpuLifecycle_LogInitialized.Format(initializedCount, view?.IsContextLost));
    }

    private void OnSceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        updatedCount++;
    }

    private void OnContextLost(object? sender, ContextLostRoutedEventArgs e)
    {
        lostCount++;

        Log(Strings.Keys.GpuLifecycle_LogContextLost.Format(lostCount));
    }

    private void OnContextRestored(object? sender, ContextRestoredRoutedEventArgs e)
    {
        restoredCount++;

        Log(Strings.Keys.GpuLifecycle_LogContextRestored.Format(restoredCount));
    }

    private void OnSceneDestroyed(object? sender, DestroyedRoutedEventArgs e)
    {
        destroyedCount++;

        Log(Strings.Keys.GpuLifecycle_LogSceneDestroyed.Format(destroyedCount));
    }

    private void Log(string line)
    {
        lines.Add($"f{frames,5}  {line}");

        while (lines.Count > 8)
            lines.RemoveAt(0);
    }

    private void Report()
    {
        var pipeline = Context.Scene?.RenderPipeline;

        var state = Strings.Keys.GpuLifecycle_StateReadout.Format(
            frames,
            initializedCount,
            updatedCount,
            lostCount,
            restoredCount,
            destroyedCount,
            view?.IsContextLost,
            pipeline?.IsInitialized,
            pipeline?.IsDestroyed,
            worstFrame * 1000,
            lastAction);

        Readout.Text = state;

        LogReadout.Text = lines.Count == 0 ? "—" : string.Join("\n", lines);
    }
}
