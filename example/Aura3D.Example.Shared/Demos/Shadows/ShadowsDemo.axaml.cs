using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Aura3D.Examples.Localization;
using Avalonia.Interactivity;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Examples.Demos;

/// <summary>
/// 阴影：主平行光走级联（CSM），其余平行光与没有级联支持的管线走单张正交图。
/// 这页把两条路径的参数分开，并直接从挂在灯上的 <see cref="CsmShadowData"/> 读回真实生效的分辨率与级联数。
/// 参数行的排版全在 <c>ShadowsDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class ShadowsDemo : Demo
{
    private DirectionalLight? main;
    private DirectionalLight? second;
    private Node? casters;

    private float pitch = -34;
    private float yaw = -22;

    // ShadowConfig 的初值取自引擎默认，检视面板因此在建场景之前也能显示真值。
    private float shadowWidth = 50;
    private float shadowHeight = 50;
    private float shadowNear = 0.1f;
    private float shadowFar = 50f;

    /// <summary>
    /// 建页：装配 XAML，并把参数行的初值对齐到引擎里真正生效的那一份。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public ShadowsDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        var settings = Context.Settings;

        ShadowWidthRow.Value = shadowWidth;
        ShadowHeightRow.Value = shadowHeight;
        ShadowNearRow.Value = shadowNear;
        ShadowFarRow.Value = shadowFar;

        CascadeCountRow.Value = settings.CsmCascadeCount;
        SplitLambdaRow.Value = settings.CsmSplitLambda;
        MapResolutionRow.Value = settings.CsmShadowMapResolution;
        LightLimitRow.Value = settings.DirectionalLightLimit;

        // XAML 里这两个开关画的是勾选态，这里把设置同步成一样的，免得面板与真值不符。
        settings.Debug.ShowDirectionalLight = true;
        settings.Debug.ShowBoundingBox = true;
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 6f, 26f);
        scene.MainCamera.LookAt(new Vector3(0, 0.6f, -6f));
        scene.MainCamera.SetClippingPlanes(0.4f, (float)FarPlaneRow.Value);

        main = new DirectionalLight
        {
            Name = "Main",
            LightColor = System.Drawing.Color.White,
            CastShadow = true,
            Irradiance = 60000,
        };

        ApplyMainRotation();

        scene.AddNode(main);

        // 第二盏也投影，但它不是主光，所以拿的是单张正交图那条路径。
        second = new DirectionalLight
        {
            Name = "Second",
            LightColor = System.Drawing.Color.Orange,
            CastShadow = true,
            Irradiance = 20000,
        };

        second.RotationDegrees = new Vector3(-50f, 120f, 0);

        scene.AddNode(second);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(160f, 160f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(512, 40));
        ground.RotationDegrees = new Vector3(-90f, 0, 0);

        scene.AddNode(ground);

        casters = new Node { Name = "Casters" };

        scene.AddNode(casters);

        // 三组不同距离的立柱：近处、中景、地平线附近，级联边界会在这三段上表现得很清楚。
        AddPillars(6f, 1, new Vector3(-3f, 0, 6f));
        AddPillars(14f, 3, new Vector3(-6f, 0, -8f));
        AddPillars(26f, 6, new Vector3(-14f, 0, -34f));

        var sphere = new Mesh
        {
            Name = "Ball",
            Geometry = new SphereGeometry(1.6f, 44, 24),
            Material = new Material(),
        };

        sphere.Material.SetTexture("BaseColor", Procedural.VerticalGradient(
            new Procedural.Rgb(230, 230, 240), new Procedural.Rgb(120, 130, 160)));
        sphere.Position = new Vector3(4f, 1.6f, 2f);

        casters.AddChild(sphere, AttachToParentRule.KeepLocal);

        ApplyShadowConfig();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        Report();

        Context.RequestFrame();
    }

    private void OnMainPitchChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        pitch = (float)e.ValueAs<double>();

        ApplyMainRotation();

        Context.InvalidateRender();
    }

    private void OnMainYawChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        yaw = (float)e.ValueAs<double>();

        ApplyMainRotation();

        Context.InvalidateRender();
    }

    private void OnMainCastToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (main != null)
            main.CastShadow = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnMainExplicitToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (Context.Scene != null)
            Context.Scene.MainDirectionalLight = e.ValueAs<bool>() ? main : null;

        Report();

        Context.InvalidateRender();
    }

    private void OnShadowWidthChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        shadowWidth = (float)e.ValueAs<double>();

        ApplyShadowConfig();
    }

    private void OnShadowHeightChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        shadowHeight = (float)e.ValueAs<double>();

        ApplyShadowConfig();
    }

    private void OnShadowNearChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        shadowNear = (float)e.ValueAs<double>();

        ApplyShadowConfig();
    }

    private void OnShadowFarChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        shadowFar = (float)e.ValueAs<double>();

        ApplyShadowConfig();
    }

    private void OnCascadeCountChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.CsmCascadeCount = (int)Math.Round(e.ValueAs<double>());

        Report();

        Context.InvalidateRender();
    }

    private void OnSplitLambdaChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.CsmSplitLambda = (float)e.ValueAs<double>();

        Report();

        Context.InvalidateRender();
    }

    private void OnMapResolutionChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.CsmShadowMapResolution = (int)Math.Round(e.ValueAs<double>() / 256) * 256;

        Report();

        Context.InvalidateRender();
    }

    private void OnCameraFarChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Scene?.MainCamera.SetClippingPlanes(0.4f, (float)e.ValueAs<double>());

        Report();

        Context.InvalidateRender();
    }

    private void OnLightLimitChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.DirectionalLightLimit = (int)Math.Round(e.ValueAs<double>());

        Report();

        Context.InvalidateRender();
    }

    private void OnShowDirectionalLightToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowDirectionalLight = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnShowBoundingBoxToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowBoundingBox = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnReload(object? sender, RoutedEventArgs e) =>
        Context.Reload(Context.RequestedPipeline);

    private void ApplyMainRotation()
    {
        if (main != null)
            main.RotationDegrees = new Vector3(pitch, yaw, 0);
    }

    private void ApplyShadowConfig()
    {
        foreach (var light in new[] { main, second })
        {
            if (light == null)
                continue;

            light.ShadowConfig.Width = (int)Math.Round(shadowWidth);
            light.ShadowConfig.Height = (int)Math.Round(shadowHeight);
            light.ShadowConfig.NearPlane = shadowNear;
            light.ShadowConfig.FarPlane = shadowFar;
        }

        Report();

        Context.InvalidateRender();
    }

    private void AddPillars(float spacing, int count, Vector3 origin)
    {
        if (casters == null)
            return;

        for (int i = 0; i < count; i++)
        {
            var height = 1.6f + (i % 3) * 0.9f;

            var pillar = new Mesh
            {
                Name = $"P{count}-{i}",
                Geometry = new BoxGeometry(0.9f, height, 0.9f),
                Material = new Material(),
            };

            pillar.Material.SetTexture("BaseColor", Procedural.Checker(64, 2));
            pillar.Position = origin + new Vector3(i * spacing, height / 2f, 0);

            casters.AddChild(pillar, AttachToParentRule.KeepLocal);
        }
    }

    private void Report()
    {
        var pipeline = Context.Scene?.RenderPipeline;

        if (pipeline == null || main == null)
            return;

        var csm = main.GetPipelineGpuState<CsmShadowData>(nameof(CsmShadowData));

        var single = main.GetPipelineGpuState<RenderTarget>("ShadowMapRenderTarget");

        var mainLightName = Context.Scene!.MainDirectionalLight?.Name ?? Strings.Keys.Shadows_AutoPicked.T();

        Readout.Text = Strings.Keys.Shadows_Readout.Format(
            Context.RequestedPipeline.DisplayName(),
            pipeline.SupportsCSM,
            mainLightName,
            csm?.CascadeCount ?? 0,
            csm?.Resolution ?? 0,
            Context.Settings.CsmCascadeCount,
            Context.Settings.CsmShadowMapResolution,
            single == null ? Strings.Keys.Shadows_SingleNone.T() : $"{single.Width}×{single.Height}",
            (csm?.CascadeSplitDepths.Length ?? 0) - 1);
    }
}
