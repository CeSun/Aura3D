using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Aura3D.Examples.Localization;
using Avalonia.Collections;
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
/// Assimp 这条路：FBX 动作库的标准用法——模型文件只给骨骼，动作文件用同一个
/// <see cref="Skeleton"/> 实例去 <c>LoadAnimations</c>，拿到的剪辑就挂在同一副骨架上。
/// 依赖 Assimp 原生库，所以只在桌面端出现。
/// 参数行的排版全在 <c>AssimpFbxDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class AssimpFbxDemo : Demo
{
    // 剪辑名来自 FBX 文件本身，是身份不是文案；只有第 0 项「不挂采样器」是需要翻译的界面文字。
    private static readonly string[] ClipNames = ["Idle_Rifle_Hip", "Jog_Fwd_Rifle"];

    private ModelNode? model;
    private readonly List<Animation> clips = [];
    private readonly Dictionary<string, Animation> byLabel = [];

    private AnimationSampler? sampler;
    private int clipIndex = 1;
    private string boneName = string.Empty;
    private float sampleTime;

    /// <summary>当前选中项的身份：第 0 项是「不挂采样器」，其余是文件里的剪辑名。</summary>
    private string ClipLabel => (string)ClipOptions[clipIndex];

    /// <summary>「挂上的动作」下拉的选项，供 XAML 绑定（ComboRow.Options 是 IList）。</summary>
    public IList ClipOptions { get; } =
        ClipNames.Prepend(Strings.Keys.AssimpFbx_NoSampler.T()).ToList();

    /// <summary>骨骼名单：资产到位前是空的，<see cref="BuildScene"/> 里就地补齐。</summary>
    public AvaloniaList<string> BoneOptions { get; } = [];

    /// <summary>
    /// 建页：装配 XAML，并把动作下拉的选中项对齐到字段的初值。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public AssimpFbxDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        ClipRow.SelectedItem = ClipOptions[clipIndex];
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        var loaded = await assets.AssimpModelAsync("FbxMannequin");

        loaded.Name = "SK_Mannequin";

        model = loaded;

        // 两个动作文件都传同一个 Skeleton：返回的剪辑直接就是这副骨架的，不需要重定向。
        foreach (var key in new[] { "FbxIdle", "FbxJogFwd" })
        {
            var found = await assets.AssimpAnimationsAsync(key, model.Skeleton);

            foreach (var animation in found)
            {
                clips.Add(animation);

                byLabel[MatchLabel(animation.Name)] = animation;
            }
        }
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        if (model == null)
            return;

        scene.ShowGrid = true;
        scene.MainCamera.Position = new Vector3(38f, 42f, 62f);
        scene.MainCamera.LookAt(new Vector3(0, 40f, 0));

        var light = new DirectionalLight { LightColor = System.Drawing.Color.White };

        light.RotationDegrees = new Vector3(-42f, -24f, 0);

        scene.AddNode(light);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(600f, 600f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 20));

        scene.AddNode(ground);

        scene.AddNode(model);

        boneName = PickBone();

        ApplyClip();

        scene.MainCamera.FitToBoundingBox(model.BoundingBox, 0.25f);

        // 骨骼名单与剪辑条数都要等资产到位，XAML 里只有空壳，这里补齐绑定读的那份集合并回填初选。
        BoneOptions.Clear();

        foreach (var name in model.Skeleton?.Bones.Select(b => b.Name) ?? [])
            BoneOptions.Add(name);

        BoneRow.SelectedItem = boneName;

        Report();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        Context.RequestFrame();
    }

    private void OnClipChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        clipIndex = Math.Max(0, ClipOptions.IndexOf((string)e.Value!));

        ApplyClip();

        Report();
    }

    private void OnBoneChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        boneName = (string)e.Value!;

        ReportSample();
    }

    private void OnSampleTimeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        sampleTime = (float)e.ValueAs<double>();

        ReportSample();
    }

    private string MatchLabel(string animationName) =>
        animationName.Contains("Idle", StringComparison.OrdinalIgnoreCase) ? ClipNames[0] :
        animationName.Contains("Jog", StringComparison.OrdinalIgnoreCase) ? ClipNames[1] :
        ClipNames[0];

    private void ApplyClip()
    {
        if (model == null)
            return;

        if (clipIndex == 0)
        {
            model.AnimationSampler = null;
            sampler = null;

            return;
        }

        if (!byLabel.TryGetValue(ClipLabel, out var animation))
            return;

        sampler = new AnimationSampler(animation) { TimeScale = 1f, LoopMode = LoopMode.Loop };

        model.AnimationSampler = sampler;
    }

    private string PickBone()
    {
        var names = model?.Skeleton?.Bones.Select(b => b.Name).ToList() ?? [];

        return names.FirstOrDefault(n => n.Contains("Head", StringComparison.OrdinalIgnoreCase))
               ?? names.FirstOrDefault(n => n.Contains("Spine", StringComparison.OrdinalIgnoreCase))
               ?? names.FirstOrDefault()
               ?? string.Empty;
    }

    private void ReportSample()
    {
        var animation = clips.Count > 0
            ? byLabel.GetValueOrDefault(ClipLabel) ?? clips[0]
            : null;

        if (animation == null || string.IsNullOrEmpty(boneName))
        {
            SampleReadout.Text = Strings.Keys.AssimpFbx_NoClip.T();

            return;
        }

        var matrix = animation.Sample(boneName, sampleTime);

        SampleReadout.Text = Strings.Keys.AssimpFbx_SampleReadout.Format(
            animation.Name,
            sampleTime,
            boneName,
            matrix.Translation.X,
            matrix.Translation.Y,
            matrix.Translation.Z,
            animation.Channels.Count);
    }

    private void ReportSkeleton()
    {
        var skeleton = model?.Skeleton;

        if (skeleton == null)
            return;

        var retargeted = clips.Count > 0 && ReferenceEquals(clips[0].Skeleton, skeleton);

        SkeletonReadout.Text = Strings.Keys.AssimpFbx_SkeletonReadout.Format(
            skeleton.Bones.Count,
            skeleton.Root?.Name ?? "-",
            skeleton.GetBoneIndexMap().Count,
            retargeted
                ? Strings.Keys.AssimpFbx_SkeletonSame.T()
                : Strings.Keys.AssimpFbx_SkeletonDiff.T());
    }

    private void Report()
    {
        var table = string.Join(Strings.Keys.AssimpFbx_ListSeparator.T(), clips.Select(c => $"{c.Name}({c.Duration:0.##}s)"));

        ClipsReadout.Text = Strings.Keys.AssimpFbx_ClipTableReadout.Format(
            ClipLabel,
            clips.Count,
            table.Length > 0 ? table : Strings.Keys.AssimpFbx_None.T(),
            sampler?.BonesTransform.Count ?? 0);

        ReportSkeleton();

        ReportSample();
    }
}
