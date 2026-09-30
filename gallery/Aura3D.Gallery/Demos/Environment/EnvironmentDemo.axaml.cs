using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Gallery.Kit;
using Aura3D.Gallery.Localization;
using Avalonia.Interactivity;
using Irihi.Lingua;
using System;
using System.Collections;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// 环境图与 IBL：<see cref="Aura3D.Core.Scenes.Scene.Background"/> 是唯一的环境光源入口，
/// 两条 PBR 管线拿它烘成辐照度图与预滤波反射图并缓存在相机节点上。
/// 五个来源分两类：前两个是文件资产（1k HDRI 全景、六面天空盒），后三个是程序化生成的等距柱状全景图。
/// 文件那一类是这个页面的重点——没挂有内容的立方图时，管线会退到引擎那张纯白立方图
/// （<c>PBRPipelineBase.DefaultIblAmbientCubeTexture</c>），金属只剩一层没有方向的白光，
/// 看上去跟「IBL 没工作」一样。
/// 参数行的排版全在 <c>EnvironmentDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class EnvironmentDemo : Demo
{
    private const string IrradianceKey = "IrradianceMap";
    private const string PrefilterKey = "PrefilteredEnvironmentMap";

    private const string HdrKey = "Hdr1k";

    private static readonly LinguaKey[] EnvKeys =
    [
        Strings.Keys.Environment_EnvHdri,
        Strings.Keys.Environment_EnvSkyboxCube,
        Strings.Keys.Environment_EnvStudio,
        Strings.Keys.Environment_EnvSunset,
        Strings.Keys.Environment_EnvTestChart,
    ];

    /// <summary>
    /// 前两个来源直接来自文件，程序化全景图从这一档开始。索引减去它才是 <see cref="BuildPanorama"/> 的花样号。
    /// </summary>
    private const int FirstProceduralIndex = 2;

    /// <summary>全景图下拉的选项集合，供 XAML 的 <c>Options="{Binding PanoramaOptions}"</c> 绑定。</summary>
    public IList PanoramaOptions { get; } = EnvKeys.Select(k => k.T()).ToList();

    private int envIndex;
    private uint faceSize = 128;

    private Texture hdrPanorama = null!;
    private CubeTexture fileCube = null!;

    private Node? ballRow;
    private DirectionalLight? sun;

    private bool rebaked;

    /// <summary>
    /// 建页：装配 XAML，并把参数行的初值对齐到引擎里真正生效的那一份。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public EnvironmentDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        var settings = Context.Settings;

        PanoramaCombo.SelectedItem = PanoramaOptions[envIndex];
        FaceSizeRow.Value = faceSize;
        AmbientRow.Value = settings.IblAmbientIntensity;
        ExposureRow.Value = settings.ToneMappingExposure;
        ClampRow.Value = settings.BrightnessClamp;
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        // 等距柱状全景：烘之前先整张解出来，一次烘完就是立方图。
        hdrPanorama = await assets.HdrTextureAsync(HdrKey);

        // 六面天空盒：helper 内部按 AssetManifest.SkyboxKeys 的顺序取六张 jpg。
        fileCube = await assets.CubeTextureAsync();
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 2.4f, 9.5f);
        scene.MainCamera.LookAt(new Vector3(0, 1.2f, 0));
        // 40×40 地面 + 拉远余量，抬过默认 far 100。
        scene.MainCamera.FarPlane = 150f;

        ApplyEnvironment(scene);

        sun = new DirectionalLight
        {
            LightColor = System.Drawing.Color.White,
            CastShadow = true,
            Irradiance = 30000,
        };

        sun.RotationDegrees = new Vector3(-38f, -25f, 0);

        scene.AddNode(sun);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(40f, 40f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 18));

        scene.AddNode(ground);

        ballRow = new Node { Name = "Balls" };

        scene.AddNode(ballRow);

        // 五档金属度×粗糙度组合。引擎没有 metallicFactor/roughnessFactor 这类数值因子，
        // 只有 MetallicRoughness 这一张图：G=粗糙、B=金属，所以每档都得现做一张纯色图。
        (float Metal, float Rough)[] combos =
        [
            (0f, 0.1f),
            (0f, 0.85f),
            (0.5f, 0.35f),
            (1f, 0.05f),
            (1f, 0.7f),
        ];

        for (int i = 0; i < combos.Length; i++)
        {
            var material = new Material();

            material.SetTexture("BaseColor", Procedural.Checker(64, 3));
            material.SetTexture("MetallicRoughness", Procedural.MetalRoughSolid(combos[i].Metal, combos[i].Rough));

            var ball = new Mesh
            {
                Name = $"M{combos[i].Metal:0.##}R{combos[i].Rough:0.##}",
                Geometry = new SphereGeometry(0.85f, 44, 24),
                Material = material,
            };

            ball.Position = new Vector3((i - 2) * 2.1f, 1.1f, 0);

            ballRow.AddChild(ball, AttachToParentRule.KeepLocal);
        }

        Report();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (ballRow != null)
            ballRow.RotationDegrees = new Vector3(0, ballRow.RotationDegrees.Y + 8f * (float)deltaTime, 0);

        Report();

        Context.RequestFrame();
    }

    private void OnPanoramaChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        envIndex = Math.Max(0, PanoramaOptions.IndexOf((string)e.Value!));

        if (Context.Scene != null)
            ApplyEnvironment(Context.Scene);

        Report();

        Context.InvalidateRender();
    }

    private void OnFaceSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        faceSize = (uint)Math.Round(e.ValueAs<double>() / 32) * 32;

        if (Context.Scene != null)
            ApplyEnvironment(Context.Scene);

        Context.InvalidateRender();
    }

    private void OnRebake(object? sender, RoutedEventArgs e)
    {
        InvalidateIblCaches();

        rebaked = true;

        Report();

        Context.InvalidateRender();
    }

    private void OnReload(object? sender, RoutedEventArgs e) =>
        Context.Reload(Context.RequestedPipeline);

    private void OnIblAmbientChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.IblAmbientIntensity = (float)e.ValueAs<double>();

        Context.InvalidateRender();
    }

    private void OnSunVisibleToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (sun != null)
            sun.Enable = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnExposureChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.ToneMappingExposure = (float)e.ValueAs<double>();

        Context.InvalidateRender();
    }

    private void OnBrightnessClampChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.BrightnessClamp = (float)e.ValueAs<double>();

        Context.InvalidateRender();
    }

    private void OnRenderBackgroundToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (Context.Scene != null)
            Context.Scene.MainCamera.IsRenderBackground = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void ApplyEnvironment(Aura3D.Core.Scenes.Scene scene)
    {
        scene.Background = envIndex switch
        {
            // 真实 HDRI：文件里是线性 HDR 值，烘之前不能再走一次 gamma 解码（加载器默认就是 false）。
            0 => HDRIToCubeTextureConverter.ConvertFromTexture(hdrPanorama, faceSize),

            // 六面天空盒已经是立方图，直接用；面边长由那六张图自己决定。
            1 => fileCube,

            // 程序化全景图：花样的编号从 FirstProceduralIndex 起算。
            _ => HDRIToCubeTextureConverter.ConvertFromTexture(
                BuildPanorama(envIndex - FirstProceduralIndex), faceSize),
        };
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

    private static Texture BuildPanorama(int which)
    {
        const int Width = 256;
        const int Height = 128;

        var data = new float[Width * Height * 4];

        for (int y = 0; y < Height; y++)
        {
            float v = y / (float)(Height - 1);

            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)(Width - 1);

                Vector3 color;

                switch (which)
                {
                    case 1:
                        color = Sunset(u, v);
                        break;
                    case 2:
                        color = TestPattern(u, v);
                        break;
                    default:
                        color = Studio(u, v);
                        break;
                }

                var i = (y * Width + x) * 4;

                data[i] = color.X;
                data[i + 1] = color.Y;
                data[i + 2] = color.Z;
                data[i + 3] = 1f;
            }
        }

        var texture = new Texture();

        texture.SetHdrData(data, Width, Height);
        texture.SetColorFormat(ColorFormat.RGBA);

        // 全景图是线性 HDR 数据，标成 gamma 空间会被再解码一次，反射会明显发白。
        texture.SetIsGammaSpace(false);

        texture.MinFilter = TextureFilterMode.Linear;
        texture.MagFilter = TextureFilterMode.Linear;
        texture.WrapS = TextureWrapMode.Repeat;
        texture.WrapT = TextureWrapMode.ClampToEdge;

        return texture;
    }

    private static Vector3 Studio(float u, float v)
    {
        // 上部天光渐变 + 两条水平柔光箱，给反射一个可读的形状。
        var sky = Vector3.Lerp(new Vector3(0.12f, 0.16f, 0.24f), new Vector3(0.55f, 0.6f, 0.7f), 1f - v);

        var band = 0f;

        if (MathF.Abs(v - 0.3f) < 0.05f)
            band += 3.5f;

        if (MathF.Abs(v - 0.55f) < 0.03f)
            band += 1.8f;

        return sky + new Vector3(band);
    }

    private static Vector3 Sunset(float u, float v)
    {
        var horizon = new Vector3(0.55f, 0.35f, 0.28f);
        var zenith = new Vector3(0.05f, 0.1f, 0.22f);

        var sky = Vector3.Lerp(zenith, horizon, MathF.Pow(1f - v, 2f));

        // 一个小而极亮的太阳盘：HDR 值远大于 1，曝光与亮度上限就是为它准备的。
        var du = MathF.Abs(u - 0.25f);
        var dv = MathF.Abs(v - 0.42f);

        if (du < 0.03f && dv < 0.05f)
            sky += new Vector3(40f, 34f, 24f);

        // 地面一侧压暗，避免整个球被当成环境光。
        if (v > 0.62f)
            sky *= 0.25f;

        return sky;
    }

    private static Vector3 TestPattern(float u, float v)
    {
        // 经线八格、纬线四格，立方图六个面的朝向与接缝一眼可辨。
        var sector = (int)(u * 8f) + (int)(v * 4f) * 8;

        var r = (sector % 2) * 0.8f + 0.15f;
        var g = ((sector >> 1) % 2) * 0.8f + 0.15f;
        var b = ((sector >> 2) % 2) * 0.8f + 0.15f;

        return new Vector3(r, g, b);
    }

    private void Report()
    {
        var scene = Context.Scene;

        if (scene == null)
            return;

        var camera = scene.MainCamera;

        var irradiance = camera.GetPipelineGpuState<CubeRenderTarget>(IrradianceKey);
        var prefilter = camera.GetPipelineGpuState<CubeRenderTarget>(PrefilterKey);

        var background = scene.Background.IsT0
            ? Strings.Keys.Environment_BackgroundCube.Format(
                scene.Background.AsT0.Width, scene.Background.AsT0.Height)
            : scene.Background.IsT1
                ? Strings.Keys.Environment_BackgroundPlain.T()
                : Strings.Keys.Environment_BackgroundNone.T();

        Readout.Text = Strings.Keys.Environment_Readout.Format(
            background,
            PanoramaOptions[envIndex],
            faceSize,
            irradiance?.FrameBufferId ?? 0,
            prefilter?.FrameBufferId ?? 0,
            Context.Settings.IblAmbientIntensity,
            Context.Settings.ToneMappingExposure,
            rebaked
                ? Strings.Keys.Environment_Rebaked.T()
                : Strings.Keys.Environment_NotRebaked.T());
    }
}
