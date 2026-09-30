using Aura3D.Core.Geometries;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Gallery.Kit;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Aura3D.Gallery.Localization;
using ModelNode = Aura3D.Core.Nodes.Model;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// 两种把多条剪辑合成一个姿势的采样器：<see cref="AnimationGraph"/> 按谓词在节点间切换并做 <c>BlendTime</c>
/// 交叉淡入；<see cref="AnimationBlendSpace"/> 按 (x,y) 轴值做反距离加权。两者都实现
/// <see cref="IAnimationSampler"/>，所以挂法与单条剪辑完全一样——<c>Model.Update</c> 分不出区别。
/// 模型用 KayKit 骷髅武士（CC0，95 条剪辑）：它有侧移与后退步态，混合空间才能真的沿两个轴 blend；
/// Soldier 只有前向的 Idle/Walk/Run，摆不出 2D 的采样点。
/// 参数行的排版全在 <c>AnimationMixDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class AnimationMixDemo : Demo
{
    // 状态机的三个状态，按数组顺序循环：Idle → Walking → Running → Walking → Idle
    private static readonly string[] StateClips = ["Idle", "Walking_A", "Running_A"];

    // 混合空间的采样点摆成十字，两轴都是 [-1,1]：竖轴前后、横轴左右，原点静止。
    // 端点放跑、半程放走——KayKit 没有后退跑和侧移走，所以下端点只能用后退走顶上，
    // 左右半程也没有对应的走路剪辑，只能空着
    private static readonly (string Clip, Vector2 Point)[] BlendSamples =
    [
        ("Idle", new Vector2(0f, 0f)),
        ("Running_A", new Vector2(0f, 1f)),
        ("Walking_A", new Vector2(0f, 0.5f)),
        ("Walking_Backwards", new Vector2(0f, -1f)),
        ("Running_Strafe_Left", new Vector2(-1f, 0f)),
        ("Running_Strafe_Right", new Vector2(1f, 0f)),
    ];

    private ModelNode? idleRunModel;
    private ModelNode? blendModel;
    private AnimationGraph? graph;
    private AnimationBlendSpace? blendSpace;

    private readonly Dictionary<string, Animation> byName = [];
    private readonly List<AnimationGraphNode> graphNodes = [];

    private int blendPoints;

    private int stateIndex;
    private bool autoToggle = true;
    private double toggleSeconds = 2.5;
    private double clock;
    private float graphBlendTime = 0.6f;

    private float axisX;
    private float axisY;
    private float idwPower = 2f;

    // 速度轴自动扫掠：X 在 0(静止)→1(跑步)→0 之间余弦往返，直接演示连续过渡
    private bool autoSweep = true;
    private double sweepPeriod = 8;
    private double sweepClock;

    /// <summary>
    /// 建页：装配 XAML。参数行的初值就是上面这些字段的默认值，因此 XAML 里写的是字面量，无需回写。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public AnimationMixDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        // 编辑板上的采样点摆位与 BlendSamples 一一对应，标签就是剪辑名
        BlendPad.SetSamples(BlendSamples.Select(s => new Kit.BlendSpacePad.SamplePoint(s.Point.X, s.Point.Y, s.Clip)));
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        var (model, animations) = await assets.ModelWithAnimationsAsync("KayKitWarrior");

        model.Name = "WarriorGraph";

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
        // 30×30 地面 + 拉远余量，抬过默认 far 100。
        scene.MainCamera.FarPlane = 120f;

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

        scene.AddNode(ground);

        idleRunModel.Position = new Vector3(-1.9f, 0, 0);

        scene.AddNode(idleRunModel);

        BuildGraph();

        // 右半边是同一条模型的克隆：SharedResource 连骨骼都共用，两个采样器各写自己的 BoneMatrixBuffer。
        blendModel = idleRunModel.Clone(CopyType.SharedResource);
        blendModel.Name = "WarriorBlend";
        blendModel.Position = new Vector3(1.9f, 0, 0);

        scene.AddNode(blendModel);

        BuildBlendSpace();

        // KayKit 的头盔不是蒙皮网格、只是静态挂在 head 骨骼节点下，而引擎的节点树不随动画更新，
        // 骨骼一动头盔就留在原地。显式把它转成 BoneAttachment 跟随骨骼；克隆要在 attach 之前做，
        // 两份模型各自处理一遍。
        AttachRigidMeshesToBones(idleRunModel);
        AttachRigidMeshesToBones(blendModel);
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        clock += deltaTime;

        if (autoToggle && clock > toggleSeconds)
        {
            clock = 0;

            AdvanceState();
        }

        if (autoSweep)
        {
            sweepClock = (sweepClock + deltaTime) % sweepPeriod;

            // 沿竖轴余弦往返：0(静止) → 0.5(走路) → 1(前进跑) 再回来；
            // SetAxis 只挪编辑板滑块、不发事件
            axisY = (1f - (float)Math.Cos(2 * Math.PI * sweepClock / sweepPeriod)) / 2f;

            BlendPad.SetAxis(axisX, axisY);
        }

        GraphReadout.Text =
            $"{(graph == null ? Strings.Keys.AnimationMix_NoGraph.T() : $"CurrentWeight={graph.CurrentWeight:0.###}")} · " +
            Strings.Keys.AnimationMix_GraphReadout.Format(StateClips[stateIndex], graphBlendTime);

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
        AdvanceState();

        Context.InvalidateRender();
    }

    // 沿 Idle → Walking → Running → Idle 的环推进一格：谓词读的就是这个字段
    private void AdvanceState() => stateIndex = (stateIndex + 1) % StateClips.Length;

    // 编辑板拖动：用户接管轴值；开着扫掠时先关掉，否则下一帧滑块就被扫掠值拽回去
    private void OnPadAxisChanged(object? sender, (double X, double Y) e)
    {
        axisX = (float)e.X;
        axisY = (float)e.Y;

        if (autoSweep)
        {
            autoSweep = false;

            AutoSweepToggle.IsChecked = false;
        }

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

        BlendPad.SetAxis(0, 0);

        sweepClock = 0;

        Context.InvalidateRender();
    }

    private void OnAutoSweepToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        autoSweep = e.ValueAs<bool>();

        if (autoSweep)
        {
            // 从静止重新开始一个完整的 静止→跑→静止 循环
            sweepClock = 0;
        }
    }

    private void OnSweepPeriodChanged(object? sender, InspectorValueChangedEventArgs e) =>
        sweepPeriod = Math.Max(1, (float)e.ValueAs<double>());

    private void BuildGraph()
    {
        if (idleRunModel?.Skeleton == null)
            return;

        if (!byName.TryGetValue(StateClips[0], out var idle) ||
            !byName.TryGetValue(StateClips[1], out var walk) ||
            !byName.TryGetValue(StateClips[2], out var run))
            return;

        var idleNode = new AnimationGraphNode(new AnimationSampler(idle)) { BlendTime = graphBlendTime };
        var walkNode = new AnimationGraphNode(new AnimationSampler(walk)) { BlendTime = graphBlendTime };
        var runNode = new AnimationGraphNode(new AnimationSampler(run)) { BlendTime = graphBlendTime };

        // 状态环：Idle → Walking → Running → Walking → Idle。转移条件就是「目标态是不是自己」。
        idleNode.AddNextNode((_, _) => stateIndex == 1, walkNode);
        walkNode.AddNextNode((_, _) => stateIndex == 2, runNode);
        walkNode.AddNextNode((_, _) => stateIndex == 0, idleNode);
        runNode.AddNextNode((_, _) => stateIndex == 1, walkNode);

        graphNodes.Clear();
        graphNodes.Add(idleNode);
        graphNodes.Add(walkNode);
        graphNodes.Add(runNode);

        graph = new AnimationGraph(idleRunModel.Skeleton, idleNode);

        idleRunModel.AnimationSampler = graph;
    }

    private void BuildBlendSpace()
    {
        if (blendModel?.Skeleton == null)
            return;

        blendSpace = new AnimationBlendSpace(blendModel.Skeleton) { IdwPower = idwPower };

        foreach (var (clip, point) in BlendSamples)
            AddPoint(clip, point);

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

    /// <summary>
    /// 把「没有蒙皮属性、但挂在骨骼节点下」的 mesh（KayKit 的头盔就是这种）转成 <see cref="BoneAttachment"/>：
    /// 从模型树摘下来、<c>Model = null</c> 变成静态网格，挂到场景根的 BoneAttachment 上，
    /// 每帧由 <c>LocalOffset × 骨骼矩阵 × 蒙皮网格世界矩阵</c> 搬到骨骼当前位置。
    /// LocalOffset 取绑定姿势下 mesh 相对骨骼的变换，保证第一帧位置不变。
    /// </summary>
    private void AttachRigidMeshesToBones(ModelNode model)
    {
        var skeleton = model.Skeleton;

        if (skeleton == null)
            return;

        // BoneAttachment 需要一份蒙皮网格来拿 sampler 与模型世界矩阵
        var samplerProvider = model.Meshes.FirstOrDefault(m => m.IsSkinnedMesh);

        if (samplerProvider == null)
            return;

        foreach (var mesh in model.Meshes.ToList())
        {
            // 有蒙皮属性的 mesh 走 GPU 蒙皮，不用管
            if (mesh.Geometry?.GetAttributeData(BuildInVertexAttribute.Joints_0) != null)
                continue;

            // glTF 里关节名就是节点名，沿 mesh 的父链向上找最近的骨骼
            var bone = FindNearestBoneAncestor(mesh.Parent, skeleton);

            if (bone == null)
                continue;

            var localOffset = GetLocalToModel(mesh, model) * bone.WorldMatrix.Inverse();

            mesh.Parent!.RemoveChild(mesh, AttachToParentRule.KeepLocal);

            mesh.Model = null;

            var attachment = new BoneAttachment
            {
                Name = $"{mesh.Name}_Attach",
                Mesh = samplerProvider,
                BoneName = bone.Name,
                LocalOffset = localOffset,
            };

            attachment.AddChild(mesh, AttachToParentRule.KeepLocal);

            Context.Scene!.AddNode(attachment);
        }
    }

    private static Bone? FindNearestBoneAncestor(Node? node, Skeleton skeleton)
    {
        while (node != null)
        {
            var bone = skeleton.Bones.FirstOrDefault(b => b.Name == node.Name);

            if (bone != null)
                return bone;

            node = node.Parent;
        }

        return null;
    }

    // 行主序约定下 world = local × parent，从 mesh 往上累乘到模型根，得到模型空间的绑定变换
    private static Matrix4x4 GetLocalToModel(Node node, Node root)
    {
        var result = Matrix4x4.Identity;

        while (node != null && !ReferenceEquals(node, root))
        {
            result *= node.LocalTransform;

            node = node.Parent;
        }

        return result;
    }
}
