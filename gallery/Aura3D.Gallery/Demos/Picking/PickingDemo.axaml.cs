using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Aura3D.Gallery.Kit;
using Aura3D.Gallery.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// 拾取：<see cref="Aura3DViewBase.ObjectPicked"/> 事件、<see cref="Aura3DViewBase.PickAt"/> 全命中列表、
/// <see cref="Aura3DViewBase.PickClosestAt"/> 最近命中，以及实例拾取带回的
/// <see cref="PickResult.InstanceIndex"/>。
/// 传入的坐标是 DIP（Avalonia 的逻辑像素），视图内部才乘 <c>RenderScaling</c> 换算成像素，
/// 所以 <c>e.GetPosition(view)</c> 可以直接用，不必自己乘缩放。
/// 视口上的命中点标记与参数行的排版都在 <c>PickingDemo.axaml</c> 里，视图事件的订阅仍留在 <see cref="ViewAttached"/>。
/// </summary>
public sealed partial class PickingDemo : Demo
{
    private const int GridSize = 6;

    private readonly List<Mesh> targets = [];

    private Aura3DView? view;
    private InstancedMesh? field;
    private Mesh? blocker;

    private bool centerMode;
    private int lastHitCount;
    private int clickCount;

    /// <summary>
    /// 建页：装配 XAML。视口开关的初值与引擎默认一致，无需再对齐运行时状态。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public PickingDemo(DemoContext context) : base(context)
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public override void ViewAttached(Aura3DView created)
    {
        view = created;

        created.ObjectPicked += (_, e) =>
        {
            clickCount++;

            Report(e.Node, e.WorldPosition, e.Distance, e.InstanceIndex, "ObjectPicked");
        };

        // 自己再挂一个 PointerPressed：事件参数里没有屏幕坐标，而全命中列表要按点击位置现算。
        created.PointerPressed += OnPointerPressed;
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 4.5f, 12f);
        scene.MainCamera.LookAt(new Vector3(0, 1.4f, 0));

        var light = new DirectionalLight { LightColor = System.Drawing.Color.White };

        light.RotationDegrees = new Vector3(-45f, -25f, 0);

        scene.AddNode(light);

        for (int i = 0; i < 5; i++)
        {
            var mesh = new Mesh
            {
                Name = $"Target{i}",
                Geometry = new SphereGeometry(0.8f, 32, 18),
                Material = new Material(),
            };

            mesh.Material.SetTexture("BaseColor", Procedural.Checker(128, 3 + i));
            mesh.Position = new Vector3((i - 2) * 2.3f, 1.2f, -1f);

            scene.AddNode(mesh);
            targets.Add(mesh);
        }

        // 一块半透明挡板横在球体前面：同一次点击会同时命中它和后面的球，
        // 用来区分 PickAt（全命中）与 PickClosestAt（最近）。
        var blockerMaterial = new Material { BlendMode = BlendMode.Translucent };

        blockerMaterial.SetTexture("BaseColor", Procedural.SoftDot(128, 0.8f));

        blocker = new Mesh
        {
            Name = "Blocker",
            Geometry = new PlaneGeometry(13f, 6f),
            Material = blockerMaterial,
        };

        blocker.Position = new Vector3(0, 2.4f, 3f);

        scene.AddNode(blocker);

        BuildField(scene, GridSize);
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (centerMode)
        {
            var center = new Point(0, 0);

            if (view != null)
                center = new Point(view.Bounds.Width / 2, view.Bounds.Height / 2);

            var hits = view?.PickAt(center.X, center.Y) ?? [];
            var closest = hits.OrderBy(h => h.Distance).FirstOrDefault();

            PlaceMarker(center);

            if (closest != null)
                Report(closest.Node, closest.WorldPosition, closest.Distance, closest.InstanceIndex, "PickAt");
            else
                Readout.Text = Strings.Keys.Picking_NoCenterHit.T();

            lastHitCount = hits.Count;
        }

        Context.RequestFrame();
    }

    /// <inheritdoc />
    public override void Unload()
    {
        if (view != null)
            view.PointerPressed -= OnPointerPressed;

        view = null;
    }

    private void OnEnablePickingToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (view != null)
            view.EnablePicking = e.ValueAs<bool>();
    }

    private void OnCenterModeToggled(object? sender, InspectorValueChangedEventArgs e) =>
        centerMode = e.ValueAs<bool>();

    private void OnBlockerToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (blocker != null)
            blocker.Enable = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnFieldSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        if (Context.Scene != null && field != null)
            SetFieldCount(Context.Scene, (int)e.ValueAs<double>());
    }

    private void OnResetClickCount(object? sender, RoutedEventArgs e) => clickCount = 0;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (view == null || centerMode)
            return;

        var position = e.GetPosition(view);
        var hits = view.PickAt(position.X, position.Y);
        var closest = hits.OrderBy(h => h.Distance).FirstOrDefault();

        PlaceMarker(position);

        lastHitCount = hits.Count;

        if (closest != null)
            Report(closest.Node, closest.WorldPosition, closest.Distance, closest.InstanceIndex, Strings.Keys.Picking_SourceClick.T());
    }

    private void PlaceMarker(Point position)
    {
        Canvas.SetLeft(Marker, position.X - Marker.Width / 2);
        Canvas.SetTop(Marker, position.Y - Marker.Height / 2);
    }

    private void Report(Node node, Vector3 worldPosition, float distance, int? instanceIndex, string source)
    {
        Readout.Text = Strings.Keys.Picking_HitReadout.Format(
            source,
            node.Name,
            distance,
            worldPosition.X,
            worldPosition.Y,
            worldPosition.Z,
            instanceIndex?.ToString() ?? "—",
            lastHitCount,
            clickCount);

        Context.InvalidateRender();
    }

    private void BuildField(Scene scene, int size)
    {
        var source = new Mesh
        {
            Name = "FieldCube",
            Geometry = new BoxGeometry(0.34f, 0.34f, 0.34f),
            Material = new Material(),
        };

        source.Material.SetTexture("BaseColor", Procedural.VerticalGradient(width: 8, height: 64));

        field = InstancedMesh.FromMesh(source);

        SetFieldCount(scene, size);
    }

    private void SetFieldCount(Scene scene, int size)
    {
        if (field == null)
            return;

        var transforms = new List<Matrix4x4>(size * size);

        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                var position = new Vector3(
                    (x - (size - 1) / 2f) * 0.55f,
                    0.18f,
                    (z - (size - 1) / 2f) * 0.55f + 4f);

                transforms.Add(
                    Matrix4x4.CreateScale(1f, 1f + ((x + z) % 4) * 0.5f, 1f)
                    * Matrix4x4.CreateTranslation(position));
            }
        }

        if (!scene.Nodes.Contains(field))
            scene.AddNode(field);

        field.SetInstances(transforms);

        Context.InvalidateRender();
    }
}
