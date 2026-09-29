using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
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
/// <c>PointCloudPass</c> 的四件事：圆形点精灵、点尺寸的两条来源、只有 Opaque/Translucent 两条绘制分支、
/// 以及 <c>INSTANCED_MESH</c> 那条实例分支。判定条件是 <see cref="Geometry.PrimitiveType"/> 为
/// <see cref="PrimitiveType.Points"/>，所以这页放了一个普通盒子当反例——它一根像素都不会出现。
/// 参数行的排版全在 <c>PointCloudDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class PointCloudDemo : Demo
{
    private static readonly LinguaKey[] ShapeKeys =
        [Strings.Keys.PointCloud_OptionShell, Strings.Keys.PointCloud_OptionHelix, Strings.Keys.PointCloud_OptionCube];

    private const float CloudRadius = 2.4f;
    private const float RowSpacing = 6f;

    private readonly List<Vector4> instanceColors = [];
    private readonly List<Matrix4x4> instanceTransforms = [];

    private Mesh? followPass;
    private Mesh? materialSize;
    private Mesh? translucent;
    private Mesh? masked;
    private InstancedMesh? instanced;

    private int count = 12000;
    private int shapeIndex;
    private bool coloredGroup1 = true;
    private bool materialOverride = true;
    private bool showInstanced = true;

    private float passPointSize = 5f;
    private float materialPointSize = 16f;

    /// <summary>形态下拉的选项，供 XAML 的 <c>Options="{Binding ShapeOptions}"</c> 绑定。</summary>
    public IList ShapeOptions { get; } = ShapeKeys.Select(k => k.T()).ToList();

    /// <summary>
    /// 建页：装配 XAML，并把形态下拉的选中项对齐到本页当前形态。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public PointCloudDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        ShapeRow.SelectedItem = ShapeOptions[shapeIndex];
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.ShowGrid = false;
        scene.MainCamera.Position = new Vector3(0, 3.2f, 16f);
        scene.MainCamera.LookAt(new Vector3(0, 2.6f, 0));
        scene.MainCamera.SetClippingPlanes(0.1f, 80f);

        followPass = AddCloud(Strings.Keys.PointCloud_NodeFollowsPass.T(), -RowSpacing * 1.5f, coloredGroup1, null);
        materialSize = AddCloud(Strings.Keys.PointCloud_NodeMaterialSize.T(), -RowSpacing * 0.5f, true, materialPointSize);
        translucent = AddCloud("3 Translucent", RowSpacing * 0.5f, true, 9f);
        masked = AddCloud("4 Masked", RowSpacing * 1.5f, true, 9f);

        translucent!.Material!.BlendMode = BlendMode.Translucent;
        masked!.Material!.BlendMode = BlendMode.Masked;

        // 反例：这管线的绘制谓词只收 Points，非点图元连 draw 都轮不到。
        var notAPointCloud = new Mesh
        {
            Name = Strings.Keys.PointCloud_NodeBoxNotDrawn.T(),
            Geometry = new BoxGeometry(1.6f, 1.6f, 1.6f),
            Material = new Material(),
        };

        notAPointCloud.Position = new Vector3(0, 2.6f, -3.4f);

        scene.AddNode(notAPointCloud);

        RebuildInstanced();

        Report();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (instanced != null && showInstanced)
        {
            for (int i = 0; i < instanceTransforms.Count; i++)
            {
                var spin = Matrix4x4.CreateRotationY((float)(deltaTime * 0.35) * (1f + i * 0.15f));

                instanceTransforms[i] = spin * instanceTransforms[i];
            }

            instanced.SetInstances(instanceTransforms);
        }

        Context.RequestFrame();
    }

    private void OnPassPointSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        passPointSize = (float)e.ValueAs<double>();

        var pass = FindPass();

        if (pass != null)
            pass.DefaultPointSize = passPointSize;

        Report();

        Context.InvalidateRender();
    }

    private void OnMaterialOverrideToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        materialOverride = e.ValueAs<bool>();

        var material = materialSize?.Material;

        if (material != null)
        {
            if (materialOverride)
                material.SetParameterValue("uPointSize", materialPointSize);
            else
                material.RemoveParameterValue("uPointSize");
        }

        Report();

        Context.InvalidateRender();
    }

    private void OnMaterialSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        materialPointSize = (float)e.ValueAs<double>();

        if (materialOverride)
            materialSize?.Material?.SetParameterValue("uPointSize", materialPointSize);

        Report();

        Context.InvalidateRender();
    }

    private void OnShapeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        // 建页时模板会把初值推给下拉，触发一次「没变」的合成选中，用相等判断挡掉。
        if (e.Value is not string value)
            return;

        var index = Math.Max(0, ShapeOptions.IndexOf(value));

        if (index == shapeIndex)
            return;

        shapeIndex = index;

        RebuildClouds();
    }

    private void OnCountChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        count = (int)e.ValueAs<double>();

        RebuildClouds();
    }

    private void OnGroup1ColorToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        coloredGroup1 = !e.ValueAs<bool>();

        RebuildClouds();
    }

    private void OnTranslucentToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (translucent != null)
            translucent.Enable = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnMaskedToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (masked != null)
            masked.Enable = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnShowInstancedToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        showInstanced = e.ValueAs<bool>();

        if (instanced != null)
            instanced.Enable = showInstanced;

        RebuildInstanced();

        Report();

        Context.InvalidateRender();
    }

    private Mesh AddCloud(string name, float x, bool withColors, float? ownPointSize)
    {
        var mesh = new Mesh
        {
            Name = name,
            Geometry = BuildCloud(shapeIndex, count, withColors),
            Material = new Material(),
        };

        if (ownPointSize is float size)
            mesh.Material.SetParameterValue("uPointSize", size);

        mesh.Position = new Vector3(x, 2.6f, 0);

        Context.Scene!.AddNode(mesh);

        return mesh;
    }

    private void RebuildClouds()
    {
        if (followPass != null)
            followPass.Geometry = BuildCloud(shapeIndex, count, coloredGroup1);

        if (materialSize != null)
            materialSize.Geometry = BuildCloud(shapeIndex, count, true);

        if (translucent != null)
            translucent.Geometry = BuildCloud(shapeIndex, count / 2, true);

        if (masked != null)
            masked.Geometry = BuildCloud(shapeIndex, count / 2, true);

        Report();

        Context.InvalidateRender();
    }

    private void RebuildInstanced()
    {
        if (instanced == null)
        {
            var seed = new Mesh
            {
                Name = "InstancedSeed",
                // 逐实例颜色占用同一个槽位 2，所以这份几何只给位置；形态固定取 0（球壳）。
                Geometry = BuildCloud(0, 420, false),
                Material = new Material(),
            };

            instanced = InstancedMesh.FromMesh(seed);
            instanced.Name = "InstancedCloud";
            instanced.Position = new Vector3(0, 2.6f, -6.5f);

            Context.Scene!.AddNode(instanced);
        }

        int instances = 7;

        instanceColors.Clear();
        instanceTransforms.Clear();

        for (int i = 0; i < instances; i++)
        {
            var t = i / (float)Math.Max(instances - 1, 1);

            instanceTransforms.Add(
                Matrix4x4.CreateScale(0.5f + 0.12f * i) *
                Matrix4x4.CreateTranslation(new Vector3(-4.2f + i * 1.4f, 0, 0)));

            instanceColors.Add(new Vector4(0.2f + 0.75f * t, 0.5f - 0.35f * t, 1f - 0.7f * t, 1f));
        }

        instanced.SetInstances(instanceTransforms);
        instanced.SetInstanceAttribute(BuildInVertexAttribute.Color_0, 4, instanceColors);
        instanced.Enable = showInstanced;
    }

    private static Geometry BuildCloud(int which, int points, bool withColors)
    {
        var positions = new List<float>(points * 3);
        var colors = withColors ? new List<float>(points * 4) : null;
        var indices = new List<uint>(points);

        for (int i = 0; i < points; i++)
        {
            var p = which switch
            {
                1 => Helix(i, points),
                2 => Cube(i),
                _ => Shell(i, points),
            };

            positions.Add(p.X);
            positions.Add(p.Y);
            positions.Add(p.Z);

            if (colors != null)
            {
                var c = new Vector4(
                    0.5f + 0.48f * MathF.Sin(p.X * 1.7f + p.Z),
                    0.5f + 0.45f * MathF.Sin(p.Y * 2.1f + 1.4f),
                    0.5f + 0.46f * MathF.Cos(p.X - p.Y * 1.3f),
                    1f);

                colors.Add(c.X);
                colors.Add(c.Y);
                colors.Add(c.Z);
                colors.Add(c.W);
            }

            indices.Add((uint)i);
        }

        var geometry = new Geometry { PrimitiveType = PrimitiveType.Points };

        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);

        if (colors != null)
            geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, colors);

        geometry.SetIndices(indices);

        return geometry;
    }

    private static Vector3 Shell(int index, int total)
    {
        var y = 1f - (index / (float)Math.Max(total - 1, 1)) * 2f;
        var r = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
        var theta = index * 2.399963f;

        return new Vector3(MathF.Cos(theta) * r, y, MathF.Sin(theta) * r) * CloudRadius + new Vector3(0, CloudRadius, 0);
    }

    private static Vector3 Helix(int index, int total)
    {
        var t = index / (float)Math.Max(total - 1, 1);
        var angle = t * MathF.PI * 14f;
        var radius = CloudRadius * (0.45f + 0.55f * MathF.Sin(t * MathF.PI));

        return new Vector3(
            MathF.Cos(angle) * radius,
            CloudRadius * 2f * t,
            MathF.Sin(angle) * radius);
    }

    private static Vector3 Cube(int index)
    {
        var jitter = CloudRadius * 1.15f;

        return new Vector3(
            (Hash(index) - 0.5f) * 2f * jitter,
            (Hash(index + 7919) - 0.5f) * 2f * jitter + CloudRadius,
            (Hash(index + 104729) - 0.5f) * 2f * jitter);
    }

    private static float Hash(int value)
    {
        var s = MathF.Sin(value * 12.9898f) * 43758.5453f;

        return s - MathF.Floor(s);
    }

    private PointCloudPass? FindPass() =>
        Context.Scene?.RenderPipeline.EveryCameraRenderPasses.OfType<PointCloudPass>().FirstOrDefault();

    private void Report()
    {
        var pass = FindPass();

        var group2 = materialSize?.Material != null &&
            materialSize.Material!.TryGetParameterValue<float>("uPointSize", out var v) ? v : (float?)null;

        Readout.Text = Strings.Keys.PointCloud_Readout.Format(
            pass == null
                ? Strings.Keys.PointCloud_PassMissing.T()
                : $"PointCloudPass.DefaultPointSize={pass.DefaultPointSize:0.#}",
            group2 is null
                ? Strings.Keys.PointCloud_SizeDroppedToPass.T()
                : $"{group2:0.#}",
            showInstanced
                ? Strings.Keys.PointCloud_InstancedCounts.Format(instanced?.InstanceCount ?? 0, instanced?.VertexCount ?? 0)
                : Strings.Keys.PointCloud_InstancedOff.T());
    }
}
