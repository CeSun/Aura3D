using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Particles;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SDColor = System.Drawing.Color;

namespace Example.Pages;

public partial class DebugTestPage : UserControl
{
    private CameraController _cameraController = null!;
    private DebugTestViewModel? _vm;

    // 场景节点引用
    private readonly List<Node> _sceneNodes = [];
    private DirectionalLight? _dirLight;
    private PointLight? _pointLight;
    private SpotLight? _spotLight;
    private Mesh? _ground;
    private ParticleSystem? _particles;
    private Texture? _fireTexture;
    private Node? _fireGroup;
    private ParticleSystem? _dustParticles;
    private Mesh? _stonesMesh;

    // 模型源
    private readonly List<Model> _staticSourceModels = [];
    private Model? _soldierSource;
    private Animation? _soldierAnimation;
    private BoundingBox? _soldierBounds;

    // FPS 统计
    private readonly List<double> _deltaTimes = [];
    private int _fpsMin = int.MaxValue;
    private int _fpsMax;
    private long _fpsSum;
    private int _fpsFrameCount;

    public DebugTestPage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3DView) { MoveSpeed = 10f };
    }

    // ─── Scene Init ──────────────────────────────────────────

    private async void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var view = aura3DView;
        view.AutoRequestNextFrameRendering = false;

        _vm = DataContext as DebugTestViewModel;
        if (_vm == null) return;

        // 订阅 Build 命令
        _vm.BuildRequested += OnBuildRequested;

        // 订阅 ViewModel 属性变更
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        // 挂接拾取事件
        view.ObjectPicked += OnObjectPicked;

        // ── 加载模型资源 ──
        await LoadAssetsAsync();

        // ── 首次构建 ──
        BuildScene();

        // 应用初始状态
        ApplyAllSettings();

        view.RequestNextFrameRendering();
    }

    // ─── Asset Loading ───────────────────────────────────────

    private async System.Threading.Tasks.Task LoadAssetsAsync()
    {
        // 静态模型
        var staticUris = new[]
        {
            "avares://Example/Assets/Models/lion_head_1k.glb",
            "avares://Example/Assets/Models/coffee_table_round_01_1k.glb",
            "avares://Example/Assets/Models/wooden_stool_02_1k.glb",
            "avares://Example/Assets/Models/lightbulb_01_1k.glb",
        };

        foreach (var uri in staticUris)
        {
            try
            {
                var model = await System.Threading.Tasks.Task.Run(() =>
                {
                    using var stream = AssetLoader.Open(new Uri(uri));
                    return ModelLoader.LoadGlbModel(stream);
                });
                if (model != null)
                    _staticSourceModels.Add(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load {uri}: {ex.Message}");
            }
        }

        // Soldier 骨骼模型
        try
        {
            var (model, animations) = await System.Threading.Tasks.Task.Run(() =>
            {
                using var stream = AssetLoader.Open(
                    new Uri("avares://Example/Assets/Models/Soldier.glb"));
                return ModelLoader.LoadGlbModelAndAnimations(stream);
            });
            _soldierSource = model;
            if (animations.Count > 0)
                _soldierAnimation = animations[0];
            _soldierBounds = model?.BoundingBox;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load Soldier: {ex.Message}");
        }

        // Stones model (for mesh particles)
        try
        {
            var stonesModel = await System.Threading.Tasks.Task.Run(() =>
            {
                using var stream = AssetLoader.Open(
                    new Uri("avares://Example/Assets/Models/stones_01.glb"));
                return ModelLoader.LoadGlbModel(stream);
            });
            _stonesMesh = stonesModel?.Meshes.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load stones_01: {ex.Message}");
        }

    }

    // ─── Build Scene ─────────────────────────────────────────

    private void OnBuildRequested() => BuildScene();

    private void BuildScene()
    {
        if (_vm == null) return;

        var view = aura3DView;
        var scene = view.Scene!;

        // 清除旧节点
        foreach (var node in _sceneNodes)
            view.Remove(node);
        _sceneNodes.Clear();
        _dirLight = null;
        _pointLight = null;
        _spotLight = null;
        _ground = null;
        _particles = null;
        _fireGroup = null;
        _dustParticles = null;

        // ── 光照 ──
        _dirLight = new DirectionalLight
        {
            RotationDegrees = new Vector3(_vm.DirLightRotX, _vm.DirLightRotY, 0),
            LightColor = ToSystemColor(_vm.DirLightColor),
            Irradiance = _vm.DirLightIrradiance,
            Position = new Vector3(0f, 10.0f, 0.0f),
            CastShadow = true
        };
        _dirLight.ShadowConfig.FarPlane = 200;
        _dirLight.ShadowConfig.NearPlane = 1;
        _dirLight.ShadowConfig.Width = 200;
        _dirLight.ShadowConfig.Height = 200;
        view.AddNode(_dirLight);
        _sceneNodes.Add(_dirLight);

        _pointLight = new PointLight
        {
            Position = new Vector3(_vm.PointLightX, _vm.PointLightY, _vm.PointLightZ),
            LightColor = ToSystemColor(_vm.PointLightColor),
            LuminousIntensity = _vm.PointLightIntensity,
            AttenuationRadius = _vm.PointLightRadius
        };
        view.AddNode(_pointLight);
        _sceneNodes.Add(_pointLight);

        _spotLight = new SpotLight
        {
            Position = new Vector3(_vm.SpotLightX, _vm.SpotLightY, _vm.SpotLightZ),
            RotationDegrees = new Vector3(_vm.SpotLightRotX, _vm.SpotLightRotY, _vm.SpotLightRotZ),
            LightColor = ToSystemColor(_vm.SpotLightColor),
            LuminousIntensity = _vm.SpotLightIntensity,
            InnerConeAngleDegree = _vm.SpotLightInnerAngle,
            OuterAngleDegree = _vm.SpotLightOuterAngle,
            AttenuationRadius = _vm.SpotLightRadius
        };
        view.AddNode(_spotLight);
        _sceneNodes.Add(_spotLight);

        // ── 地面 ──
        float groundSize = _soldierBounds != null
            ? MathF.Max(_soldierBounds.Size.X, _soldierBounds.Size.Z) * 15f
            : 120f;

        _ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(groundSize, groundSize),
            Material = new Material()
            {
                BaseColor = Texture.CreateFromColor(SDColor.DarkGray)
            }
        };
        view.AddNode(_ground);
        _sceneNodes.Add(_ground);

        // ── Fire Group ──
        var firePos = new Vector3(_vm.FirePosX, _vm.FirePosY, _vm.FirePosZ);
        _fireGroup = new Node { Name = "FireGroup", Position = firePos };
        view.AddNode(_fireGroup);
        _sceneNodes.Add(_fireGroup);

        // Fire light (local coords)
        var fireLight = new PointLight
        {
            LightColor = SDColor.FromArgb(255, 255, 100, 10),
            LuminousIntensity = 800,
            AttenuationRadius = 5f,
            CastShadow = false,
        };
        _fireGroup.AddChild(fireLight, AttachToParentRule.KeepLocal);

        // Fire particles (6x6 flipbook, local coords)
        if (_fireTexture == null)
        {
            using var stream = AssetLoader.Open(new Uri("avares://Example/Assets/Textures/fire.png"));
            _fireTexture = TextureLoader.LoadTexture(stream);
        }

        _particles = new ParticleSystem
        {
            Name = "Flame",
            MaxParticles = 10000,
        };

        _particles.Emitters.Add(new ParticleEmitter
        {
            Shape = EmissionShape.Circle,
            ShapeSize = new Vector3(1.5f, 0, 1.5f),
            EmissionRate = 200,
            Lifetime = new RangeFloat(0.6f, 1.5f),
            Velocity = new RangeVector3(new(-0.3f, 2, -0.3f), new(0.3f, 5, 0.3f)),
            StartSize = new RangeFloat(0.6f, 1.2f),
            EndSize = new RangeFloat(0.3f, 0.6f),
            StartColor = SDColor.FromArgb(255, 255, 180, 60),
            EndColor = SDColor.FromArgb(255, 255, 80, 0),
            Gravity = new Vector3(0, 1.5f, 0),
            Damping = 0.4f,
            BlendMode = BlendMode.Translucent,
            Texture = _fireTexture,
            FlipbookTiles = new Vector2(8, 8),
        });

        _particles.Emitters.Add(new ParticleEmitter
        {
            Shape = EmissionShape.Cone,
            ShapeSize = new Vector3(0.5f, 1.5f, 0.5f),
            ConeAngle = 15,
            EmissionRate = 50,
            Lifetime = new RangeFloat(0.3f, 0.8f),
            Velocity = new RangeVector3(new(-0.5f, 6, -0.5f), new(0.5f, 10, 0.5f)),
            StartSize = new RangeFloat(0.15f, 0.3f),
            EndSize = new RangeFloat(0.03f, 0.06f),
            StartColor = SDColor.FromArgb(180, 255, 200, 50),
            EndColor = SDColor.FromArgb(0, 255, 80, 0),
            Gravity = new Vector3(0, 2f, 0),
            Damping = 0.2f,
            BlendMode = BlendMode.Translucent,
            Texture = _fireTexture,
            FlipbookTiles = new Vector2(8, 8),
        });

        _fireGroup.AddChild(_particles, AttachToParentRule.KeepLocal);
        _particles.Play();

        // ── Mesh Particle System (Dust/Debris — bullet impact at ground) ──
        if (_stonesMesh != null)
        {
            _dustParticles = new ParticleSystem
            {
                Name = "DustDebris",
                MaxParticles = 750,
            };

                // Main burst: debris flying outward and upward from impact point
                _dustParticles.Emitters.Add(new ParticleEmitter
                {
                    Shape = EmissionShape.Hemisphere,
                    ShapeSize = new Vector3(0.3f, 0.6f, 0.3f),
                    EmissionRate = 100,
                    Looping = true,
                    Lifetime = new RangeFloat(0.5f, 1.5f),
                    Velocity = new RangeVector3(new(-4f, 3, -4f), new(4f, 10, 4f)),
                    StartSize = new RangeFloat(2.0f, 2.0f),
                    EndSize = new RangeFloat(2.0f, 2.0f),
                    StartColor = SDColor.FromArgb(255, 200, 180, 150),
                    EndColor = SDColor.FromArgb(0, 200, 180, 150),
                    Rotation = new RangeFloat(0f, MathF.PI * 2),
                    AngularVelocity = new RangeFloat(-12f, 12f),
                    Gravity = new Vector3(0f, -12f, 0f),
                    Damping = 2f,
                    MeshScale = 1.0f,
                    BlendMode = BlendMode.Opaque,
                    Mesh = _stonesMesh,
                });

                // Secondary: smaller lingering dust cloud
                _dustParticles.Emitters.Add(new ParticleEmitter
                {
                    Shape = EmissionShape.Circle,
                    ShapeSize = new Vector3(0.8f, 0, 0.8f),
                    EmissionRate = 38,
                    Looping = true,
                    Lifetime = new RangeFloat(0.8f, 2.5f),
                    Velocity = new RangeVector3(new(-1.5f, 0.5f, -1.5f), new(1.5f, 3, 1.5f)),
                    StartSize = new RangeFloat(2.0f, 2.0f),
                    EndSize = new RangeFloat(2.0f, 2.0f),
                    StartColor = SDColor.FromArgb(200, 180, 160, 140),
                    EndColor = SDColor.FromArgb(0, 180, 160, 140),
                    Rotation = new RangeFloat(0f, MathF.PI * 2),
                    AngularVelocity = new RangeFloat(-6f, 6f),
                    Gravity = new Vector3(0f, -6f, 0f),
                    Damping = 1f,
                    MeshScale = 1.0f,
                    BlendMode = BlendMode.Opaque,
                    Mesh = _stonesMesh,
                });

                // Place at impact point on ground
                _dustParticles.Position = new Vector3(_vm.DustPosX, _vm.DustPosY, _vm.DustPosZ);
                _dustParticles.RotationDegrees = new Vector3(_vm.DustRotX, _vm.DustRotY, _vm.DustRotZ);
                view.AddNode(_dustParticles);
                _sceneNodes.Add(_dustParticles);
                _dustParticles.Play();
        }

        // ── 静态模型网格 ──
        int staticCount = _vm.StaticMeshCount;
        if (_staticSourceModels.Count > 0 && staticCount > 0)
        {
            float staticSpacing = 4f;
            int staticGrid = (int)Math.Ceiling(Math.Sqrt(staticCount));
            float staticHalf = (staticGrid - 1) * staticSpacing / 2f;

            for (int i = 0; i < staticCount; i++)
            {
                var source = _staticSourceModels[i % _staticSourceModels.Count];
                var clone = source.Clone();

                int row = i / staticGrid;
                int col = i % staticGrid;
                clone.Position = new Vector3(
                    col * staticSpacing - staticHalf + 6f,
                    0,
                    row * staticSpacing - staticHalf - 5f);
                clone.RotationDegrees = new Vector3(0, (i * 37f) % 360f, 0);
                // 凳子(index 2)放大5倍，灯泡(index 3)放大10倍
                int modelIndex = i % _staticSourceModels.Count;
                float scale = modelIndex switch
                {
                    2 => 3.0f,
                    3 => 6.0f,
                    _ => 0.6f
                };
                clone.Scale = new Vector3(scale);

                view.AddNode(clone);
                _sceneNodes.Add(clone);
            }
        }

        // ── 骨骼模型网格 ──
        int skinnedCount = _vm.SkinnedMeshCount;
        if (_soldierSource != null && skinnedCount > 0)
        {
            var bb = _soldierBounds;
            float spacing = bb != null
                ? MathF.Max(bb.Size.X, bb.Size.Z) * 1.5f
                : 3f;

            int skinnedGrid = (int)Math.Ceiling(Math.Sqrt(skinnedCount));
            float skinnedHalf = (skinnedGrid - 1) * spacing / 2f;

            for (int i = 0; i < skinnedCount; i++)
            {
                int row = i / skinnedGrid;
                int col = i % skinnedGrid;

                float x = col * spacing - skinnedHalf - 6f;
                float z = row * spacing - skinnedHalf - 5f;

                var clone = _soldierSource.Clone();
                clone.BoundingBoxPadding = _vm.BoundingBoxPadding;

                if (_soldierAnimation != null)
                {
                    var sampler = new AnimationSampler(_soldierAnimation)
                    {
                        TimeScale = 1.0f,
                        LoopMode = LoopMode.Loop
                    };
                    clone.AnimationSampler = sampler;
                }

                clone.Position = new Vector3(x, 0, z);
                clone.RotationDegrees = new Vector3(0, 180 + (i % 4) * 90, 0);

                view.AddNode(clone);
                _sceneNodes.Add(clone);
            }
        }

        // ── 相机 ──
        if (_soldierBounds != null)
        {
            view.MainCamera.FarPlane = 500;
            view.MainCamera.Position = new Vector3(0, 15, 25);
            view.MainCamera.LookAt(new Vector3(0, 0, -5f));
        }

        // 应用当前设置状态
        ApplyAllSettings();

        // 重置 FPS
        _deltaTimes.Clear();
        _fpsFrameCount = 0;
        _fpsMin = int.MaxValue;
        _fpsMax = 0;

        _vm.TotalCount = _sceneNodes.Count(n => n is Model);
        _vm.DetailText = $"Static: {staticCount} | Skinned: {skinnedCount} | Lights: 3";

        view.RequestNextFrameRendering();
    }

    // ─── Settings Application ────────────────────────────────

    private void ApplyAllSettings()
    {
        if (_vm == null) return;

        var scene = aura3DView.Scene;
        if (scene == null) return;

        // 坐标轴 / 网格
        scene.ShowAxisGizmo = _vm.ShowAxes;
        scene.ShowGrid = _vm.ShowGrid;

        // Debug Draw
        var debug = scene.RenderPipeline.Settings.Debug;
        debug.Enable = _vm.DebugEnable;
        debug.ShowBoundingBox = _vm.ShowBoundingBox;
        debug.ShowDirectionalLight = _vm.ShowDirectionalLight;
        debug.ShowPointLight = _vm.ShowPointLight;
        debug.ShowSpotLight = _vm.ShowSpotLight;
        debug.ShowCamera = _vm.ShowCamera;
        debug.ShowBone = _vm.ShowBone;
        debug.ShowParticleBounds = _vm.ShowParticleBounds;

        // Picking
        aura3DView.EnablePicking = _vm.EnablePicking;

        // Frustum culling
        scene.RenderPipeline.EnableFrustumCulling = _vm.EnableFrustumCulling;

        // Lights
        if (_dirLight != null)
        {
            _dirLight.Enable = _vm.DirLightEnabled;
            _dirLight.RotationDegrees = new Vector3(_vm.DirLightRotX, _vm.DirLightRotY, 0);
            _dirLight.Irradiance = _vm.DirLightIrradiance;
            _dirLight.LightColor = ToSystemColor(_vm.DirLightColor);
        }
        if (_pointLight != null)
        {
            _pointLight.Enable = _vm.PointLightEnabled;
            _pointLight.Position = new Vector3(_vm.PointLightX, _vm.PointLightY, _vm.PointLightZ);
            _pointLight.LuminousIntensity = _vm.PointLightIntensity;
            _pointLight.LightColor = ToSystemColor(_vm.PointLightColor);
            _pointLight.AttenuationRadius = _vm.PointLightRadius;
        }
        if (_spotLight != null)
        {
            _spotLight.Enable = _vm.SpotLightEnabled;
            _spotLight.Position = new Vector3(_vm.SpotLightX, _vm.SpotLightY, _vm.SpotLightZ);
            _spotLight.RotationDegrees = new Vector3(_vm.SpotLightRotX, _vm.SpotLightRotY, _vm.SpotLightRotZ);
            _spotLight.LuminousIntensity = _vm.SpotLightIntensity;
            _spotLight.LightColor = ToSystemColor(_vm.SpotLightColor);
            _spotLight.InnerConeAngleDegree = _vm.SpotLightInnerAngle;
            _spotLight.OuterAngleDegree = _vm.SpotLightOuterAngle;
            _spotLight.AttenuationRadius = _vm.SpotLightRadius;
        }
    }

    // ─── ViewModel PropertyChanged ───────────────────────────

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_vm == null) return;

        var scene = aura3DView.Scene;
        if (scene == null) return;

        switch (e.PropertyName)
        {
            // 坐标轴 / 网格
            case nameof(DebugTestViewModel.ShowAxes):
                scene.ShowAxisGizmo = _vm.ShowAxes;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.ShowGrid):
                scene.ShowGrid = _vm.ShowGrid;
                aura3DView.RequestNextFrameRendering();
                break;

            // Debug Draw
            case nameof(DebugTestViewModel.DebugEnable):
                scene.RenderPipeline.Settings.Debug.Enable = _vm.DebugEnable;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.ShowBoundingBox):
                scene.RenderPipeline.Settings.Debug.ShowBoundingBox = _vm.ShowBoundingBox;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.ShowDirectionalLight):
                scene.RenderPipeline.Settings.Debug.ShowDirectionalLight = _vm.ShowDirectionalLight;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.ShowPointLight):
                scene.RenderPipeline.Settings.Debug.ShowPointLight = _vm.ShowPointLight;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.ShowSpotLight):
                scene.RenderPipeline.Settings.Debug.ShowSpotLight = _vm.ShowSpotLight;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.ShowCamera):
                scene.RenderPipeline.Settings.Debug.ShowCamera = _vm.ShowCamera;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.ShowBone):
                scene.RenderPipeline.Settings.Debug.ShowBone = _vm.ShowBone;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.ShowParticleBounds):
                scene.RenderPipeline.Settings.Debug.ShowParticleBounds = _vm.ShowParticleBounds;
                aura3DView.RequestNextFrameRendering();
                break;

            // Picking
            case nameof(DebugTestViewModel.EnablePicking):
                aura3DView.EnablePicking = _vm.EnablePicking;
                break;

            // Lights
            case nameof(DebugTestViewModel.DirLightEnabled):
                if (_dirLight != null) _dirLight.Enable = _vm.DirLightEnabled;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.DirLightRotX):
            case nameof(DebugTestViewModel.DirLightRotY):
                if (_dirLight != null) _dirLight.RotationDegrees = new Vector3(_vm.DirLightRotX, _vm.DirLightRotY, 0);
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.DirLightIrradiance):
                if (_dirLight != null) _dirLight.Irradiance = _vm.DirLightIrradiance;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.DirLightColor):
                if (_dirLight != null) _dirLight.LightColor = ToSystemColor(_vm.DirLightColor);
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.PointLightEnabled):
                if (_pointLight != null) _pointLight.Enable = _vm.PointLightEnabled;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.PointLightX):
            case nameof(DebugTestViewModel.PointLightY):
            case nameof(DebugTestViewModel.PointLightZ):
                if (_pointLight != null) _pointLight.Position = new Vector3(_vm.PointLightX, _vm.PointLightY, _vm.PointLightZ);
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.PointLightIntensity):
                if (_pointLight != null) _pointLight.LuminousIntensity = _vm.PointLightIntensity;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.PointLightColor):
                if (_pointLight != null) _pointLight.LightColor = ToSystemColor(_vm.PointLightColor);
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.PointLightRadius):
                if (_pointLight != null) _pointLight.AttenuationRadius = _vm.PointLightRadius;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.SpotLightEnabled):
                if (_spotLight != null) _spotLight.Enable = _vm.SpotLightEnabled;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.SpotLightX):
            case nameof(DebugTestViewModel.SpotLightY):
            case nameof(DebugTestViewModel.SpotLightZ):
                if (_spotLight != null) _spotLight.Position = new Vector3(_vm.SpotLightX, _vm.SpotLightY, _vm.SpotLightZ);
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.SpotLightRotX):
            case nameof(DebugTestViewModel.SpotLightRotY):
            case nameof(DebugTestViewModel.SpotLightRotZ):
                if (_spotLight != null) _spotLight.RotationDegrees = new Vector3(_vm.SpotLightRotX, _vm.SpotLightRotY, _vm.SpotLightRotZ);
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.SpotLightIntensity):
                if (_spotLight != null) _spotLight.LuminousIntensity = _vm.SpotLightIntensity;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.SpotLightColor):
                if (_spotLight != null) _spotLight.LightColor = ToSystemColor(_vm.SpotLightColor);
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.SpotLightInnerAngle):
                if (_spotLight != null) _spotLight.InnerConeAngleDegree = _vm.SpotLightInnerAngle;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.SpotLightOuterAngle):
                if (_spotLight != null) _spotLight.OuterAngleDegree = _vm.SpotLightOuterAngle;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.SpotLightRadius):
                if (_spotLight != null) _spotLight.AttenuationRadius = _vm.SpotLightRadius;
                aura3DView.RequestNextFrameRendering();
                break;

            // Fire position
            case nameof(DebugTestViewModel.FirePosX):
            case nameof(DebugTestViewModel.FirePosY):
            case nameof(DebugTestViewModel.FirePosZ):
                if (_fireGroup != null)
                    _fireGroup.Position = new Vector3(_vm.FirePosX, _vm.FirePosY, _vm.FirePosZ);
                aura3DView.RequestNextFrameRendering();
                break;

            // Dust position / rotation
            case nameof(DebugTestViewModel.DustPosX):
            case nameof(DebugTestViewModel.DustPosY):
            case nameof(DebugTestViewModel.DustPosZ):
            case nameof(DebugTestViewModel.DustRotX):
            case nameof(DebugTestViewModel.DustRotY):
            case nameof(DebugTestViewModel.DustRotZ):
                if (_dustParticles != null)
                {
                    _dustParticles.Position = new Vector3(_vm.DustPosX, _vm.DustPosY, _vm.DustPosZ);
                    _dustParticles.RotationDegrees = new Vector3(_vm.DustRotX, _vm.DustRotY, _vm.DustRotZ);
                }
                aura3DView.RequestNextFrameRendering();
                break;

            // Culling
            case nameof(DebugTestViewModel.EnableFrustumCulling):
                scene.RenderPipeline.EnableFrustumCulling = _vm.EnableFrustumCulling;
                aura3DView.RequestNextFrameRendering();
                break;
            case nameof(DebugTestViewModel.BoundingBoxPadding):
                ApplyBoundingBoxPadding();
                aura3DView.RequestNextFrameRendering();
                break;
        }
    }

    private void ApplyBoundingBoxPadding()
    {
        if (_vm == null) return;
        foreach (var node in _sceneNodes)
        {
            if (node is Model model && model.IsSkinnedModel)
            {
                model.BoundingBoxPadding = _vm.BoundingBoxPadding;
                foreach (var mesh in model.Meshes)
                    mesh.UpdateWorldBoundingBox();
            }
        }
    }

    // ─── Picking ─────────────────────────────────────────────

    private void OnObjectPicked(object? sender, ObjectPickedEventArgs e)
    {
        if (_vm == null) return;

        var result = e.PickResult;
        string typeName = result.Node switch
        {
            InstancedMesh => "InstancedMesh",
            Mesh => "Mesh",
            Model => "Model",
            _ => result.Node.GetType().Name
        };

        string instanceInfo = result.InstanceIndex.HasValue
            ? $" [Instance#{result.InstanceIndex.Value}]"
            : "";

        bool isSkinned = (result.Node is Model m && m.IsSkinnedModel) ||
                         (result.Node is Mesh mesh && mesh.IsSkinnedMesh);

        string skinnedTag = isSkinned ? " [Skinned]" : "";

        _vm.PickInfo = $"[{typeName}]{skinnedTag} {result.Node.Name}{instanceInfo} | "
            + $"Dist: {result.Distance:F2} | ({result.WorldPosition.X:F2}, {result.WorldPosition.Y:F2}, {result.WorldPosition.Z:F2})";
    }

    private static System.Drawing.Color ToSystemColor(Avalonia.Media.Color c) =>
        System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);

    // ─── Per-Frame Update ────────────────────────────────────

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        if (_vm == null) return;

        // FPS
        if (_deltaTimes.Count >= 30) _deltaTimes.RemoveAt(0);
        _deltaTimes.Add(args.DeltaTime);
        var fps = (int)(1 / _deltaTimes.Average());

        if (_fpsFrameCount == 0)
        {
            _fpsMin = fps;
            _fpsMax = fps;
            _fpsSum = 0;
        }
        if (fps < _fpsMin) _fpsMin = fps;
        if (fps > _fpsMax) _fpsMax = fps;
        _fpsSum += fps;
        _fpsFrameCount++;

        _vm.CurrentFps = fps;
        _vm.MinFps = _fpsMin;
        _vm.MaxFps = _fpsMax;
        _vm.AvgFps = (int)(_fpsSum / _fpsFrameCount);

        double barRatio = Math.Clamp(fps / 200.0, 0.0, 1.0);
        _vm.FpsBarWidth = barRatio * 200;
        _vm.FpsBarColor = fps switch
        {
            < 30 => "#FF6B6B",
            < 60 => "#FFD93D",
            _ => "#6BCB77"
        };

        // 可见性统计（统计 Model 维度，与 SkinnedMeshCullingPage 一致）
        var visible = aura3DView.Scene?.RenderPipeline.VisibleMeshesInCamera;
        if (visible != null)
        {
            var visibleModels = new HashSet<Model>();
            foreach (var m in visible)
            {
                if (m.Model != null)
                    visibleModels.Add(m.Model);
            }
            _vm.VisibleCount = visibleModels.Count;
        }

        aura3DView.RequestNextFrameRendering();
    }
}
