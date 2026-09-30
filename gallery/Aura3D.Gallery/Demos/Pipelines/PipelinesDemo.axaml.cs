using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Gallery.Kit;
using Aura3D.Gallery.Localization;
using System;
using System.Collections;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// 同一幕场景在五条管线下的差别。页里的每个对象都刻意只喂某一种信息
/// （平涂、法线贴图、金属度粗糙度、遮罩、半透明），
/// 于是在宿主状态栏里换管线就能看出「谁读了哪张贴图、谁压根不读」。
/// 参数行的排版全在 <c>PipelinesDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class PipelinesDemo : Demo
{
    private Node? row;
    private DirectionalLight? sun;
    private PointLight? point;

    private double sunAngle = -46;

    /// <summary>建页：装配 XAML。</summary>
    /// <param name="context">宿主环境。</param>
    public PipelinesDemo(DemoContext context) : base(context) => InitializeComponent();

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 2.6f, 12.5f);
        scene.MainCamera.LookAt(new Vector3(0, 1.1f, 0));
        // 40×40 地面 + 拉远余量，抬过默认 far 100。
        scene.MainCamera.FarPlane = 150f;

        scene.ShowGrid = true;

        sun = new DirectionalLight { LightColor = System.Drawing.Color.White };

        sun.RotationDegrees = new Vector3((float)sunAngle, -28f, 0);

        scene.AddNode(sun);

        point = new PointLight
        {
            LightColor = System.Drawing.Color.Orange,
            LuminousIntensity = 5000,
            AttenuationRadius = 10f,
        };

        point.Position = new Vector3(3.6f, 2.4f, 3.2f);

        scene.AddNode(point);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(40f, 40f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 20));

        scene.AddNode(ground);

        row = new Node { Name = "Row" };

        scene.AddNode(row);

        Add(Strings.Keys.Pipelines_NodeFlat.T(), new SphereGeometry(0.9f, 36, 20), -5f, m => m.SetTexture("BaseColor", Procedural.Checker(128, 6)));
        Add(Strings.Keys.Pipelines_NodeNormal.T(), new SphereGeometry(0.9f, 36, 20), -3f, m =>
        {
            m.SetTexture("BaseColor", Procedural.Checker(128, 5));
            m.SetTexture("Normal", Procedural.BumpNormal(256, 7));
        });
        Add("③ MR", new SphereGeometry(0.9f, 36, 20), -1f, m =>
        {
            m.SetTexture("BaseColor", Procedural.Checker(128, 4, new Procedural.Rgb(220, 120, 60), new Procedural.Rgb(60, 60, 70)));
            m.SetTexture("MetallicRoughness", Procedural.MetalRough(128));
        });
        Add(Strings.Keys.Pipelines_NodeMask.T(), new CylinderGeometry(0.75f, 0.75f, 1.8f, 40), 1f, m =>
        {
            m.SetTexture("BaseColor", Procedural.SoftDot(128));
            m.BlendMode = BlendMode.Masked;
            m.AlphaCutoff = 0.35f;
        });
        Add(Strings.Keys.Pipelines_NodeTranslucent.T(), new SphereGeometry(0.9f, 36, 20), 3f, m =>
        {
            m.SetTexture("BaseColor", Procedural.FromRgba(Solid(255, 90, 90, 90), 1));
            m.BlendMode = BlendMode.Translucent;
        });

        Report();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        Report();

        Context.RequestFrame();
    }

    private void OnSunAngleChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        sunAngle = e.ValueAs<double>();

        if (sun != null)
            sun.RotationDegrees = new Vector3((float)sunAngle, -28f, 0);

        Context.InvalidateRender();
    }

    private void OnSunCastToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (sun != null)
            sun.CastShadow = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnPointEnableToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (point != null)
            point.Enable = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void Add(string name, Geometry geometry, float x, Action<Material> fill)
    {
        var material = new Material();

        fill(material);

        var mesh = new Mesh
        {
            Name = name,
            Geometry = geometry,
            Material = material,
        };

        mesh.Position = new Vector3(x, 1.15f, 0);

        row!.AddChild(mesh, AttachToParentRule.KeepLocal);
    }

    private static byte[] Solid(byte r, byte g, byte b, byte a)
    {
        var data = new byte[4];

        data[0] = r;
        data[1] = g;
        data[2] = b;
        data[3] = a;

        return data;
    }

    private void Report()
    {
        var kind = Context.RequestedPipeline;
        var pipeline = Context.Scene?.RenderPipeline;

        if (pipeline == null)
            return;

        var passes = pipeline.OnceRenderPasses.Select(p => p.GetType().Name).ToList();

        passes.Add("→");

        passes.AddRange(pipeline.EveryCameraRenderPasses.Select(p => p.GetType().Name));

        PassesReadout.Text = string.Join(" ", passes);

        var pbr = kind is PipelineKind.PBRDeferred or PipelineKind.PBRForward;

        // 只有立方图那一路才喂给 IrradianceMap / PrefilteredEnvironment 两个 pass。
        var hasEnv = Context.Scene!.Background.IsT0;

        var litMark = kind == PipelineKind.NoLight ? "×" : "✓";
        var pbrMark = pbr ? "✓" : "×";
        var celMark = kind == PipelineKind.CelShading ? "✓" : "×";
        var envMark = pbr
            ? hasEnv ? Strings.Keys.Pipelines_EnvSet.T() : Strings.Keys.Pipelines_EnvUnsetDefault.T()
            : "—";

        ChannelsReadout.Text = Strings.Keys.Pipelines_ChannelsReadout.Format(
            kind.DisplayName(), litMark, pbrMark, pbrMark, pbrMark, litMark, celMark, envMark);
    }
}
