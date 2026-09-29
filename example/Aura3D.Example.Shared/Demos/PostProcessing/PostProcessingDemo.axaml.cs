using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Aura3D.Examples.Localization;
using Avalonia.Interactivity;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Examples.Demos;

/// <summary>
/// <c>PipelineSettings</c> 的每一项到底被谁读：本页把「逐帧生效」与「必须重建管线才生效」两类参数分开，
/// 并直接按当前管线列出每个滑杆是否有人读——很多参数在错的管线上拧起来毫无反应，这本身就是要演示的行为。
/// 参数行的排版全在 <c>PostProcessingDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class PostProcessingDemo : Demo
{
    private DirectionalLight? sun;
    private Node? brightRow;

    private bool needsRebuildHint;

    /// <summary>
    /// 建页：装配 XAML，并把每行的初值对齐到 <see cref="DemoContext.Settings"/> 里真正生效的那一份。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public PostProcessingDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        var settings = Context.Settings;

        ExposureRow.Value = settings.ToneMappingExposure;
        BrightnessClampRow.Value = settings.BrightnessClamp;
        AmbientRow.Value = settings.AmbientIntensity;
        IblAmbientRow.Value = settings.IblAmbientIntensity;

        FxaaRow.IsChecked = settings.EnableFxaa;
        CullingRow.IsChecked = settings.EnableFrustumCulling;

        DirectionalLimitRow.Value = settings.DirectionalLightLimit;
        PointLimitRow.Value = settings.PointLightLimit;
        SpotLimitRow.Value = settings.SpotLightLimit;
        CascadeCountRow.Value = settings.CsmCascadeCount;
        SplitLambdaRow.Value = settings.CsmSplitLambda;
        MapResolutionRow.Value = settings.CsmShadowMapResolution;
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 3.4f, 13f);
        scene.MainCamera.LookAt(new Vector3(0, 1.6f, 0));

        sun = new DirectionalLight
        {
            LightColor = System.Drawing.Color.White,
            CastShadow = true,
            Irradiance = 120000,
        };

        sun.RotationDegrees = new Vector3(-42f, -28f, 0);

        scene.AddNode(sun);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(70f, 70f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 24));

        scene.AddNode(ground);

        brightRow = new Node { Name = "Bright" };

        scene.AddNode(brightRow);

        // 三档亮度递增的球，用来观察曝光与亮度上限各自截在哪里。
        for (int i = 0; i < 3; i++)
        {
            var mesh = new Mesh
            {
                Name = $"Ball{i}",
                Geometry = new SphereGeometry(1f, 40, 24),
                Material = new Material(),
            };

            mesh.Material.SetTexture("BaseColor", Procedural.Checker(128, 2 + i * 3));
            mesh.Position = new Vector3((i - 1) * 2.8f, 1.4f, -1.4f);

            brightRow.AddChild(mesh, AttachToParentRule.KeepLocal);
        }

        // 两盏点光，验证点光上限与实际可画的光数：上限低于场景里的灯数时后面的灯会被丢掉。
        for (int i = 0; i < 2; i++)
        {
            var point = new PointLight
            {
                LightColor = i == 0 ? System.Drawing.Color.DeepSkyBlue : System.Drawing.Color.OrangeRed,
                LuminousIntensity = 9000,
                AttenuationRadius = 12f,
            };

            point.Position = new Vector3(i == 0 ? -4.5f : 4.5f, 2.6f, 2.2f);

            scene.AddNode(point);
        }

        ApplyLive();

        Report();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (brightRow != null)
            brightRow.RotationDegrees = new Vector3(0, (float)(deltaTime * 6) + brightRow.RotationDegrees.Y, 0);

        Report();

        Context.RequestFrame();
    }

    private void OnExposureChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.ToneMappingExposure = (float)e.ValueAs<double>();

        ApplyLive();
    }

    private void OnBrightnessClampChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.BrightnessClamp = (float)e.ValueAs<double>();

        ApplyLive();
    }

    private void OnAmbientIntensityChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.AmbientIntensity = (float)e.ValueAs<double>();

        ApplyLive();
    }

    private void OnIblAmbientIntensityChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.IblAmbientIntensity = (float)e.ValueAs<double>();

        ApplyLive();
    }

    private void OnFxaaToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.EnableFxaa = e.ValueAs<bool>();

        ApplyLive();
    }

    private void OnCullingToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.EnableFrustumCulling = e.ValueAs<bool>();

        ApplyLive();
    }

    private void OnDirectionalLimitChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.DirectionalLightLimit = (int)e.ValueAs<double>();
    }

    private void OnPointLimitChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.PointLightLimit = (int)e.ValueAs<double>();
    }

    private void OnSpotLimitChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.SpotLightLimit = (int)e.ValueAs<double>();
    }

    private void OnCascadeCountChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.CsmCascadeCount = (int)e.ValueAs<double>();
    }

    private void OnSplitLambdaChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.CsmSplitLambda = (float)e.ValueAs<double>();
    }

    private void OnMapResolutionChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.CsmShadowMapResolution = (int)e.ValueAs<double>();
    }

    private void OnReload(object? sender, RoutedEventArgs e)
    {
        needsRebuildHint = true;

        Context.Reload(Context.RequestedPipeline);
    }

    private void OnCastShadowToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (sun != null)
            sun.CastShadow = e.ValueAs<bool>();

        ApplyLive();
    }

    // Settings 这个实例在 CreateView 时就交给了视图与管线，改字段即改配置，只需要排一帧重绘。
    private void ApplyLive() => Context.InvalidateRender();

    private void Report()
    {
        var kind = Context.RequestedPipeline;

        var pbr = kind is PipelineKind.PBRDeferred or PipelineKind.PBRForward;
        var ambient = kind is PipelineKind.BlinnPhong or PipelineKind.CelShading;
        var shadow = kind is not PipelineKind.NoLight;

        var settings = Context.Settings;

        MatrixReadout.Text = Strings.Keys.PostProcessing_MatrixReadout.Format(
            kind,
            pbr ? "✓" : Strings.Keys.PostProcessing_ExposureNoTone.T(),
            ambient ? "✓" : "×",
            pbr ? "✓" : "×",
            shadow ? "✓" : Strings.Keys.PostProcessing_CsmNoShadowMap.T(),
            settings.PointLightLimit,
            settings.CsmCascadeCount,
            settings.CsmShadowMapResolution,
            needsRebuildHint
                ? Strings.Keys.PostProcessing_HintRebuilt.T()
                : Strings.Keys.PostProcessing_HintNeedsReload.T());
    }
}
