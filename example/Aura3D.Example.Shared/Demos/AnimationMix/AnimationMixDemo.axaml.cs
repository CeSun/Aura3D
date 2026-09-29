using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Aura3D.Examples.Localization;
using ModelNode = Aura3D.Core.Nodes.Model;

namespace Aura3D.Examples.Demos;

/// <summary>
/// 两种把多条剪辑合成一个姿势的采样器：<see cref="AnimationGraph"/> 按谓词在节点间切换并做 <c>BlendTime</c>
/// 交叉淡入；<see cref="AnimationBlendSpace"/> 按 (x,y) 轴值做反距离加权。两者都实现
/// <see cref="IAnimationSampler"/>，所以挂法与单条剪辑完全一样——<c>Model.Update</c> 分不出区别。
/// 参数行的排版全在 <c>AnimationMixDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class AnimationMixDemo : Demo
{
    private ModelNode? idleRunModel;
    private ModelNode? blendModel;
    private AnimationGraph? graph;
    private AnimationBlendSpace? blendSpace;

    private readonly Dictionary<string, Animation> byName = [];
    private readonly List<AnimationGraphNode> graphNodes = [];

    private int blendPoints;

    private bool wantRun;
    private bool autoToggle = true;
    private double toggleSeconds = 2.5;
    private double clock;
    private float graphBlendTime = 0.6f;

    private float axisX;
    private float axisY;
    private float idwPower = 2f;

    /// <summary>
    /// 建页：装配 XAML。参数行的初值就是上面这些字段的默认值，因此 XAML 里写的是字面量，无需回写。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public AnimationMixDemo(DemoContext context) : base(context)
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        var (model, animations) = await assets.ModelWithAnimationsAsync("Soldier");

        model.Name = "SoldierGraph";

        idleRunModel = model;

        foreach (var animation in animations)
            byName[animation.Name] = animation;
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        if (idleRunModel?.Skeleton == null)
            return;

        scene.ShowGrid = true;
        scene.MainCamera.Position = new Vector3(0, 1.9f, 6.4f);
        scene.MainCamera.LookAt(new Vector3(0, 1.1f, 0));

        var light = new DirectionalLight { LightColor = System.Drawing.Color.White };

        light.RotationDegrees = new Vector3(-40f, -20f, 0);

        scene.AddNode(light);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(30f, 30f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 18));
        ground.RotationDegrees = new Vector3(-90f, 0, 0);

        scene.AddNode(ground);

        idleRunModel.Position = new Vector3(-1.9f, 0, 0);

        scene.AddNode(idleRunModel);

        BuildGraph();

        // 右半边是同一条模型的克隆：SharedResource 连骨骼都共用，两个采样器各写自己的 BoneMatrixBuffer。
        blendModel = idleRunModel.Clone(CopyType.SharedResource);
        blendModel.Name = "SoldierBlend";
        blendModel.Position = new Vector3(1.9f, 0, 0);

        scene.AddNode(blendModel);

        BuildBlendSpace();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        clock += deltaTime;

        if (autoToggle && clock > toggleSeconds)
        {
            clock = 0;

            wantRun = !wantRun;
        }

        GraphReadout.Text =
            $"{(graph == null ? Strings.Keys.AnimationMix_NoGraph.T() : $"CurrentWeight={graph.CurrentWeight:0.###}")} · " +
            Strings.Keys.AnimationMix_GraphReadout.Format(wantRun ? "Run" : "Idle", graphBlendTime);

        BlendReadout.Text = Strings.Keys.AnimationMix_BlendReadout.Format(
            axisX, axisY, idwPower, blendPoints, blendSpace?.BonesTransform.Count ?? 0);

        Context.RequestFrame();
    }

    private void OnAutoToggleToggled(object? sender, InspectorValueChangedEventArgs e) =>
        autoToggle = e.ValueAs<bool>();

    private void OnIntervalChanged(object? sender, InspectorValueChangedEventArgs e) =>
        toggleSeconds = (float)e.ValueAs<double>();

    private void OnBlendTimeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        graphBlendTime = (float)e.ValueAs<double>();

        foreach (var node in graphNodes)
            node.BlendTime = graphBlendTime;

        Context.InvalidateRender();
    }

    private void OnManualSwitch(object? sender, RoutedEventArgs e)
    {
        wantRun = !wantRun;

        Context.InvalidateRender();
    }

    private void OnAxisXChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        axisX = (float)e.ValueAs<double>();

        ApplyAxis();
    }

    private void OnAxisYChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        axisY = (float)e.ValueAs<double>();

        ApplyAxis();
    }

    private void OnIdwPowerChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        idwPower = (float)e.ValueAs<double>();

        if (blendSpace != null)
            blendSpace.IdwPower = idwPower;

        Context.InvalidateRender();
    }

    private void OnResetAxis(object? sender, RoutedEventArgs e)
    {
        blendSpace?.Reset();

        axisX = 0;
        axisY = 0;

        Context.InvalidateRender();
    }

    private void BuildGraph()
    {
        if (idleRunModel?.Skeleton == null)
            return;

        if (!byName.TryGetValue("Idle", out var idle) || !byName.TryGetValue("Run", out var run))
            return;

        var idleNode = new AnimationGraphNode(new AnimationSampler(idle)) { BlendTime = graphBlendTime };
        var runNode = new AnimationGraphNode(new AnimationSampler(run)) { BlendTime = graphBlendTime };

        idleNode.AddNextNode((_, _) => wantRun, runNode);
        runNode.AddNextNode((_, _) => wantRun == false, idleNode);

        graphNodes.Clear();
        graphNodes.Add(idleNode);
        graphNodes.Add(runNode);

        graph = new AnimationGraph(idleRunModel.Skeleton, idleNode);

        idleRunModel.AnimationSampler = graph;
    }

    private void BuildBlendSpace()
    {
        if (blendModel?.Skeleton == null)
            return;

        blendSpace = new AnimationBlendSpace(blendModel.Skeleton) { IdwPower = idwPower };

        AddPoint("Idle", new Vector2(0, 0));
        AddPoint("Run", new Vector2(0, 1));
        AddPoint("Walk", new Vector2(0, -1));

        blendSpace.InitializePose();

        blendModel.AnimationSampler = blendSpace;

        ApplyAxis();
    }

    private void AddPoint(string name, Vector2 at)
    {
        if (blendSpace == null || !byName.TryGetValue(name, out var animation))
            return;

        blendSpace.AddAnimationSampler(at, new AnimationSampler(animation));

        blendPoints++;
    }

    private void ApplyAxis()
    {
        blendSpace?.SetAxis(axisX, axisY);

        Context.InvalidateRender();
    }
}
