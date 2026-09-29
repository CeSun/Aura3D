using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Aura3D.Examples.Localization;
using Avalonia.Interactivity;
using Irihi.Lingua;
using System;
using System.Collections;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Examples.Demos;

/// <summary>
/// 背景的两条绘制分支：<c>Scene.Background</c> 是 <c>OneOf&lt;CubeTexture, Texture&gt;</c>，
/// BackgroundPass 按分支号走两条完全不同的路——立方图按视线方向采样天空盒，普通图铺满窗口拉伸。
/// 四种背景全部来自文件资产，和 ibl-environment 页的程序化全景图互为对照。
/// 参数行的排版全在 <c>SkyboxBackgroundDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class SkyboxBackgroundDemo : Demo
{
    private const string IrradianceKey = "IrradianceMap";
    private const string PrefilterKey = "PrefilteredEnvironmentMap";

    private static readonly LinguaKey[] SourceKeys =
    [
        Strings.Keys.Background_SourceEngineDefault,
        Strings.Keys.Background_SourceCubeMap,
        Strings.Keys.Background_SourceHdr,
        Strings.Keys.Background_SourceFlat,
    ];

    /// <summary>背景来源下拉的选项集合，供 XAML 的 <c>Options="{Binding SourceOptions}"</c> 绑定。</summary>
    public IList SourceOptions { get; } = SourceKeys.Select(k => k.T()).ToList();

    private int sourceIndex = 1;
    private uint cubeFaceSize = 128;

    private bool orthographic;
    private float orthoSize = 12f;
    private float fieldOfView = 75f;
    private float farPlane = 100f;

    private CubeTexture fileCube = null!;
    private Texture hdrPanorama = null!;
    private Texture flatImage = null!;

    private Mesh? ball;
    private DirectionalLight? sun;

    /// <summary>
    /// 建页：装配 XAML，并把参数行的初值对齐到引擎里真正生效的那一份。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public SkyboxBackgroundDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        var settings = Context.Settings;

        SourceCombo.SelectedItem = SourceOptions[sourceIndex];
        CubeFaceRow.Value = cubeFaceSize;
        OrthoSizeRow.Value = orthoSize;
        FovRow.Value = fieldOfView;
        FarPlaneRow.Value = farPlane;
        AmbientRow.Value = settings.IblAmbientIntensity;
        ExposureRow.Value = settings.ToneMappingExposure;
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        // 六面按 CubeMapFace 顺序取，helper 内部用的就是 AssetManifest.SkyboxKeys。
        fileCube = await assets.CubeTextureAsync();

        hdrPanorama = await assets.HdrTextureAsync("Hdr1k");

        flatImage = await assets.TextureAsync("BackgroundJpg");
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 3.2f, 13f);
        scene.MainCamera.LookAt(new Vector3(0, 1.4f, 0));

        ApplyCamera(scene.MainCamera);
        ApplyBackground(scene);

        sun = new DirectionalLight
        {
            LightColor = System.Drawing.Color.White,
            Irradiance = 60000,
        };

        sun.RotationDegrees = new Vector3(-42f, -18f, 0);

        scene.AddNode(sun);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(60f, 60f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 24));
        ground.RotationDegrees = new Vector3(-90f, 0, 0);

        scene.AddNode(ground);

        // 一排等宽不等高的方柱：透视下侧棱向内收，正交下保持平行，投影分支一眼可辨。
        for (int i = 0; i < 4; i++)
        {
            float height = 1.6f + i;

            var pillar = new Mesh
            {
                Name = $"Pillar{i}",
                Geometry = new BoxGeometry(1.2f, height, 1.2f),
                Material = new Material(),
            };

            pillar.Material.SetTexture(
                "BaseColor",
                Texture.CreateFromColor(System.Drawing.Color.FromArgb(255, 200 - i * 30, 150, 90 + i * 40)));

            pillar.Position = new Vector3(-5.5f + i * 1.9f, height / 2f, -4f);

            scene.AddNode(pillar);
        }

        // 反射球是本页唯一能看见「哪一支在喂 IBL」的地方。
        var mirror = new Material();

        mirror.SetTexture("BaseColor", Texture.CreateFromColor(System.Drawing.Color.White));
        mirror.SetTexture("MetallicRoughness", Procedural.MetalRoughSolid(1f, 0.06f));

        ball = new Mesh
        {
            Name = "MirrorBall",
            Geometry = new SphereGeometry(1.6f, 48, 32),
            Material = mirror,
        };

        ball.Position = new Vector3(2.6f, 1.7f, 1.5f);

        scene.AddNode(ball);

        Report();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        // 球体慢速自转：让画面持续有帧，也方便看反射横扫过天空内容。
        if (ball != null)
            ball.RotationDegrees = new Vector3(0, ball.RotationDegrees.Y + 10f * (float)deltaTime, 0);

        Report();

        Context.RequestFrame();
    }

    private void OnSourceChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        sourceIndex = Math.Max(0, SourceOptions.IndexOf((string)e.Value!));

        if (Context.Scene != null)
            ApplyBackground(Context.Scene);

        Report();

        Context.InvalidateRender();
    }

    private void OnCubeFaceChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        cubeFaceSize = (uint)Math.Round(e.ValueAs<double>() / 32) * 32;

        if (Context.Scene != null)
            ApplyBackground(Context.Scene);

        Report();

        Context.InvalidateRender();
    }

    private void OnRenderBackgroundToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        var camera = Context.Scene?.MainCamera;

        if (camera != null)
            camera.IsRenderBackground = e.ValueAs<bool>();

        Report();

        Context.InvalidateRender();
    }

    private void OnOrthographicToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        orthographic = e.ValueAs<bool>();

        ApplyCameraIfNeeded();
    }

    private void OnOrthoSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        orthoSize = (float)e.ValueAs<double>();

        ApplyCameraIfNeeded();
    }

    private void OnFovChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        fieldOfView = (float)e.ValueAs<double>();

        ApplyCameraIfNeeded();
    }

    private void OnFarPlaneChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        farPlane = (float)e.ValueAs<double>();

        ApplyCameraIfNeeded();
    }

    private void OnIblAmbientChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.IblAmbientIntensity = (float)e.ValueAs<double>();

        Report();

        Context.InvalidateRender();
    }

    private void OnExposureChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.ToneMappingExposure = (float)e.ValueAs<double>();

        Context.InvalidateRender();
    }

    private void OnSunVisibleToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (sun != null)
            sun.Enable = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnRebake(object? sender, RoutedEventArgs e)
    {
        InvalidateIblCaches();

        Context.InvalidateRender();
    }

    private void ApplyBackground(Aura3D.Core.Scenes.Scene scene)
    {
        switch (sourceIndex)
        {
            case 1:
                scene.Background = fileCube;
                break;
            case 2:
                scene.Background = HDRIToCubeTextureConverter.ConvertFromTexture(hdrPanorama, cubeFaceSize);
                break;
            case 3:
                scene.Background = flatImage;
                break;
            default:
                // 引擎在 Scene 构造里就把这张纯色图当默认背景，分支号走 T1。
                scene.Background = Texture.CreateFromColor(System.Drawing.Color.AliceBlue);
                break;
        }
    }

    private void ApplyCameraIfNeeded()
    {
        var camera = Context.Scene?.MainCamera;

        if (camera == null)
            return;

        ApplyCamera(camera);

        Report();

        Context.InvalidateRender();
    }

    private void ApplyCamera(Camera camera)
    {
        camera.ProjectionType = orthographic ? ProjectionType.Orthographic : ProjectionType.Perspective;
        camera.OrthographicSize = orthoSize;
        camera.FieldOfView = fieldOfView;
        camera.FarPlane = farPlane;
    }

    private void InvalidateIblCaches()
    {
        var camera = Context.Scene?.MainCamera;

        if (camera == null)
            return;

        foreach (var key in new[] { IrradianceKey, PrefilterKey })
        {
            if (camera.GetPipelineGpuState<CubeRenderTarget>(key) is { } target)
                target.Invalidate();
        }
    }

    private void Report()
    {
        var scene = Context.Scene;

        if (scene == null)
            return;

        var camera = scene.MainCamera;

        var branch = scene.Background.IsT0
            ? Strings.Keys.Background_BranchCube.Format(scene.Background.AsT0.Width, scene.Background.AsT0.Height)
            : scene.Background.IsT1
                ? Strings.Keys.Background_BranchImage.Format(scene.Background.AsT1.Width, scene.Background.AsT1.Height)
                : Strings.Keys.Background_BranchNone.T();

        var variant = camera.ProjectionType == ProjectionType.Orthographic
            ? Strings.Keys.Background_VariantOrtho.Format(camera.OrthographicSize)
            : Strings.Keys.Background_VariantPersp.Format(camera.FieldOfView);

        var irradiance = camera.GetPipelineGpuState<CubeRenderTarget>(IrradianceKey);
        var prefilter = camera.GetPipelineGpuState<CubeRenderTarget>(PrefilterKey);

        Readout.Text = Strings.Keys.Background_Readout.Format(
            SourceOptions[sourceIndex],
            branch,
            variant,
            camera.FarPlane,
            camera.IsRenderBackground ? Strings.Keys.Background_RenderOn.T() : Strings.Keys.Background_RenderOff.T(),
            irradiance?.FrameBufferId ?? 0,
            prefilter?.FrameBufferId ?? 0);
    }
}
