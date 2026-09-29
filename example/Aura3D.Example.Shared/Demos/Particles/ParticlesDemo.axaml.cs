using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Particles;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
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
using Color = System.Drawing.Color;

namespace Aura3D.Examples.Demos;

/// <summary>
/// 粒子系统：三条发射器走三条渲染路径——公告板（<see cref="ParticleEmitter.Texture"/> +
/// <c>ParticlePass</c>，含 <see cref="ParticleEmitter.FlipbookTiles"/> 的按寿命翻页）、
/// 一次性爆发（<c>Looping=false</c> + <c>Duration</c>）、
/// 网格模式（<c>Mesh</c> 非空时引擎改用 <see cref="InstancedMesh"/> 绘制）。
/// 发射原点取自 <see cref="ParticleSystem"/> 节点的变换，所以每条发射器各自是一个系统节点。
/// 参数行的排版全在 <c>ParticlesDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class ParticlesDemo : Demo
{
    // 程序化贴图是纯 CPU 资源，构造期就能生成，检视面板因此可以直接引用。
    private static readonly Texture SoftDot = Procedural.SoftDot(128, 2.4f);
    private static readonly Texture Spark = Procedural.Spark(64, 64);
    private static readonly Texture ChunkSkin = Procedural.Checker(64, 2);

    // 512×512 的火焰翻页图：8×8 共 64 帧，是 FlipbookTiles 唯一认的那种排布。
    private Texture fireSheet = null!;

    private static readonly LinguaKey[] TextureKeys =
        [Strings.Keys.Particles_TextureSoftDot, Strings.Keys.Particles_TextureSpark, Strings.Keys.Particles_TextureFlame];

    // ComboRow.Options 是 IList，XAML 只能绑到公开的列表属性上。
    public IList TextureOptions { get; } = TextureKeys.Select(k => k.T()).ToList();

    public IList BlendModeOptions { get; } = new List<BlendMode>
    {
        BlendMode.Opaque,
        BlendMode.Masked,
        BlendMode.Translucent,
    };

    private int textureIndex = 2;
    private float flipbookTiles = 8f;

    // 发射器是普通 CPU 对象，UI 线程就能建好，检视面板因此可以直接拿它当初始值来源。
    private readonly ParticleEmitter fountainEmitter = new()
    {
        Shape = EmissionShape.Cone,
        ConeAngle = 17f,
        ShapeSize = new Vector3(0.3f, 0.3f, 0.3f),
        EmissionRate = 900,
        Lifetime = new RangeFloat(1.1f, 1.9f),
        Velocity = new RangeVector3(new Vector3(-0.4f, 6.4f, -0.4f), new Vector3(0.4f, 8.2f, 0.4f)),
        StartSize = new RangeFloat(0.45f, 0.8f),
        EndSize = new RangeFloat(0.02f, 0.06f),
        StartColor = Color.Goldenrod,
        EndColor = Color.FromArgb(0, 255, 120, 30),
        Gravity = new Vector3(0, -7.5f, 0),
        Damping = 0.35f,
        Texture = SoftDot,
        BlendMode = BlendMode.Translucent,
        MaxParticles = 4000,
    };

    private readonly ParticleEmitter burstEmitter = new()
    {
        Shape = EmissionShape.SphereSurface,
        ShapeSize = new Vector3(0.35f, 0.35f, 0.35f),
        EmissionRate = 24000,
        Looping = false,
        Duration = 0.18f,
        Lifetime = new RangeFloat(1.4f, 2.6f),
        Velocity = new RangeVector3(new Vector3(-4.5f, 1.5f, -4.5f), new Vector3(4.5f, 7f, 4.5f)),
        StartSize = new RangeFloat(0.3f, 0.55f),
        EndSize = new RangeFloat(0f, 0.04f),
        StartColor = Color.White,
        EndColor = Color.FromArgb(0, 90, 140, 255),
        AngularVelocity = new RangeFloat(-2.4f, 2.4f),
        Gravity = new Vector3(0, -4f, 0),
        Texture = Spark,
        BlendMode = BlendMode.Translucent,
        MaxParticles = 2000,
    };

    private readonly ParticleEmitter debrisEmitter = new()
    {
        Shape = EmissionShape.Box,
        ShapeSize = new Vector3(1.6f, 0.4f, 1.6f),
        EmissionRate = 90,
        Lifetime = new RangeFloat(2.5f, 4f),
        Velocity = new RangeVector3(new Vector3(-1.2f, -0.4f, -1.2f), new Vector3(1.2f, 1.4f, 1.2f)),
        StartSize = new RangeFloat(0.5f, 1f),
        EndSize = new RangeFloat(0.5f, 1f),
        Rotation = new RangeFloat(0f, 3.14f),
        AngularVelocity = new RangeFloat(-3f, 3f),
        Gravity = new Vector3(0, -12f, 0),
        Mesh = new Mesh
        {
            Name = "DebrisChunk",
            Geometry = new BoxGeometry(0.3f, 0.3f, 0.3f),
            Material = ChunkMaterial(),
        },
        MeshScale = 1f,
        MaxParticles = 600,
    };

    private ParticleSystem? fountain;
    private ParticleSystem? burst;
    private ParticleSystem? debris;

    /// <summary>
    /// 建页：装配 XAML，并把参数行的初值对齐到发射器对象里真正生效的那一份。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public ParticlesDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        FountainRateRow.Value = fountainEmitter.EmissionRate;
        FountainConeAngleRow.Value = fountainEmitter.ConeAngle;
        FountainGravityRow.Value = fountainEmitter.Gravity.Y;
        FountainDampingRow.Value = fountainEmitter.Damping;
        FountainStartSizeRow.Value = fountainEmitter.StartSize.Max;

        BurstRateRow.Value = burstEmitter.EmissionRate;
        BurstDurationRow.Value = burstEmitter.Duration;
        BurstLifetimeRow.Value = burstEmitter.Lifetime.Max;
        BurstVelocityRow.Value = burstEmitter.Velocity.Max.Y;

        DebrisRateRow.Value = debrisEmitter.EmissionRate;
        DebrisSpinRow.Value = debrisEmitter.AngularVelocity.Max;
        DebrisMeshScaleRow.Value = debrisEmitter.MeshScale;
        DebrisMaxParticlesRow.Value = debrisEmitter.MaxParticles;

        TextureRow.SelectedItem = TextureOptions[textureIndex];
        BlendModeRow.SelectedItem = fountainEmitter.BlendMode;
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        fireSheet = await assets.TextureAsync("ParticleFirePng");
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        ApplyBillboard();
        scene.MainCamera.Position = new Vector3(0, 5.5f, 18f);
        scene.MainCamera.LookAt(new Vector3(0, 3.2f, 0));

        var light = new DirectionalLight { LightColor = Color.White };

        light.RotationDegrees = new Vector3(-50f, -20f, 0);

        scene.AddNode(light);

        fountain = AddSystem(scene, "Fountain", new Vector3(-6f, 0.6f, 0f), fountainEmitter);
        burst = AddSystem(scene, "Burst", new Vector3(0f, 3.2f, 0f), burstEmitter);
        debris = AddSystem(scene, "Debris", new Vector3(6f, 4.2f, 0f), debrisEmitter);

        Context.Settings.Debug.ShowParticleBounds = true;
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        Readout.Text = Strings.Keys.Particles_Readout.Format(
            (fountain?.ActiveCount ?? 0) + (burst?.ActiveCount ?? 0) + (debris?.ActiveCount ?? 0),
            fountain?.ActiveCount ?? 0,
            burst?.ActiveCount ?? 0,
            burstEmitter.IsFinished,
            debris?.ActiveCount ?? 0,
            debrisEmitter.UseMeshRenderer
                ? "InstancedMesh"
                : Strings.Keys.Particles_RendererBillboard.T(),
            fountain?.IsPlaying ?? false,
            TextureOptions[textureIndex],
            fountainEmitter.FlipbookTiles.X,
            fountainEmitter.FlipbookTiles.Y) +
            (fountainEmitter.Texture == null ? Strings.Keys.Particles_NoTextureHint.T() : string.Empty);

        Context.RequestFrame();
    }

    private void OnFountainRateChanged(object? sender, InspectorValueChangedEventArgs e) =>
        fountainEmitter.EmissionRate = (float)e.ValueAs<double>();

    private void OnFountainConeAngleChanged(object? sender, InspectorValueChangedEventArgs e) =>
        fountainEmitter.ConeAngle = (float)e.ValueAs<double>();

    private void OnFountainGravityChanged(object? sender, InspectorValueChangedEventArgs e) =>
        fountainEmitter.Gravity = new Vector3(0, (float)e.ValueAs<double>(), 0);

    private void OnFountainDampingChanged(object? sender, InspectorValueChangedEventArgs e) =>
        fountainEmitter.Damping = (float)e.ValueAs<double>();

    private void OnFountainStartSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        var max = (float)e.ValueAs<double>();

        fountainEmitter.StartSize = new RangeFloat(max * 0.6f, max);
    }

    private void OnTextureChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        textureIndex = Math.Max(0, TextureOptions.IndexOf((string)e.Value!));

        ApplyBillboard();

        Context.InvalidateRender();
    }

    private void OnFlipbookTilesChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        flipbookTiles = (float)Math.Round(e.ValueAs<double>());

        ApplyBillboard();

        Context.InvalidateRender();
    }

    private void OnBurstRateChanged(object? sender, InspectorValueChangedEventArgs e) =>
        burstEmitter.EmissionRate = (float)e.ValueAs<double>();

    private void OnBurstDurationChanged(object? sender, InspectorValueChangedEventArgs e) =>
        burstEmitter.Duration = (float)e.ValueAs<double>();

    private void OnBurstLifetimeChanged(object? sender, InspectorValueChangedEventArgs e) =>
        burstEmitter.Lifetime = new RangeFloat(burstEmitter.Lifetime.Min, (float)e.ValueAs<double>());

    private void OnBurstVelocityChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        var max = (float)e.ValueAs<double>();

        burstEmitter.Velocity = new RangeVector3(
            new Vector3(-max, 1.5f, -max),
            new Vector3(max, max, max));
    }

    private void OnBurstRestart(object? sender, RoutedEventArgs e)
    {
        burst?.Stop();
        burst?.Play();
    }

    private void OnDebrisRateChanged(object? sender, InspectorValueChangedEventArgs e) =>
        debrisEmitter.EmissionRate = (float)e.ValueAs<double>();

    private void OnDebrisSpinChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        var max = (float)e.ValueAs<double>();

        debrisEmitter.AngularVelocity = new RangeFloat(-max, max);
    }

    private void OnDebrisMeshScaleChanged(object? sender, InspectorValueChangedEventArgs e) =>
        debrisEmitter.MeshScale = (float)e.ValueAs<double>();

    private void OnDebrisMaxParticlesChanged(object? sender, InspectorValueChangedEventArgs e) =>
        debrisEmitter.MaxParticles = (int)e.ValueAs<double>();

    private void OnDebrisRestart(object? sender, RoutedEventArgs e)
    {
        debris?.Stop();
        debris?.Play();
    }

    private void OnBlendModeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        var mode = (BlendMode)e.Value!;

        fountainEmitter.BlendMode = mode;
        burstEmitter.BlendMode = mode;

        Context.InvalidateRender();
    }

    private void OnRestartAll(object? sender, RoutedEventArgs e)
    {
        foreach (var system in new[] { fountain, burst, debris })
        {
            system?.Stop();
            system?.Play();
        }
    }

    private void OnPauseToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        foreach (var system in new[] { fountain, burst, debris })
        {
            if (e.ValueAs<bool>())
                system?.Pause();
            else
                system?.Play();
        }
    }

    private void OnCullingToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        foreach (var system in new[] { fountain, burst, debris })
        {
            if (system != null)
                system.EnableVisibilityCulling = e.ValueAs<bool>();
        }
    }

    private void OnShowBoundsToggled(object? sender, InspectorValueChangedEventArgs e) =>
        Context.Settings.Debug.ShowParticleBounds = e.ValueAs<bool>();

    // 贴图与翻页格数一起改：程序化图只有一帧，选它们时强制 1×1，否则会被切成拼贴块。
    private void ApplyBillboard()
    {
        fountainEmitter.Texture = textureIndex switch
        {
            1 => Spark,
            2 => fireSheet,
            _ => SoftDot,
        };

        fountainEmitter.FlipbookTiles = textureIndex == 2
            ? new Vector2(flipbookTiles, flipbookTiles)
            : Vector2.One;
    }

    private static Material ChunkMaterial()
    {
        var material = new Material();

        material.SetTexture("BaseColor", ChunkSkin);

        return material;
    }

    private static ParticleSystem AddSystem(Scene scene, string name, Vector3 position, ParticleEmitter emitter)
    {
        var system = new ParticleSystem { Name = name, MaxParticles = emitter.MaxParticles };

        system.Emitters.Add(emitter);
        system.Position = position;

        scene.AddNode(system);

        system.Play();

        return system;
    }
}
