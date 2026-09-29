using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Aura3D.Examples.Localization;
using Avalonia.Interactivity;
using Irihi.Lingua;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Examples.Demos;

/// <summary>
/// <see cref="InstancedMeshGroup"/>（HISM）：八叉树把上万个实例切成若干
/// <see cref="InstancedMesh"/> 分组，剔除按组做，所以相机只看到一角时其余组不参与绘制。
/// 读数里的 <see cref="InstancedMeshGroup.InPlaceUpdateCount"/> 与
/// <see cref="InstancedMeshGroup.RebuildCount"/> 是本页的主角——
/// 原地更新只改缓冲，跨叶更新会把整棵树重建一次。
/// 参数行的排版全在 <c>HismDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class HismDemo : Demo
{
    private static readonly LinguaKey[] MoveModeKeys =
        [Strings.Keys.Hism_MoveNudge, Strings.Keys.Hism_MoveTeleport];

    private readonly List<Matrix4x4> transforms = [];

    private InstancedMeshGroup? group;
    private Random random = new(20260929);

    private int instanceCount = 12000;
    private int maxPerGroup = 512;
    private int maxDepth = 6;
    private int moveModeIndex;
    private bool jitter;
    private double jitterSpeed = 30;
    private int cursor;

    /// <summary>「移动方式」下拉的选项，供 XAML 绑定（ComboRow.Options 是 IList）。</summary>
    public IList MoveModes { get; } = MoveModeKeys.Select(k => k.T()).ToList();

    /// <summary>
    /// 建页：装配 XAML，并把下拉的选中项对齐到字段的初值。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public HismDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        MoveModeRow.SelectedItem = MoveModes[moveModeIndex];
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 26f, 62f);
        scene.MainCamera.LookAt(new Vector3(0, 2f, 0));

        var light = new DirectionalLight { LightColor = System.Drawing.Color.White };

        light.RotationDegrees = new Vector3(-46f, -14f, 0);

        scene.AddNode(light);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(140f, 140f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 32));
        ground.RotationDegrees = new Vector3(-90f, 0, 0);

        scene.AddNode(ground);

        var source = new Mesh
        {
            Name = "Stalk",
            Geometry = new BoxGeometry(0.32f, 2.6f, 0.32f),
            Material = new Material(),
        };

        source.Material.SetTexture("BaseColor", Procedural.VerticalGradient(
            new Procedural.Rgb(120, 190, 110),
            new Procedural.Rgb(36, 74, 44),
            width: 4,
            height: 64));

        group = new InstancedMeshGroup(source)
        {
            Name = "HISM",
            MaxInstancesPerGroup = maxPerGroup,
            MaxDepth = maxDepth,
        };

        scene.AddNode(group);

        RebuildField();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (!jitter || group == null || group.InstanceCount == 0)
            return;

        var steps = Math.Max(1, (int)Math.Round(jitterSpeed * deltaTime));

        for (int s = 0; s < steps; s++)
        {
            cursor = (cursor + 977) % group.InstanceCount;

            group.UpdateInstance(cursor, moveModeIndex == 0 ? Nudge(cursor) : Teleport(cursor));
        }

        Report();

        Context.RequestFrame();
    }

    private void OnInstanceCountChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        instanceCount = (int)e.ValueAs<double>();

        RebuildField();
    }

    private void OnMaxPerGroupChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        maxPerGroup = (int)e.ValueAs<double>();

        if (group != null)
        {
            group.MaxInstancesPerGroup = maxPerGroup;

            group.Build();
        }
    }

    private void OnMaxDepthChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        maxDepth = (int)e.ValueAs<double>();

        if (group != null)
        {
            group.MaxDepth = maxDepth;

            group.Build();
        }
    }

    private void OnMoveModeChanged(object? sender, InspectorValueChangedEventArgs e) =>
        moveModeIndex = Math.Max(0, MoveModes.IndexOf((string)e.Value!));

    private void OnJitterToggled(object? sender, InspectorValueChangedEventArgs e) => jitter = e.ValueAs<bool>();

    private void OnJitterSpeedChanged(object? sender, InspectorValueChangedEventArgs e) =>
        jitterSpeed = e.ValueAs<double>();

    private void OnGroupCullingToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        foreach (var leaf in group?.Groups ?? [])
            leaf.EnableFrustumCulling = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        group?.ClearInstances();

        Context.InvalidateRender();
    }

    private void OnRefillClick(object? sender, RoutedEventArgs e) => RebuildField();

    private void RebuildField()
    {
        transforms.Clear();

        random = new Random(20260929);

        for (int i = 0; i < instanceCount; i++)
            transforms.Add(Scatter(i));

        group?.SetInstances(transforms);
        group?.Build();

        Report();

        Context.InvalidateRender();
    }

    private Matrix4x4 Scatter(int index)
    {
        var plan = Ground(index);
        var position = new Vector3(plan.X, Terrain(plan.X, plan.Z), plan.Z);

        var height = 0.55f + (float)random.NextDouble() * 1.35f;
        var lean = (float)(random.NextDouble() - 0.5) * 0.5f;
        var twist = (float)(random.NextDouble() - 0.5) * 3.2f;

        return Matrix4x4.CreateScale(1f, height, 1f)
               * Matrix4x4.CreateRotationZ(lean)
               * Matrix4x4.CreateRotationY(twist)
               * Matrix4x4.CreateTranslation(position);
    }

    private static Vector3 Ground(int index)
    {
        // 分布只由下标决定，这样增量与瞬移两种模式改的是同一个实例。
        var angle = index * 2.399963f;
        var radius = 6f + 44f * MathF.Sqrt((index % 9973) / 9973f);

        return new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
    }

    private static float Terrain(float x, float z) =>
        1.3f * MathF.Sin(x * 0.045f) * MathF.Cos(z * 0.037f) + 0.6f;

    private Matrix4x4 Nudge(int index)
    {
        var position = Ground(index);

        position.X += 0.25f;
        position.Z += 0.15f;

        return Matrix4x4.CreateScale(1f, 1.1f, 1f)
               * Matrix4x4.CreateTranslation(new Vector3(position.X, Terrain(position.X, position.Z), position.Z));
    }

    private Matrix4x4 Teleport(int index)
    {
        var position = Ground(index);

        position.X = -position.X * 0.7f;
        position.Z = -position.Z * 0.7f;

        return Matrix4x4.CreateScale(1f, 1.6f, 1f)
               * Matrix4x4.CreateTranslation(new Vector3(position.X, Terrain(position.X, position.Z), position.Z));
    }

    private void Report()
    {
        var leaves = group?.Groups.Count ?? 0;

        OctreeReadout.Text = Strings.Keys.Hism_OctreeReadout.Format(
            group?.InstanceCount ?? 0,
            leaves,
            group?.IsBuilding == true,
            maxPerGroup,
            maxDepth,
            group?.InPlaceUpdateCount ?? 0,
            group?.RebuildCount ?? 0,
            cursor);
    }
}
