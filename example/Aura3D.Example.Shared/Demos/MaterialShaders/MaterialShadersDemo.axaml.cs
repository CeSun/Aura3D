using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Aura3D.Examples.Localization;
using Irihi.Lingua;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Examples.Demos;

/// <summary>
/// 材质的三件事：通道（<c>SetTexture</c> → 同名 <c>*Texture</c> uniform）、
/// 自定义着色器覆盖（按 pass 类名挂 GLSL，可只换片元）、材质参数（<c>SetParameterValue</c> → 同名 uniform）。
/// 全页零外部资产，贴图由 <see cref="Procedural"/> 现算。
/// 参数行的排版全在 <c>MaterialShadersDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class MaterialShadersDemo : Demo
{
    /// <summary>被覆盖的 pass 类名；Blinn-Phong 主管线只有这一个着色 pass。</summary>
    private const string Pass = "LightPass";

    private static readonly LinguaKey[] BaseColorKeys =
        [Strings.Keys.MaterialShaders_MapChecker, Strings.Keys.MaterialShaders_MapSoftDot, Strings.Keys.MaterialShaders_MapGradient];

    /// <summary>BaseColor 下拉的选项集合，供 XAML 绑定。</summary>
    public IList BaseColorOptions { get; } = BaseColorKeys.Select(k => k.T()).ToList();

    /// <summary>BlendMode 下拉的选项集合，供 XAML 绑定。</summary>
    public IList BlendModeOptions { get; } = Enum.GetValues<BlendMode>();

    private readonly Dictionary<int, Texture> swappableTextures = new();

    private Node? row;
    private Mesh? litMesh;
    private Mesh? switchMesh;
    private Mesh? texturedMesh;
    private Mesh? stripeMesh;

    private Material? engineMaterial;
    private Material? solidMaterial;
    private Material? texturedMaterial;
    private Material? stripeMaterial;
    private Material? maskedMaterial;
    private Material? translucentMaterial;

    private Texture? bumpTexture;
    private Texture? dotTexture;

    private double spin = 14;
    private double hue;
    private double tintStrength = 0.55;
    private double stripeSpeed = 0.9;
    private double stripeFrequencyU = 6;
    private double stripeFrequencyV = 2;
    private double stripeCutoff = 0.5;
    private double alphaCutoff = 0.35;
    private bool useCustomSolid = true;
    private bool showNormal;
    private bool animateTime;

    private float clock;

    /// <summary>
    /// 建页：装配 XAML。滑杆行的初值都是本页常量，直接写在 .axaml 里；
    /// 两个下拉的选中项在这里对齐到默认值。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public MaterialShadersDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        BaseColorCombo.SelectedItem = BaseColorOptions[0];
        BlendModeCombo.SelectedItem = BlendMode.Masked;
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 2.4f, 11.5f);
        scene.MainCamera.LookAt(new Vector3(0, 2.1f, 0.4f));

        var light = new DirectionalLight { LightColor = System.Drawing.Color.White };

        light.RotationDegrees = new Vector3(-48f, -28f, 0);

        scene.AddNode(light);

        swappableTextures[0] = Procedural.Checker(128, 4);
        swappableTextures[1] = Procedural.SoftDot(128);
        swappableTextures[2] = Procedural.VerticalGradient(width: 32, height: 256);

        bumpTexture = Procedural.BumpNormal(128, 5);
        dotTexture = Procedural.SoftDot(128, 1.6f);

        engineMaterial = new Material();
        engineMaterial.SetTexture("BaseColor", swappableTextures[0]);

        solidMaterial = Shaders.Solid(Pass, ColorOf(hue));
        texturedMaterial = Shaders.Textured(Pass, swappableTextures[0], TintOf(tintStrength));
        stripeMaterial = Shaders.Stripes(Pass, new Vector4(0.15f, 0.45f, 0.85f, 1f), new Vector2((float)stripeFrequencyU, (float)stripeFrequencyV));
        stripeMaterial.AlphaCutoff = (float)stripeCutoff;

        maskedMaterial = Shaders.Textured(Pass, dotTexture, new Vector4(1f, 0.72f, 0.25f, 1f));
        maskedMaterial.BlendMode = BlendMode.Masked;
        maskedMaterial.AlphaCutoff = (float)alphaCutoff;

        translucentMaterial = Shaders.Textured(Pass, dotTexture, new Vector4(0.35f, 0.9f, 0.65f, 1f));
        translucentMaterial.BlendMode = BlendMode.Translucent;
        translucentMaterial.AlphaCutoff = (float)alphaCutoff;

        row = new Node { Name = "Row" };

        scene.AddNode(row);

        // 上排：三颗球对比"引擎着色器 / 自定义常量色 / 贴图×染色"。
        litMesh = AddSphere(-2.3f, 3.1f, 0f, engineMaterial);
        switchMesh = AddSphere(0f, 3.1f, 0f, solidMaterial);
        texturedMesh = AddSphere(2.3f, 3.1f, 0f, texturedMaterial);

        // 下排：参数驱动的程序化条纹，以及同一张贴图在 Masked 与 Translucent 下的两种透明路径。
        stripeMesh = AddSphere(-2.3f, 1.15f, 1.6f, stripeMaterial);
        AddQuad(0f, 1.15f, 1.6f, maskedMaterial);
        AddQuad(2.3f, 1.15f, 1.6f, translucentMaterial);
    }

    private void OnBaseColorChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        var material = litMesh?.Material ?? engineMaterial;

        material?.SetTexture("BaseColor", swappableTextures[Math.Max(0, BaseColorOptions.IndexOf((string)e.Value!))]);

        Context.InvalidateRender();
    }

    private void OnNormalToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        showNormal = e.ValueAs<bool>();

        var material = litMesh?.Material ?? engineMaterial;

        material?.SetTexture("Normal", showNormal ? bumpTexture : null);

        Context.InvalidateRender();
    }

    private void OnUseCustomSolidToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        useCustomSolid = e.ValueAs<bool>();

        if (switchMesh != null)
            switchMesh.Material = useCustomSolid ? solidMaterial! : engineMaterial!;

        Context.InvalidateRender();
    }

    private void OnHueChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        hue = e.ValueAs<double>();

        solidMaterial?.SetParameterValue("uColor", ColorOf(hue));

        Context.InvalidateRender();
    }

    private void OnTintChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        tintStrength = e.ValueAs<double>();

        texturedMaterial?.SetParameterValue("uColor", TintOf(tintStrength));

        Context.InvalidateRender();
    }

    private void OnStripeSpeedChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        stripeSpeed = e.ValueAs<double>();
    }

    private void OnStripeFrequencyUChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        stripeFrequencyU = e.ValueAs<double>();

        ApplyStripe();
    }

    private void OnStripeFrequencyVChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        stripeFrequencyV = e.ValueAs<double>();

        ApplyStripe();
    }

    private void OnBlendModeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        if (stripeMaterial != null)
            stripeMaterial.BlendMode = (BlendMode)e.Value!;

        Context.InvalidateRender();
    }

    private void OnStripeCutoffChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        stripeCutoff = e.ValueAs<double>();

        if (stripeMaterial != null)
            stripeMaterial.AlphaCutoff = (float)stripeCutoff;

        Context.InvalidateRender();
    }

    private void OnAnimateTimeToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        animateTime = e.ValueAs<bool>();
    }

    private void OnAlphaCutoffChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        alphaCutoff = e.ValueAs<double>();

        if (maskedMaterial != null)
            maskedMaterial.AlphaCutoff = (float)alphaCutoff;

        if (translucentMaterial != null)
            translucentMaterial.AlphaCutoff = (float)alphaCutoff;

        Context.InvalidateRender();
    }

    private void OnDoubleSidedToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (translucentMaterial != null)
            translucentMaterial.DoubleSided = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnSpinChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        spin = e.ValueAs<double>();
    }

    private void OnShowGridToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (Context.Scene != null)
            Context.Scene.ShowGrid = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (row != null && spin > 0)
            row.RotationDegrees = new Vector3(0f, (float)(spin * deltaTime), 0f);

        if (animateTime)
        {
            clock += (float)(deltaTime * stripeSpeed);

            stripeMaterial?.SetParameterValue("uTime", clock);
        }

        Readout.Text = Strings.Keys.MaterialShaders_Readout.Format(
            deltaTime * 1000.0, clock, Pass, alphaCutoff);

        Context.RequestFrame();
    }

    /// <summary>色相环取色，饱和度与亮度固定为满值。</summary>
    private static Vector4 ColorOf(double hueDegrees)
    {
        var x = (float)(hueDegrees / 360.0) % 1f * 6f;

        static float Channel(float value) => MathF.Min(1f, MathF.Max(0f, value));

        return new Vector4(
            Channel(MathF.Abs(x - 3f) - 1f),
            Channel(2f - MathF.Abs(x - 2f)),
            Channel(2f - MathF.Abs(x - 4f)),
            1f);
    }

    private static Vector4 TintOf(double strength)
    {
        var k = (float)strength;

        return new Vector4(1f - k * 0.75f, 1f - k * 0.1f, 1f - k * 0.55f, 1f);
    }

    private void ApplyStripe()
    {
        stripeMaterial?.SetParameterValue(
            "uStripe",
            new Vector2((float)stripeFrequencyU, (float)stripeFrequencyV));

        Context.InvalidateRender();
    }

    private Mesh AddSphere(float x, float y, float z, Material material)
    {
        var mesh = new Mesh
        {
            Name = $"sphere-{x}-{y}",
            Geometry = new SphereGeometry(0.82f, 40, 24),
            Material = material,
        };

        mesh.Position = new Vector3(x, y, z);

        row!.AddChild(mesh, AttachToParentRule.KeepLocal);

        return mesh;
    }

    private Mesh AddQuad(float x, float y, float z, Material material)
    {
        var mesh = new Mesh
        {
            Name = $"quad-{x}-{y}",
            Geometry = new PlaneGeometry(1.7f, 1.7f),
            Material = material,
        };

        mesh.Position = new Vector3(x, y, z);

        row!.AddChild(mesh, AttachToParentRule.KeepLocal);

        return mesh;
    }
}
