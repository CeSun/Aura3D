using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Aura3D.Examples.Localization;
using Avalonia.Interactivity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Examples.Demos;

/// <summary>
/// PBR 的两条管线到底读哪几张贴图、打包约定是什么、以及 <see cref="Material"/> 上哪些开关真的有人看。
/// 这页把「接上了」和「起作用了」分开演示：五张图都会被绑成 uniform，可其中两张在着色器里根本没被采样。
/// 参数行的排版全在 <c>PbrChannelsDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class PbrChannelsDemo : Demo
{
    private readonly bool[] plugged = [true, true, true, false, false];

    // ComboRow.Options 是 IList，XAML 只能绑到公开的列表属性上。
    public IList BlendModeNames { get; } = new List<string> { "Opaque", "Masked", "Translucent" };

    // 材质是纯托管对象，可以在 UI 线程先建好，检视面板的初值因此是真的（BuildScene 跑在其后）。
    private readonly Material hero = new();
    private readonly Material plate = new();

    private Mesh? heroMesh;
    private Mesh? bareMesh;
    private DirectionalLight? sun;

    private int metalRoughSwaps;
    private float alphaCutoff = 0.5f;

    /// <summary>
    /// 建页：装配 XAML，并把参数行的初值对齐到材质与 <c>PipelineSettings</c> 里真正生效的那一份。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public PbrChannelsDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        var settings = Context.Settings;

        IblIntensityRow.Value = settings.IblAmbientIntensity;
        ExposureRow.Value = settings.ToneMappingExposure;

        BlendModeRow.SelectedItem = NameOf(hero.BlendMode);
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 2.8f, 10f);
        scene.MainCamera.LookAt(new Vector3(0, 1.2f, 0));

        sun = new DirectionalLight
        {
            LightColor = System.Drawing.Color.White,
            CastShadow = true,
        };

        sun.RotationDegrees = new Vector3(-40f, -32f, 0);

        scene.AddNode(sun);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(40f, 40f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 20));
        ground.RotationDegrees = new Vector3(-90f, 0, 0);

        scene.AddNode(ground);

        heroMesh = AddMesh("Hero", new SphereGeometry(1.15f, 48, 28), new Vector3(-2.6f, 1.4f, 0), hero);

        // 参考球：只接 BaseColor，用来对照金属度/粗糙度与法线图到底改了什么。
        var bareMaterial = new Material();

        bareMaterial.SetTexture("BaseColor", Procedural.Checker(128, 6));

        bareMesh = AddMesh("Bare", new SphereGeometry(1.15f, 48, 28), new Vector3(0f, 1.4f, 0), bareMaterial);

        plate.SetTexture("BaseColor", Procedural.SoftDot(256));
        plate.BlendMode = BlendMode.Masked;
        plate.AlphaCutoff = alphaCutoff;

        AddMesh("Plate", new PlaneGeometry(2.6f, 2.6f), new Vector3(2.9f, 1.4f, 0), plate);

        ApplyChannels();

        Report();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (heroMesh != null)
            heroMesh.RotationDegrees = new Vector3(0, heroMesh.RotationDegrees.Y + 18f * (float)(deltaTime), 0);

        if (bareMesh != null)
            bareMesh.RotationDegrees = new Vector3(0, bareMesh.RotationDegrees.Y + 18f * (float)(deltaTime), 0);

        if (sun != null)
            sun.RotationDegrees = new Vector3(-40f, (float)(-32f + System.Math.Sin(deltaTime * 0.35) * 25f), 0);

        Report();

        Context.RequestFrame();
    }

    private void OnBaseColorToggled(object? sender, InspectorValueChangedEventArgs e) => SetChannel(0, e);

    private void OnNormalToggled(object? sender, InspectorValueChangedEventArgs e) => SetChannel(1, e);

    private void OnMetallicRoughnessToggled(object? sender, InspectorValueChangedEventArgs e) => SetChannel(2, e);

    private void OnOcclusionToggled(object? sender, InspectorValueChangedEventArgs e) => SetChannel(3, e);

    private void OnEmissiveToggled(object? sender, InspectorValueChangedEventArgs e) => SetChannel(4, e);

    private void OnSwapMetalRough(object? sender, RoutedEventArgs e)
    {
        metalRoughSwaps++;

        ApplyChannels();
    }

    private void OnBlendModeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        hero.BlendMode = (BlendMode)Enum.Parse<BlendMode>((string)e.Value!);
        plate.BlendMode = hero.BlendMode;

        Report();

        Context.InvalidateRender();
    }

    private void OnAlphaCutoffChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        alphaCutoff = (float)e.ValueAs<double>();

        hero.AlphaCutoff = alphaCutoff;
        plate.AlphaCutoff = alphaCutoff;

        Context.InvalidateRender();
    }

    private void OnDoubleSidedToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        hero.DoubleSided = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnIblIntensityChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.IblAmbientIntensity = (float)e.ValueAs<double>();

        Context.InvalidateRender();
    }

    private void OnExposureChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.ToneMappingExposure = (float)e.ValueAs<double>();

        Context.InvalidateRender();
    }

    private void SetChannel(int index, InspectorValueChangedEventArgs e)
    {
        plugged[index] = e.ValueAs<bool>();

        ApplyChannels();
    }

    private Mesh AddMesh(string name, Geometry geometry, Vector3 position, Material material)
    {
        var mesh = new Mesh
        {
            Name = name,
            Geometry = geometry,
            Material = material,
        };

        mesh.Position = position;

        Context.Scene!.AddNode(mesh);

        return mesh;
    }

    private void ApplyChannels()
    {
        // 对调次数决定 MR 图是 glTF 约定还是反过来，方便看出两条分支读的是不同通道。
        var swapped = metalRoughSwaps % 2 == 1;

        hero.SetTexture("BaseColor", plugged[0] ? Procedural.Checker(256, 8) : null);
        hero.SetTexture("Normal", plugged[1] ? Procedural.BumpNormal(256, 5, 2.2f) : null);
        hero.SetTexture("MetallicRoughness", plugged[2] ? MetalRough(swapped) : null);
        hero.SetTexture("Occlusion", plugged[3] ? Procedural.Checker(128, 3, new Procedural.Rgb(250, 250, 250), new Procedural.Rgb(30, 30, 30)) : null);
        hero.SetTexture("Emissive", plugged[4] ? Procedural.SoftDot(128, 1.4f) : null);

        Report();

        Context.InvalidateRender();
    }

    private static Texture MetalRough(bool swapped)
    {
        const int Size = 128;

        var data = new byte[Size * Size * 4];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size;
                float v = y / (float)Size;

                // 左半金属、右半介质；粗糙度沿竖向波，跟 Procedural.MetalRough 同一个花样。
                byte metal = u > 0.5f ? (byte)255 : (byte)0;
                byte rough = (byte)(40 + (MathF.Sin(v * MathF.PI * 4f) * 0.5f + 0.5f) * 180f);

                var i = (y * Size + x) * 4;

                data[i] = 255;
                data[i + 1] = swapped ? metal : rough;
                data[i + 2] = swapped ? rough : metal;
                data[i + 3] = 255;
            }
        }

        return Procedural.FromRgba(data, Size);
    }

    private static string NameOf(BlendMode mode) => mode.ToString();

    private void Report()
    {
        var kind = Context.RequestedPipeline;
        var deferred = kind == PipelineKind.PBRDeferred;
        var pbr = kind is PipelineKind.PBRDeferred or PipelineKind.PBRForward;

        // 只有延迟管线的不透明分支真采样 Emissive；Occlusion 两边都只绑不采。
        var boundNotSampled = Strings.Keys.PbrChannels_BoundNotSampled.T();

        string At(int i) => i switch
        {
            0 => "✓",
            1 => "✓",
            2 => pbr ? "✓" : "×",
            3 => boundNotSampled,
            4 => deferred ? "✓" : boundNotSampled,
            _ => "?",
        };

        var mrConvention = metalRoughSwaps % 2 == 1
            ? Strings.Keys.PbrChannels_MrSwapped.T()
            : Strings.Keys.PbrChannels_MrStandard.T();

        var version = hero.Version;

        Readout.Text = Strings.Keys.PbrChannels_Readout.Format(
            kind.DisplayName(),
            At(0),
            At(1),
            At(2),
            At(3),
            At(4),
            mrConvention,
            alphaCutoff,
            version);
    }
}
