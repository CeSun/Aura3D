using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Aura3D.Examples.Localization;

namespace Aura3D.Examples.Demos;

/// <summary>
/// 四种内置几何体的构造参数与节点变换。改哪一段参数就重建那一个几何体（顶点是真的重新生成，
/// 不是靠缩放糊弄），四段互不干扰；下拉只决定顶点/索引读数读的是哪一个。
/// 全页零外部资产：棋盘纹理由 <see cref="Procedural"/> 现算。
/// 参数行的排版全在 <c>GeometriesDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class GeometriesDemo : Demo
{
    private readonly Dictionary<string, Mesh> meshes = new(StringComparer.Ordinal);

    private Node? row;
    private string readoutKind = "Box";

    private double boxWidth = 2, boxHeight = 2, boxDepth = 2;
    private double sphereRadius = 1.4, sphereWidthSegments = 48, sphereHeightSegments = 24, spherePhiLength = 360;
    private double cylTop = 1, cylBottom = 1, cylHeight = 2.4, cylRadial = 48;
    private bool cylOpen;
    private double planeWidth = 3, planeHeight = 3, planeWSegments = 8, planeHSegments = 8;

    private float spin;

    /// <summary>下拉的选项，同时也是 <c>meshes</c> 的键；XAML 里以 <c>Options="{Binding KindOptions}"</c> 取用。</summary>
    public string[] KindOptions { get; } = ["Box", "Sphere", "Cylinder", "Plane"];

    /// <summary>
    /// 建页：装配 XAML，并把下拉的选中项对齐到页面当前读数盯着的那个几何体。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public GeometriesDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        KindRow.SelectedItem = readoutKind;
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.ShowGrid = true;
        scene.MainCamera.Position = new Vector3(0, 3.2f, 11f);
        scene.MainCamera.LookAt(new Vector3(0, 1.2f, 0));

        var light = new DirectionalLight { LightColor = System.Drawing.Color.White };

        light.RotationDegrees = new Vector3(-45f, -25f, 0);

        scene.AddNode(light);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(80f, 80f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 16));

        scene.AddNode(ground);

        row = new Node { Name = "Row" };

        scene.AddNode(row);

        for (int index = 0; index < KindOptions.Length; index++)
        {
            var kind = KindOptions[index];

            var mesh = new Mesh
            {
                Name = kind,
                Geometry = Build(kind),
                Material = new Material(),
            };

            mesh.Material.SetTexture("BaseColor", Procedural.Checker(128, 4));
            mesh.Position = new Vector3((index - 1.5f) * 4.2f, 1.5f, 0);

            row.AddChild(mesh, AttachToParentRule.KeepLocal);
            meshes[kind] = mesh;
        }
    }

    private void OnKindChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        readoutKind = e.ValueAs<string>();

        Report();
    }

    private void OnBoxWidthChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        boxWidth = e.ValueAs<double>();

        Rebuild("Box");
    }

    private void OnBoxHeightChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        boxHeight = e.ValueAs<double>();

        Rebuild("Box");
    }

    private void OnBoxDepthChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        boxDepth = e.ValueAs<double>();

        Rebuild("Box");
    }

    private void OnSphereRadiusChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        sphereRadius = e.ValueAs<double>();

        Rebuild("Sphere");
    }

    private void OnSphereWidthSegmentsChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        sphereWidthSegments = e.ValueAs<double>();

        Rebuild("Sphere");
    }

    private void OnSphereHeightSegmentsChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        sphereHeightSegments = e.ValueAs<double>();

        Rebuild("Sphere");
    }

    private void OnSpherePhiLengthChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        spherePhiLength = e.ValueAs<double>();

        Rebuild("Sphere");
    }

    private void OnCylTopChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        cylTop = e.ValueAs<double>();

        Rebuild("Cylinder");
    }

    private void OnCylBottomChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        cylBottom = e.ValueAs<double>();

        Rebuild("Cylinder");
    }

    private void OnCylHeightChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        cylHeight = e.ValueAs<double>();

        Rebuild("Cylinder");
    }

    private void OnCylRadialChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        cylRadial = e.ValueAs<double>();

        Rebuild("Cylinder");
    }

    private void OnCylOpenToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        cylOpen = e.ValueAs<bool>();

        Rebuild("Cylinder");
    }

    private void OnPlaneWidthChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        planeWidth = e.ValueAs<double>();

        Rebuild("Plane");
    }

    private void OnPlaneHeightChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        planeHeight = e.ValueAs<double>();

        Rebuild("Plane");
    }

    private void OnPlaneWSegmentsChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        planeWSegments = e.ValueAs<double>();

        Rebuild("Plane");
    }

    private void OnPlaneHSegmentsChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        planeHSegments = e.ValueAs<double>();

        Rebuild("Plane");
    }

    private void OnSpinChanged(object? sender, InspectorValueChangedEventArgs e) =>
        spin = (float)e.ValueAs<double>();

    private void OnShowGridToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (Context.Scene != null)
            Context.Scene.ShowGrid = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnShowAxisToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (Context.Scene != null)
            Context.Scene.ShowAxisGizmo = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (spin <= 0 || row == null)
            return;

        row.RotationDegrees = new Vector3(0, row.RotationDegrees.Y + spin * (float)deltaTime, 0);

        Context.RequestFrame();
    }

    private Geometry Build(string kind) => kind switch
    {
        "Box" => new BoxGeometry((float)boxWidth, (float)boxHeight, (float)boxDepth),
        "Sphere" => new SphereGeometry(
            (float)sphereRadius,
            (int)sphereWidthSegments,
            (int)sphereHeightSegments,
            phiLength: (float)(spherePhiLength * MathF.PI / 180f)),
        "Cylinder" => new CylinderGeometry(
            (float)cylTop,
            (float)cylBottom,
            (float)cylHeight,
            (int)cylRadial,
            4,
            cylOpen),
        _ => new PlaneGeometry((float)planeWidth, (float)planeHeight, (int)planeWSegments, (int)planeHSegments),
    };

    /// <summary>
    /// 用当前参数重建指定那一种几何体。下拉只决定读数读哪一个，所以改了参数不一定要重印读数。
    /// </summary>
    private void Rebuild(string kind)
    {
        if (meshes.TryGetValue(kind, out var mesh))
        {
            mesh.Geometry = Build(kind);
        }

        if (kind == readoutKind)
            Report();

        Context.InvalidateRender();
    }

    private void Report()
    {
        if (!meshes.TryGetValue(readoutKind, out var mesh) || mesh.Geometry == null)
            return;

        var geometry = mesh.Geometry;

        var bytes = (long)geometry.VertexCount * (3 + 2 + 3) * sizeof(float);

        VertexReadout.Text = Strings.Keys.Geometries_VertexReadout.Format(
            geometry.VertexCount, geometry.IndicesCount, bytes / 1024.0);
    }

}
