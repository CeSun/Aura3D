using Aura3D.Core.Geometries;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Particles;
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
using Color = System.Drawing.Color;
using ModelNode = Aura3D.Core.Nodes.Model;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// 蒙皮动画的驱动链：<see cref="AnimationSampler"/> 挂在 <see cref="ModelNode.AnimationSampler"/> 上，
/// 场景每帧调 Model.Update，由它在 <see cref="IAnimationSampler.ExternalUpdate"/> 为假时推进采样器；
/// <see cref="BoneAttachment"/> 则直接从 sampler 的 BonesTransform 里取某根骨骼的矩阵把自己搬过去。
/// 挂在右手上的火把是挂载效果的"看得见"的载体：柄、握把、铁箍与护笼全用 BoxGeometry 拼出来，
/// 杯口一路 <see cref="ParticleSystem"/> 分四路发射器喷火焰（翻页火焰图）、柔光、火星与烟，
/// 再配一盏会轻微摇曳的 <see cref="PointLight"/>，随着走路动画被骨骼拽着走。
/// 参数行的排版全在 <c>SkinnedAnimationDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class SkinnedAnimationDemo : Demo
{
    // 程序化贴图是纯 CPU 资源，构造期就能生成；火焰翻页图来自资产（ParticleFirePng，8×8 共 64 帧）。
    private static readonly Texture SoftDot = Procedural.SoftDot(128, 2.4f);
    private static readonly Texture Spark = Procedural.Spark(64, 64);

    private Texture fireSheet = null!;

    private static readonly LinguaKey[] LoopModeKeys =
        [Strings.Keys.SkinnedAnimation_LoopModeLoop, Strings.Keys.SkinnedAnimation_LoopModeOnce, Strings.Keys.SkinnedAnimation_LoopModePingPong];

    /// <summary>LoopMode 下拉的选项集合，供 XAML 的 <c>Options="{Binding LoopModeOptions}"</c> 绑定。</summary>
    public IList LoopModeOptions { get; } = LoopModeKeys.Select(k => k.T()).ToList();

    private ModelNode? model;
    private List<Animation> animations = [];

    private List<string> clipNames = [];

    private AnimationSampler? sampler;
    private BoneAttachment? attachment;
    private Node? torch;
    private PointLight? torchLight;
    private ParticleSystem? flame;

    /// <summary>火把点光源的基准亮度，Update 里在这个值附近做三频叠加的轻微摇曳。</summary>
    private const float TorchLightIntensity = 600f;

    /// <summary>士兵在 glb 里的默认朝向背对镜头；按相机方位角（约 30°）转过来正对观察者。</summary>
    private const float ModelFacingYaw = 210f;

    /// <summary>
    /// 火把根节点相对手骨的朝向（度）：(-90, 0, 0) 把柄轴（火把本地 +Y）扳到手骨的 -Z 上——
    /// 这就是掌心握持的方向。依据：glb 里食指根 (LeftHandIndex1) 与尾指根 (LeftHandPinky1)
    /// 相对手骨的偏移分别落在局部 Z 的两端（-2.9 / +5.6），拇指根偏向 -X，
    /// 所以拳心握棒沿"指节线"＝局部 Z 轴、掌心法线＝局部 X 轴；柄轴垂直于手指穿过掌心，
    /// 取朝上的一支（局部 -Z，世界仰角约 +23°，火口朝身体前上方）。
    /// 之前试过把柄轴对齐世界竖直方向，读起来像在手背上插了根旗杆，反而不像握着。
    /// </summary>
    private static readonly Vector3 TorchTiltDegrees = new(-90f, 0f, 0f);

    /// <summary>
    /// 火把根节点沿手骨 +Y（模型局部单位，即厘米）的握点偏移，落在掌心而不是腕关节。
    /// 该偏移先于骨骼矩阵生效，所以用的是模型局部单位。
    /// </summary>
    private static readonly Vector3 TorchGripOffset = new(0f, 6f, 0f);

    private int clipIndex;
    private float timeScale = 1f;
    private int loopModeIndex;
    private bool externalUpdate;
    private bool showAttachment = true;
    private float attachmentYaw;

    private float torchClock;
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

        // 火把火焰用的翻页图（8×8 共 64 帧），与粒子页共用同一份资产。
        fireSheet = await assets.TextureAsync("ParticleFirePng");
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

        scene.AddNode(ground);

        scene.AddNode(model);

        // 士兵的建模朝向背对镜头（相机在 +X+Z 一侧看到的是后背），转 ModelFacingYaw 让他正对观察者；
        // BoneAttachment 取的是 Mesh.WorldTransform，模型转过去后火把会跟着一起转到正面。
        model.RotationDegrees = new Vector3(0, ModelFacingYaw, 0);

        // 剪辑名来自 glb 实际的 animations 数组，只有资产到位后才填得进下拉。
        clipNames = animations.Select((a, i) => Strings.Keys.SkinnedAnimation_ClipOption.Format(i, a.Name, a.Duration)).ToList();

        ClipCombo.Options = clipNames;
        ClipCombo.SelectedItem = clipNames.Count > 0 ? clipNames[clipIndex] : "—";

        sampler = CreateSampler(clipIndex);
        model.AnimationSampler = sampler;

        scene.MainCamera.FitToBoundingBox(model.BoundingBox, 0.3f);

        // fit 会按模型盒子把 far 收紧到十几米，30×30 地面和滚轮拉远立刻撞远平面；这里固定抬回本页尺度。
        scene.MainCamera.FarPlane = 120f;

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
            attachment.LocalOffset = GripOffset(attachmentYaw);

        ReportBones();

        Context.InvalidateRender();
    }

    /// <summary>握点偏移：绕手骨 Y 轴偏航一圈，再沿手骨推到掌心。</summary>
    private static Matrix4x4 GripOffset(float yawDegrees) =>
        Matrix4x4.CreateRotationY(yawDegrees * MathF.PI / 180f)
        * Matrix4x4.CreateTranslation(TorchGripOffset);

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        // 火光摇曳：三个互质频率的正弦叠加，确定性（无随机数），在基准亮度的 ±18% 内摆动
        torchClock += (float)deltaTime;

        if (torchLight != null)
            torchLight.LuminousIntensity = TorchLightIntensity *
                (0.86f
                 + 0.09f * MathF.Sin(torchClock * 11.3f)
                 + 0.05f * MathF.Sin(torchClock * 27.7f + 1.9f)
                 + 0.04f * MathF.Sin(torchClock * 4.1f + 0.6f));

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
            LocalOffset = GripOffset(attachmentYaw),

            // glTF 的场景根节点带着 0.01 的整体缩放（模型按厘米建模）。不归一化的话，
            // 按米建模的火把会被缩小一百倍，在手里只剩几个像素。
            NormalizeScale = true,
            Enable = showAttachment,
        };

        BuildTorch();

        Context.Scene!.AddNode(attachment);

        // 发射原点取 ParticleSystem 的世界变换，Play 之后每帧都会跟着骨骼矩阵走。
        flame?.Play();

        ReportBones();
    }

    // 火把各部件的配色：木柄、皮握把、铁件、炭杯与炭芯。
    private static readonly Color Wood = Color.FromArgb(255, 116, 78, 48);
    private static readonly Color Leather = Color.FromArgb(255, 72, 48, 32);
    private static readonly Color Iron = Color.FromArgb(255, 58, 56, 54);
    private static readonly Color Charcoal = Color.FromArgb(255, 38, 34, 32);
    private static readonly Color Ember = Color.FromArgb(255, 255, 148, 40);

    /// <summary>
    /// 用方块拼一支拿得出手的火把：木柄（手握的那段缠两圈皮条）、铁箍、燃料方杯、
    /// 顶端炭芯与外撇八度的四根护笼柱；杯口一路粒子系统分四路发射器——
    /// 翻页火焰、柔光辉光、上蹿下落的火星、慢烟——最后挂一盏会摇曳的点光源。
    /// 全部子节点挂在 torch 根下，随 BoneAttachment 一起被骨骼矩阵拽着走。
    /// </summary>
    private void BuildTorch()
    {
        torch = new Node
        {
            Name = "Torch",
            RotationDegrees = TorchTiltDegrees,
        };

        // 手握段：从掌心往下伸一截，缠两圈皮条防滑
        AddBox("TorchGrip", 0.058f, 0.26f, 0.058f, new Vector3(0, -0.13f, 0), Wood);
        AddBox("TorchWrapA", 0.078f, 0.034f, 0.078f, new Vector3(0, -0.19f, 0), Leather);
        AddBox("TorchWrapB", 0.078f, 0.034f, 0.078f, new Vector3(0, -0.07f, 0), Leather);

        // 上半段略细，套一圈铁箍过渡到燃料杯
        AddBox("TorchShaft", 0.05f, 0.1f, 0.05f, new Vector3(0, 0.05f, 0), Wood);
        AddBox("TorchCollar", 0.088f, 0.036f, 0.088f, new Vector3(0, 0.115f, 0), Iron);

        // 燃料方杯与顶端炭芯：炭芯颜色给足饱和度，让点光源把它烧亮
        AddBox("TorchCup", 0.108f, 0.09f, 0.108f, new Vector3(0, 0.195f, 0), Charcoal);
        AddBox("TorchCoal", 0.062f, 0.045f, 0.062f, new Vector3(0, 0.255f, 0), Ember);

        // 护笼：四根细方柱各向外撇 8°，加两根十字横箍
        var barSpecs = new (float X, float Z, float Pitch, float Roll)[]
        {
            ( 1f,  1f,  8f,  0f),
            (-1f,  1f, -8f,  0f),
            ( 1f, -1f,  0f,  8f),
            (-1f, -1f,  0f, -8f),
        };

        for (var i = 0; i < barSpecs.Length; i++)
        {
            var (x, z, pitch, roll) = barSpecs[i];

            var bar = AddBox($"TorchBar{i}", 0.016f, 0.15f, 0.016f,
                new Vector3(0.054f * x, 0.275f, 0.054f * z), Iron);

            bar.RotationDegrees = new Vector3(pitch, 0, roll);
        }

        AddBox("TorchBandX", 0.152f, 0.018f, 0.018f, new Vector3(0, 0.215f, 0), Iron);
        AddBox("TorchBandZ", 0.018f, 0.018f, 0.152f, new Vector3(0, 0.215f, 0), Iron);

        // 杯口火焰：四路发射器共用一个系统，原点取本节点的世界变换，跟着手跑
        flame = new ParticleSystem
        {
            Name = "TorchFlame",
            MaxParticles = 512,
            Position = new Vector3(0, 0.3f, 0),
        };

        flame.Emitters.Add(FlameCoreEmitter());
        flame.Emitters.Add(FlameGlowEmitter());
        flame.Emitters.Add(EmberEmitter());
        flame.Emitters.Add(SmokeEmitter());

        torch.AddChild(flame, AttachToParentRule.KeepLocal);

        // 点光源挂在杯口正上方，火光主要喂给持火把的手臂和半个身体
        torchLight = new PointLight
        {
            Name = "TorchLight",
            LightColor = Color.Orange,
            LuminousIntensity = TorchLightIntensity,
            AttenuationRadius = 6f,
            Position = new Vector3(0, 0.33f, 0),
        };

        torch!.AddChild(torchLight, AttachToParentRule.KeepLocal);

        attachment!.AddChild(torch, AttachToParentRule.KeepLocal);
    }

    private Mesh AddBox(string name, float width, float height, float depth, Vector3 position, Color color)
    {
        var mesh = new Mesh
        {
            Name = name,
            Geometry = new BoxGeometry(width, height, depth),
            Material = new Material(),
            Position = position,
        };

        mesh.Material.SetTexture("BaseColor", Texture.CreateFromColor(color));

        torch!.AddChild(mesh, AttachToParentRule.KeepLocal);

        return mesh;
    }

    /// <summary>火焰主体：翻页火焰图按寿命走完 64 帧，亮黄起、透红收。</summary>
    private ParticleEmitter FlameCoreEmitter() => new()
    {
        Shape = EmissionShape.Cone,
        ConeAngle = 11f,
        ShapeSize = new Vector3(0.035f, 0.02f, 0.035f),
        EmissionRate = 170f,
        Lifetime = new RangeFloat(0.16f, 0.3f),
        Velocity = new RangeVector3(new Vector3(-0.12f, 0.55f, -0.12f), new Vector3(0.12f, 0.95f, 0.12f)),
        StartSize = new RangeFloat(0.13f, 0.19f),
        EndSize = new RangeFloat(0.01f, 0.03f),
        StartColor = Color.FromArgb(255, 255, 236, 160),
        EndColor = Color.FromArgb(0, 255, 90, 20),
        Rotation = new RangeFloat(0f, MathF.PI * 2f),
        AngularVelocity = new RangeFloat(-3f, 3f),
        Gravity = new Vector3(0, 1.1f, 0),
        Damping = 1.6f,
        Texture = fireSheet,
        FlipbookTiles = new Vector2(8, 8),
        BlendMode = BlendMode.Translucent,
        MaxParticles = 256,
    };

    /// <summary>柔光辉光：无纹理感的光团，负责把杯口糊成一片亮，遮住方块拼接的棱角。</summary>
    private ParticleEmitter FlameGlowEmitter() => new()
    {
        Shape = EmissionShape.Sphere,
        ShapeSize = new Vector3(0.03f, 0.03f, 0.03f),
        EmissionRate = 90f,
        Lifetime = new RangeFloat(0.12f, 0.2f),
        Velocity = new RangeVector3(new Vector3(-0.06f, 0.4f, -0.06f), new Vector3(0.06f, 0.7f, 0.06f)),
        StartSize = new RangeFloat(0.1f, 0.16f),
        EndSize = new RangeFloat(0.01f, 0.03f),
        StartColor = Color.FromArgb(230, 255, 190, 90),
        EndColor = Color.FromArgb(0, 255, 120, 30),
        Gravity = new Vector3(0, 0.8f, 0),
        Texture = SoftDot,
        BlendMode = BlendMode.Translucent,
        MaxParticles = 128,
    };

    /// <summary>火星：小火点上蹿、被负重力拉回抛物线，冷却成暗红后消失。</summary>
    private ParticleEmitter EmberEmitter() => new()
    {
        Shape = EmissionShape.Sphere,
        ShapeSize = new Vector3(0.03f, 0.03f, 0.03f),
        EmissionRate = 22f,
        Lifetime = new RangeFloat(0.5f, 1f),
        Velocity = new RangeVector3(new Vector3(-0.3f, 1.2f, -0.3f), new Vector3(0.3f, 2.2f, 0.3f)),
        StartSize = new RangeFloat(0.012f, 0.022f),
        EndSize = new RangeFloat(0f, 0.004f),
        StartColor = Color.FromArgb(255, 255, 214, 130),
        EndColor = Color.FromArgb(0, 255, 70, 10),
        Gravity = new Vector3(0, -2.2f, 0),
        Damping = 0.4f,
        Texture = Spark,
        BlendMode = BlendMode.Translucent,
        MaxParticles = 128,
    };

    /// <summary>慢烟：给火焰上方一点体积感，尺寸随寿命涨大、透明度归零。</summary>
    private ParticleEmitter SmokeEmitter() => new()
    {
        Shape = EmissionShape.Cone,
        ConeAngle = 16f,
        ShapeSize = new Vector3(0.03f, 0.01f, 0.03f),
        EmissionRate = 9f,
        Lifetime = new RangeFloat(0.9f, 1.6f),
        Velocity = new RangeVector3(new Vector3(-0.08f, 0.5f, -0.08f), new Vector3(0.08f, 0.85f, 0.08f)),
        StartSize = new RangeFloat(0.05f, 0.08f),
        EndSize = new RangeFloat(0.24f, 0.34f),
        StartColor = Color.FromArgb(70, 120, 118, 116),
        EndColor = Color.FromArgb(0, 120, 118, 116),
        Rotation = new RangeFloat(0f, MathF.PI * 2f),
        AngularVelocity = new RangeFloat(-1f, 1f),
        Gravity = new Vector3(0, 0.5f, 0),
        Texture = SoftDot,
        BlendMode = BlendMode.Translucent,
        MaxParticles = 64,
    };

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
