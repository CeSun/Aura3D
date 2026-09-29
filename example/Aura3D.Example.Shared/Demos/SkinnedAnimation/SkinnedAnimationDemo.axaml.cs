using Aura3D.Core.Geometries;
using Aura3D.Core.Math;
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
using ModelNode = Aura3D.Core.Nodes.Model;

namespace Aura3D.Examples.Demos;

/// <summary>
/// 蒙皮动画的驱动链：<see cref="AnimationSampler"/> 挂在 <see cref="ModelNode.AnimationSampler"/> 上，
/// 场景每帧调 Model.Update，由它在 <see cref="IAnimationSampler.ExternalUpdate"/> 为假时推进采样器；
/// <see cref="BoneAttachment"/> 则直接从 sampler 的 BonesTransform 里取某根骨骼的矩阵把自己搬过去。
/// 参数行的排版全在 <c>SkinnedAnimationDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class SkinnedAnimationDemo : Demo
{
    private static readonly LinguaKey[] LoopModeKeys =
        [Strings.Keys.SkinnedAnimation_LoopModeLoop, Strings.Keys.SkinnedAnimation_LoopModeOnce, Strings.Keys.SkinnedAnimation_LoopModePingPong];

    /// <summary>LoopMode 下拉的选项集合，供 XAML 的 <c>Options="{Binding LoopModeOptions}"</c> 绑定。</summary>
    public IList LoopModeOptions { get; } = LoopModeKeys.Select(k => k.T()).ToList();

    private ModelNode? model;
    private List<Animation> animations = [];

    private List<string> clipNames = [];

    private AnimationSampler? sampler;
    private BoneAttachment? attachment;
    private Mesh? torch;

    private int clipIndex;
    private float timeScale = 1f;
    private int loopModeIndex;
    private bool externalUpdate;
    private bool showAttachment = true;
    private float attachmentYaw;

    private double manualStepClock;

    /// <summary>
    /// 建页：装配 XAML。剪辑下拉的选项要等 glb 里的动画数组，留到 <see cref="BuildScene"/> 再填，
    /// 这里只把常量初值对齐。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public SkinnedAnimationDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        LoopModeCombo.SelectedItem = LoopModeOptions[loopModeIndex];
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        var (loaded, clips) = await assets.ModelWithAnimationsAsync("Soldier");

        loaded.Name = "Soldier";

        model = loaded;
        animations = clips;
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        if (model == null)
            return;

        scene.ShowGrid = true;
        scene.MainCamera.Position = new Vector3(2.4f, 2.2f, 4.2f);
        scene.MainCamera.LookAt(new Vector3(0, 1.2f, 0));

        var light = new DirectionalLight
        {
            Name = "Key",
            LightColor = System.Drawing.Color.White,
            CastShadow = false,
        };

        light.RotationDegrees = new Vector3(-38f, -25f, 0);

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

        scene.AddNode(model);

        // 剪辑名来自 glb 实际的 animations 数组，只有资产到位后才填得进下拉。
        clipNames = animations.Select((a, i) => Strings.Keys.SkinnedAnimation_ClipOption.Format(i, a.Name, a.Duration)).ToList();

        ClipCombo.Options = clipNames;
        ClipCombo.SelectedItem = clipNames.Count > 0 ? clipNames[clipIndex] : "—";

        sampler = CreateSampler(clipIndex);
        model.AnimationSampler = sampler;

        scene.MainCamera.FitToBoundingBox(model.BoundingBox, 0.3f);

        SetupAttachment();

        Context.Settings.Debug.ShowBone = true;

        Report();
    }

    private void OnClipChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        clipIndex = clipNames.IndexOf((string)e.Value!);

        if (sampler != null && model != null && clipIndex >= 0)
        {
            sampler = CreateSampler(clipIndex);
            model.AnimationSampler = sampler;
        }

        Report();
    }

    private void OnTimeScaleChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        timeScale = (float)e.ValueAs<double>();

        if (sampler != null)
            sampler.TimeScale = timeScale;

        Context.InvalidateRender();
    }

    private void OnLoopModeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        loopModeIndex = Math.Max(0, LoopModeOptions.IndexOf((string)e.Value!));

        if (sampler != null)
            sampler.LoopMode = ModeOf();

        Context.InvalidateRender();
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        sampler?.Reset();

        Context.InvalidateRender();
    }

    private void OnExternalUpdateToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        externalUpdate = e.ValueAs<bool>();

        if (sampler != null)
            sampler.ExternalUpdate = externalUpdate;

        Context.InvalidateRender();
    }

    private void OnManualStep(object? sender, RoutedEventArgs e)
    {
        sampler?.Update(1.0 / 30.0);

        manualStepClock += 1.0 / 30.0;

        Context.InvalidateRender();
    }

    private void OnShowBoneToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowBone = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnAttachmentToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        showAttachment = e.ValueAs<bool>();

        if (attachment != null)
            attachment.Enable = showAttachment;

        Context.InvalidateRender();
    }

    private void OnAttachmentYawChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        attachmentYaw = (float)e.ValueAs<double>();

        if (attachment != null)
            attachment.LocalOffset = Matrix4x4.CreateRotationY(attachmentYaw * MathF.PI / 180f)
                * Matrix4x4.CreateTranslation(new Vector3(0, 0.35f, 0));

        ReportBones();

        Context.InvalidateRender();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        Report();

        Context.RequestFrame();
    }

    private LoopMode ModeOf() =>
        loopModeIndex == 1 ? LoopMode.Once :
        loopModeIndex == 2 ? LoopMode.PingPong :
        LoopMode.Loop;

    private AnimationSampler CreateSampler(int index)
    {
        var animation = animations[index];

        return new AnimationSampler(animation)
        {
            TimeScale = timeScale,
            LoopMode = ModeOf(),
        };
    }

    private void SetupAttachment()
    {
        if (model == null || model.Skeleton == null)
            return;

        var names = model.Skeleton.Bones.Select(b => b.Name).ToList();

        var boneName = names.FirstOrDefault(n => n.Contains("hand", StringComparison.OrdinalIgnoreCase)
                                                 && !n.Contains("end", StringComparison.OrdinalIgnoreCase))
                       ?? names.FirstOrDefault(n => n.Contains("hand", StringComparison.OrdinalIgnoreCase))
                       ?? names.FirstOrDefault(n => n.Contains("head", StringComparison.OrdinalIgnoreCase));

        if (boneName == null)
            return;

        var targetMesh = model.Meshes.FirstOrDefault(m => m.IsSkinnedMesh) ?? model.Meshes.FirstOrDefault();

        if (targetMesh == null)
            return;

        attachment = new BoneAttachment
        {
            Name = "HandAttachment",
            Mesh = targetMesh,
            BoneName = boneName,
            LocalOffset = Matrix4x4.CreateTranslation(new Vector3(0, 0.35f, 0)),
            Enable = showAttachment,
        };

        torch = new Mesh
        {
            Name = "Torch",
            Geometry = new CylinderGeometry(0.05f, 0.05f, 0.7f, 12),
            Material = new Material(),
        };

        torch.Material.SetTexture("BaseColor", Texture.CreateFromColor(System.Drawing.Color.FromArgb(255, 205, 125, 60)));

        var light = new PointLight
        {
            Name = "TorchLight",
            LightColor = System.Drawing.Color.Orange,
            LuminousIntensity = 600f,
            AttenuationRadius = 6f,
            Position = new Vector3(0, 0.45f, 0),
        };

        torch.AddChild(light, AttachToParentRule.KeepLocal);
        attachment.AddChild(torch, AttachToParentRule.KeepLocal);

        Context.Scene!.AddNode(attachment);

        ReportBones();
    }

    private void ReportBones()
    {
        var skeleton = model?.Skeleton;

        if (skeleton == null)
            return;

        var index = attachment != null ? skeleton.GetBoneIndex(attachment.BoneName) : -1;

        BoneReadout.Text = Strings.Keys.SkinnedAnimation_BoneReadout.Format(
            skeleton.Bones.Count,
            attachment?.BoneName ?? Strings.Keys.SkinnedAnimation_BoneNotPicked.T(),
            index,
            sampler?.BonesTransform.Count ?? 0,
            attachmentYaw);
    }

    private void Report()
    {
        var current = clipIndex >= 0 && clipIndex < animations.Count ? animations[clipIndex] : null;

        Readout.Text = Strings.Keys.SkinnedAnimation_Readout.Format(
            current?.Name ?? "-",
            current?.Duration.ToString("0.##") ?? "-",
            sampler?.TimeScale,
            sampler?.LoopMode,
            externalUpdate
                ? Strings.Keys.SkinnedAnimation_DriverManual.T()
                : Strings.Keys.SkinnedAnimation_DriverAuto.T(),
            manualStepClock,
            model?.IsSkinnedModel == true
                ? Strings.Keys.SkinnedAnimation_Yes.T()
                : Strings.Keys.SkinnedAnimation_No.T());
    }
}
