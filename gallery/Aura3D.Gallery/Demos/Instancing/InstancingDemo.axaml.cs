using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Gallery.Kit;
using Aura3D.Gallery.Localization;
using Avalonia.Interactivity;
using Irihi.Lingua;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// <see cref="InstancedMesh"/>：一份几何 + 一组逐实例矩阵 = 一次 draw call。
/// 左片用引擎着色器（正常吃光照与贴图），右片用自定义的实例感知着色器对
/// （<see cref="Shaders.InstancedColorVertex"/> + 逐实例顶点色）——覆盖顶点阶段之后，
/// 槽位 8–11 的实例矩阵必须由自己的着色器乘进去，否则整片实例会叠在原点。
/// 参数行的排版全在 <c>InstancingDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class InstancingDemo : Demo
{
    /// <summary>「逐个 UpdateInstance」模式一帧只驱动这么多实例，让两种方式的代价可比。</summary>
    private const int IncrementalSlice = 300;

    private const float FieldSize = 17f;

    private static readonly Vector3 LeftOffset = new(-9.5f, 0, 0);
    private static readonly Vector3 RightOffset = new(9.5f, 0, 0);

    private static readonly LinguaKey[] UpdateModeKeys =
        [Strings.Keys.Instancing_UpdateBatch, Strings.Keys.Instancing_UpdatePerInstance];

    private readonly List<Vector3> anchors = [];
    private readonly List<Matrix4x4> leftTransforms = [];
    private readonly List<Matrix4x4> rightTransforms = [];

    private InstancedMesh? litField;
    private InstancedMesh? coloredField;

    private int instanceCount = 1200;
    private int updateModeIndex;
    private bool animate = true;
    private double waveSpeed = 1.1;
    private double waveAmount = 1.6;
    private float clock;

    /// <summary>「更新方式」下拉的选项，供 XAML 绑定（ComboRow.Options 是 IList）。</summary>
    public IList UpdateModes { get; } = UpdateModeKeys.Select(k => k.T()).ToList();

    /// <summary>
    /// 建页：装配 XAML，并把下拉的选中项对齐到字段的初值。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public InstancingDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        UpdateModeRow.SelectedItem = UpdateModes[updateModeIndex];
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 11f, 26f);
        scene.MainCamera.LookAt(new Vector3(0, 1.5f, 0));
        // 17×17 实例场，机位 ~28；抬过默认 far 100，留出滚轮拉远的余量。
        scene.MainCamera.FarPlane = 120f;

        var light = new DirectionalLight { LightColor = System.Drawing.Color.White };

        light.RotationDegrees = new Vector3(-50f, -18f, 0);

        scene.AddNode(light);

        litField = InstancedMesh.FromMesh(new Mesh
        {
            Name = "LitSource",
            Geometry = new BoxGeometry(0.55f, 0.55f, 0.55f),
            Material = LitMaterial(),
        });

        coloredField = InstancedMesh.FromMesh(new Mesh
        {
            Name = "ColoredSource",
            Geometry = new BoxGeometry(0.55f, 0.55f, 0.55f),
            Material = Shaders.InstanceColors("LightPass"),
        });

        scene.AddNode(litField);
        scene.AddNode(coloredField);

        Rebuild();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (!animate)
            return;

        clock += (float)(deltaTime * waveSpeed);

        WriteFrame(clock);

        Context.RequestFrame();
    }

    private void OnInstanceCountChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        instanceCount = (int)e.ValueAs<double>();

        Rebuild();
    }

    private void OnUpdateModeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        updateModeIndex = Math.Max(0, UpdateModes.IndexOf((string)e.Value!));

        WriteFrame(clock);
    }

    private void OnAnimateToggled(object? sender, InspectorValueChangedEventArgs e) => animate = e.ValueAs<bool>();

    private void OnWaveSpeedChanged(object? sender, InspectorValueChangedEventArgs e) => waveSpeed = e.ValueAs<double>();

    private void OnWaveAmountChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        waveAmount = e.ValueAs<double>();

        WriteFrame(clock);
    }

    private void OnCullingToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (litField != null)
            litField.EnableFrustumCulling = e.ValueAs<bool>();

        if (coloredField != null)
            coloredField.EnableFrustumCulling = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnRemoveFrontClick(object? sender, RoutedEventArgs e)
    {
        if (litField == null)
            return;

        for (int i = 0; i < 200 && litField.InstanceCount > 0; i++)
            litField.RemoveInstance(0);

        Report();

        Context.InvalidateRender();
    }

    private void OnRestoreClick(object? sender, RoutedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        anchors.Clear();

        var side = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(instanceCount)));
        var step = FieldSize / side;

        for (int i = 0; i < instanceCount; i++)
        {
            var x = (i % side) * step - FieldSize / 2f;
            var z = (i / side) * step - FieldSize / 2f;

            anchors.Add(new Vector3(x, 0f, z));
        }

        var colors = new List<Vector4>(anchors.Count);

        for (int i = 0; i < anchors.Count; i++)
        {
            var t = i / (float)Math.Max(anchors.Count - 1, 1);

            colors.Add(new Vector4(t, 1f - t, 0.35f + 0.5f * MathF.Sin(t * 14f), 1f));
        }

        leftTransforms.Clear();
        rightTransforms.Clear();

        if (litField != null)
            litField.SetInstances(Prepare(leftTransforms, LeftOffset, clock));

        if (coloredField != null)
        {
            coloredField.SetInstances(Prepare(rightTransforms, RightOffset, clock));
            coloredField.SetInstanceAttribute(BuildInVertexAttribute.Color_0, 4, colors);
        }

        Report();

        Context.InvalidateRender();
    }

    /// <summary>
    /// 按当前模式把这一帧的实例矩阵写下去：批量整块重传，或只动前 <see cref="IncrementalSlice"/> 个。
    /// </summary>
    private void WriteFrame(float time)
    {
        if (updateModeIndex != 1)
        {
            if (litField != null)
                litField.SetInstances(Prepare(leftTransforms, LeftOffset, time));

            if (coloredField != null)
                coloredField.SetInstances(Prepare(rightTransforms, RightOffset, time));

            return;
        }

        if (litField != null)
            UpdateSome(litField, LeftOffset, time);

        if (coloredField != null)
            UpdateSome(coloredField, RightOffset, time);
    }

    private void UpdateSome(InstancedMesh field, Vector3 offset, float time)
    {
        var limit = Math.Min(IncrementalSlice, Math.Min(field.InstanceCount, anchors.Count));

        for (int i = 0; i < limit; i++)
            field.UpdateInstance(i, TransformAt(anchors[i] + offset, i, time));
    }

    private List<Matrix4x4> Prepare(List<Matrix4x4> buffer, Vector3 offset, float time)
    {
        buffer.Clear();

        for (int i = 0; i < anchors.Count; i++)
            buffer.Add(TransformAt(anchors[i] + offset, i, time));

        return buffer;
    }

    private Matrix4x4 TransformAt(Vector3 anchor, int index, float time)
    {
        var lift = (float)waveAmount * MathF.Sin(anchor.X * 0.42f + anchor.Z * 0.31f + time);

        var stretch = 1f + 0.7f * MathF.Sin(time * 0.8f + index * 0.02f);

        return Matrix4x4.CreateScale(1f, MathF.Max(0.25f, stretch), 1f)
               * Matrix4x4.CreateRotationY(time * 0.25f + index * 0.004f)
               * Matrix4x4.CreateTranslation(new Vector3(anchor.X, 0.45f + MathF.Max(0f, lift), anchor.Z));
    }

    private void Report()
    {
        CostReadout.Text = Strings.Keys.Instancing_CostReadout.Format(
            litField?.InstanceCount ?? 0,
            coloredField?.InstanceCount ?? 0,
            litField?.VertexCount ?? 0,
            UpdateModes[updateModeIndex]);
    }

    private static Material LitMaterial()
    {
        var material = new Material();

        material.SetTexture("BaseColor", Procedural.Checker(128, 2));

        return material;
    }
}
