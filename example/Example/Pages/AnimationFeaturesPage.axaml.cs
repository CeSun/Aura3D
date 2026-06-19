using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Particles;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class AnimationFeaturesPage : UserControl
{
    private CameraController _cameraController = null!;

    public AnimationFeaturesPage()
    {
        InitializeComponent();

        _cameraController = new CameraController(aura3DView)
        {
            MoveSpeed = 100f
        };

        // Wire up loop mode radio buttons
        loopRadio.IsChecked = true;
        loopRadio.IsCheckedChanged += (s, e) =>
        {
            if (s is RadioButton rb && rb.IsChecked == true)
            {
                if (DataContext is AnimationFeaturesViewModel v) v.LoopModeIndex = 0;
                ApplyLoopMode(LoopMode.Loop);
            }
        };
        onceRadio.IsCheckedChanged += (s, e) =>
        {
            if (s is RadioButton rb && rb.IsChecked == true)
            {
                if (DataContext is AnimationFeaturesViewModel v) v.LoopModeIndex = 1;
                ApplyLoopMode(LoopMode.Once);
            }
        };
        pingPongRadio.IsCheckedChanged += (s, e) =>
        {
            if (s is RadioButton rb && rb.IsChecked == true)
            {
                if (DataContext is AnimationFeaturesViewModel v) v.LoopModeIndex = 2;
                ApplyLoopMode(LoopMode.PingPong);
            }
        };

        // Wire up Reset button
        resetButton.Click += (s, e) =>
        {
            animationSampler?.Reset();
            UpdatePlaybackStatus();
        };

        // Wire up External Update checkbox
        externalUpdateCheck.IsCheckedChanged += (s, e) => OnExternalUpdateChanged();

        // Wire up speed slider
        speedSlider.ValueChanged += (s, e) =>
        {
            if (animationSampler != null)
                animationSampler.TimeScale = (float)((Slider)s!).Value;
        };

        // Wire up animation combo
        animationCombo.SelectionChanged += (s, e) =>
        {
            if (model == null || animations.Count == 0)
                return;
            if (DataContext is AnimationFeaturesViewModel v2 && !string.IsNullOrEmpty(v2.SelectedAnimation))
            {
                SwitchToAnimation(v2.SelectedAnimation);
            }
        };

        // Demo mode switching
        demoModeCombo.SelectionChanged += (s, e) =>
        {
            if (model == null)
                return;
            if (DataContext is AnimationFeaturesViewModel v3)
            {
                SwitchDemoMode(v3.DemoModeIndex);
            }
        };

        // Graph speed slider
        graphSpeedSlider.ValueChanged += (s, e) =>
        {
            if (DataContext is AnimationFeaturesViewModel v4)
                v4.GraphSpeed = (float)((Slider)s!).Value;
        };

        // Blend space sliders
        blendXSlider.ValueChanged += (s, e) =>
        {
            if (DataContext is AnimationFeaturesViewModel v5)
                v5.BlendX = ((Slider)s!).Value;
        };
        blendYSlider.ValueChanged += (s, e) =>
        {
            if (DataContext is AnimationFeaturesViewModel v6)
                v6.BlendY = ((Slider)s!).Value;
        };
    }

    // ─── Fields ────────────────────────────────────────────────

    List<Animation> animations = [];
    Model? model;
    AnimationSampler? animationSampler;
    AnimationGraph? animationGraph;
    AnimationBlendSpace? animationBlendSpace;
    BoneAttachment? rightHandAttachment;

    // ─── Scene Initialization ─────────────────────────────────

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        if (sender is not Aura3DView view)
            return;

        // === Load SK_Mannequin model via Assimp (no animations embedded) ===
        using (var stream = AssetLoader.Open(new Uri("avares://Example/Assets/Models/SK_Mannequin.FBX")))
        {
            model = AssimpLoader.Load(stream, "fbx");
        }

        // === Load 7 external animation files ===
        string[] animFiles =
        [
            "Idle_Rifle_Hip.FBX",          // [0] Idle
            "Jog_Fwd_Rifle.FBX",           // [1] Jog Forward
            "Jog_Bwd_Rifle.FBX",           // [2] Jog Backward
            "Jog_Lt_Rifle.FBX",            // [3] Jog Left
            "Jog_Rt_Rifle.FBX",            // [4] Jog Right
            "AS_Rifle_WalkBwdLeft_Aim.FBX",// [5] Walk Back-Left
            "AS_Rifle_WalkBwdRight_Aim.FBX"// [6] Walk Back-Right
        ];

        foreach (var file in animFiles)
        {
            using var stream = AssetLoader.Open(
                new Uri($"avares://Example/Assets/Models/{file}"));
            animations.AddRange(
                AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx"));
        }

        if (DataContext is not AnimationFeaturesViewModel vm)
            return;

        // Populate animation name list
        vm.Animations.Clear();
        string[] displayNames =
        [
            "Idle (Rifle Hip)",
            "Jog Forward",
            "Jog Backward",
            "Jog Left",
            "Jog Right",
            "Walk Back-Left",
            "Walk Back-Right"
        ];
        for (int i = 0; i < animations.Count; i++)
        {
            vm.Animations.Add(displayNames[i]);
        }
        vm.SelectedAnimation = displayNames[0];

        // === Create basic animation sampler (default mode) ===
        animationSampler = new AnimationSampler(animations[0])
        {
            TimeScale = 1.0f,
            LoopMode = LoopMode.Loop
        };
        model.AnimationSampler = animationSampler;

        // === Pre-build Animation Graph: Idle ↔ Jog Forward ===
        var graphVm = vm;
        var idleNode = new AnimationGraphNode(new AnimationSampler(animations[0]))
        {
            BlendTime = 0.5f
        };
        var jogNode = new AnimationGraphNode(new AnimationSampler(animations[1]))
        {
            BlendTime = 0.3f
        };

        idleNode.AddNextNode((sampler, dt) => graphVm.GraphSpeed > 150f, jogNode);
        jogNode.AddNextNode((sampler, dt) => graphVm.GraphSpeed < 150f, idleNode);

        animationGraph = new AnimationGraph(model.Skeleton, idleNode);

        // === Pre-build Blend Space with all 7 animations ===
        animationBlendSpace = new AnimationBlendSpace(model.Skeleton)
        {
            IdwPower = 2f  // Inverse Distance Weighting exponent
        };

        animationBlendSpace.AddAnimationSampler(new Vector2(0, 0),      // Idle at center
            new AnimationSampler(animations[0]));
        animationBlendSpace.AddAnimationSampler(new Vector2(0, 1),      // Forward
            new AnimationSampler(animations[1]));
        animationBlendSpace.AddAnimationSampler(new Vector2(0, -1),     // Backward
            new AnimationSampler(animations[2]));
        animationBlendSpace.AddAnimationSampler(new Vector2(-1, 0),     // Left
            new AnimationSampler(animations[3]));
        animationBlendSpace.AddAnimationSampler(new Vector2(1, 0),      // Right
            new AnimationSampler(animations[4]));
        animationBlendSpace.AddAnimationSampler(new Vector2(-1, -1),    // Diagonal: Back-Left
            new AnimationSampler(animations[5]));
        animationBlendSpace.AddAnimationSampler(new Vector2(1, -1),     // Diagonal: Back-Right
            new AnimationSampler(animations[6]));

        // Compute initial blend pose to avoid T-pose on first frame
        animationBlendSpace.InitializePose();

        // === Lighting ===
        var dl = new DirectionalLight
        {
            RotationDegrees = new Vector3(-30, 60, 0),
            LightColor = Color.White,
            CastShadow = true
        };
        dl.ShadowConfig.FarPlane = 1000;
        dl.ShadowConfig.NearPlane = 10;
        dl.ShadowConfig.Width = 500;
        dl.ShadowConfig.Height = 500;
        view.AddNode(dl);

        // === Ground plane ===
        var mesh = new Mesh
        {
            Geometry = new Aura3D.Core.Geometries.PlaneGeometry(400, 400),
            Material = new Material()
            {
                BaseColor = Texture.CreateFromColor(Color.Gray)
            }
        };
        
        view.AddNode(mesh);

        // === Add model to scene ===
        view.AddNode(model);

        // === Bone Attachment Demo ===
        SetupBoneAttachments(view);

        view.MainCamera.FitToBoundingBox(model.BoundingBox, 0.5f);
        view.MainCamera.Position += view.MainCamera.Up * 100;
        view.MainCamera.RotationDegrees = new Vector3(-30, 0, 0);

        UpdatePlaybackStatus();
    }

    // ─── Per-Frame Update ────────────────────────────────────

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        if (model == null || DataContext is not AnimationFeaturesViewModel vm)
            return;

        // Handle ExternalUpdate for basic mode
        if (vm.IsBasicMode && vm.IsExternalUpdate && animationSampler != null)
        {
            animationSampler.Update(e.DeltaTime);
        }

        // Handle ExternalUpdate for Graph mode
        if (vm.IsGraphMode && vm.IsExternalUpdate && animationGraph != null)
        {
            animationGraph.Update(e.DeltaTime);
        }

        // Handle ExternalUpdate for BlendSpace mode
        if (vm.IsBlendSpaceMode && vm.IsExternalUpdate && animationBlendSpace != null)
        {
            animationBlendSpace.Update(e.DeltaTime);
        }

        // Update blend space axis each frame
        if (vm.IsBlendSpaceMode && animationBlendSpace != null)
        {
            animationBlendSpace.SetAxis((float)vm.BlendX, (float)vm.BlendY);
        }

        // Update BoneAttachment LocalOffset from ViewModel
        if (rightHandAttachment != null)
        {
            var degToRad = MathF.PI / 180f;
            rightHandAttachment.LocalOffset =
                Matrix4x4.CreateTranslation((float)vm.OffsetX, (float)vm.OffsetY, (float)vm.OffsetZ)
                * Matrix4x4.CreateFromYawPitchRoll(
                    (float)(vm.OffsetYaw * degToRad),
                    (float)(vm.OffsetPitch * degToRad),
                    (float)(vm.OffsetRoll * degToRad));
        }
    }

    // ─── Core Operations ─────────────────────────────────────

    private void SwitchToAnimation(string animName)
    {
        if (model == null || DataContext is not AnimationFeaturesViewModel vm)
            return;

        var target = animations.FirstOrDefault(a =>
        {
            // Match by display name → index
            var idx = vm.Animations.IndexOf(animName);
            return idx >= 0 && idx < animations.Count &&
                   animations[idx] == a;
        });

        // Fall back to index-based lookup
        var displayIdx = vm.Animations.IndexOf(animName);
        if (displayIdx < 0 || displayIdx >= animations.Count)
            return;
        target = animations[displayIdx];

        var currentSpeed = animationSampler?.TimeScale ?? 1.0f;
        var currentLoop = animationSampler?.LoopMode ?? LoopMode.Loop;

        animationSampler = new AnimationSampler(target)
        {
            TimeScale = currentSpeed,
            LoopMode = currentLoop,
            ExternalUpdate = vm.IsExternalUpdate
        };

        if (vm.IsBasicMode)
        {
            model.AnimationSampler = animationSampler;
        }

        UpdatePlaybackStatus();
    }

    private void ApplyLoopMode(LoopMode mode)
    {
        if (animationSampler != null)
            animationSampler.LoopMode = mode;
        UpdatePlaybackStatus();
    }

    private void SwitchDemoMode(int modeIndex)
    {
        if (model == null || DataContext is not AnimationFeaturesViewModel vm)
            return;

        switch (modeIndex)
        {
            case 0: // Basic
                if (animationSampler == null)
                {
                    var currentAnim = animations[0];
                    animationSampler = new AnimationSampler(currentAnim)
                    {
                        TimeScale = (float)vm.Speed,
                        LoopMode = (LoopMode)vm.LoopModeIndex,
                        ExternalUpdate = vm.IsExternalUpdate
                    };
                }
                model.AnimationSampler = animationSampler;
                break;

            case 1: // Graph
                animationGraph?.Reset();
                model.AnimationSampler = animationGraph!;
                break;

            case 2: // BlendSpace
                animationBlendSpace?.Reset();
                model.AnimationSampler = animationBlendSpace!;
                break;
        }
    }

    private void OnExternalUpdateChanged()
    {
        if (DataContext is not AnimationFeaturesViewModel vm)
            return;

        var external = vm.IsExternalUpdate;

        if (animationSampler != null)
            animationSampler.ExternalUpdate = external;
        if (animationGraph != null)
            animationGraph.ExternalUpdate = external;
        if (animationBlendSpace != null)
            animationBlendSpace.ExternalUpdate = external;

        UpdatePlaybackStatus();
    }

    private void SetupBoneAttachments(Aura3DView view)
    {
        if (model?.Skeleton == null)
            return;

        var boneMap = model.Skeleton.GetBoneIndexMap();

        // 输出所有骨骼名，便于调试
        System.Diagnostics.Debug.WriteLine("=== Skeleton Bones ===");
        foreach (var kv in boneMap)
            System.Diagnostics.Debug.WriteLine($"  [{kv.Value}] {kv.Key}");

        // 按匹配优先级查找右手骨骼
        string? rightHandName = null;
        var boneNames = boneMap.Keys.ToList();

        // 策略1：精确匹配 hand_r / hand_r_end
        rightHandName = boneNames.FirstOrDefault(b =>
        {
            var l = b.ToLowerInvariant();
            return l.EndsWith("hand_r") || l.EndsWith("hand_r_end");
        });

        // 策略2：包含 right + hand
        if (rightHandName == null)
            rightHandName = boneNames.FirstOrDefault(b =>
            {
                var l = b.ToLowerInvariant();
                return l.Contains("righthand") || l.Contains("right_hand");
            });

        // 策略3：包含 hand_r
        if (rightHandName == null)
            rightHandName = boneNames.FirstOrDefault(b =>
                b.ToLowerInvariant().Contains("hand_r"));

        // 策略4：兜底，包含 hand
        if (rightHandName == null)
            rightHandName = boneNames.FirstOrDefault(b =>
                b.ToLowerInvariant().Contains("hand"));

        System.Diagnostics.Debug.WriteLine($"Right hand bone: {rightHandName ?? "NOT FOUND"}");

        // 取模型层级中的第一个 Mesh，使用其 WorldTransform
        //（正确传递 Model 与 Mesh 之间可能的中间节点变换）
        var targetMesh = model.Meshes.FirstOrDefault();
        if (rightHandName != null && targetMesh != null)
        {
            rightHandAttachment = new BoneAttachment
            {
                Name = "RightHandAttachment",
                Mesh = targetMesh,
                BoneName = rightHandName,
                // 沿 X 轴旋转 90 度让圆柱从手部向前伸出
                LocalOffset = Matrix4x4.CreateFromYawPitchRoll(0, MathF.PI / 2, 0)
            };

            // 火把柄：细长圆柱
            var torchHandle = new Mesh
            {
                Name = "TorchHandle",
                Geometry = new Aura3D.Core.Geometries.CylinderGeometry(1.5f, 1.5f, 30, 16),
                Material = new Material()
                {
                    BaseColor = Texture.CreateFromColor(Color.FromArgb(255, 139, 90, 43))
                }
            };

            // 火焰粒子系统
            var fire = new ParticleSystem
            {
                Name = "TorchFire",
                MaxParticles = 200
            };
            // 放在手柄顶端
            fire.Position = new Vector3(0, 15, 0);

            // 内焰：亮黄/白色，小而快
            fire.Emitters.Add(new ParticleEmitter
            {
                BlendMode = BlendMode.Translucent,
                EmissionRate = 80f,
                Shape = EmissionShape.Cone,
                ConeAngle = 10f,
                Lifetime = new RangeFloat(0.3f, 0.8f),
                StartSize = new RangeFloat(3f, 6f),
                EndSize = new RangeFloat(1f, 2f),
                StartColor = Color.FromArgb(255, 255, 200, 50),
                EndColor = Color.FromArgb(0, 255, 100, 20),
                Velocity = new RangeVector3(new(-1, 8, -1), new(1, 15, 1)),
                Gravity = new Vector3(0, -2, 0)
            });

            // 外焰：橙红，大而慢
            fire.Emitters.Add(new ParticleEmitter
            {
                BlendMode = BlendMode.Translucent,
                EmissionRate = 40f,
                Shape = EmissionShape.Cone,
                ConeAngle = 15f,
                Lifetime = new RangeFloat(0.5f, 1.2f),
                StartSize = new RangeFloat(5f, 10f),
                EndSize = new RangeFloat(2f, 4f),
                StartColor = Color.FromArgb(255, 255, 120, 20),
                EndColor = Color.FromArgb(0, 200, 40, 10),
                Velocity = new RangeVector3(new(-1.5f, 5, -1.5f), new(1.5f, 12, 1.5f)),
                Gravity = new Vector3(0, -1, 0)
            });

            torchHandle.AddChild(fire, AttachToParentRule.KeepLocal);
            fire.Play();

            // 点光源跟随火焰
            var torchLight = new PointLight
            {
                Name = "TorchLight",
                LightColor = Color.FromArgb(255, 255, 160, 60),
                LuminousIntensity = 1000f,
                AttenuationRadius = 100f
            };
            torchLight.Position = new Vector3(0, 15, 0);
            torchHandle.AddChild(torchLight, AttachToParentRule.KeepLocal);

            rightHandAttachment.AddChild(torchHandle, AttachToParentRule.KeepLocal);
            view.AddNode(rightHandAttachment);
        }
    }

    private void UpdatePlaybackStatus()
    {
        if (DataContext is not AnimationFeaturesViewModel vm)
            return;

        var loopStr = (animationSampler?.LoopMode ?? LoopMode.Loop) switch
        {
            LoopMode.Loop => "Loop",
            LoopMode.Once => "Once",
            LoopMode.PingPong => "PingPong",
            _ => "Unknown"
        };

        var speed = animationSampler?.TimeScale ?? 1.0f;
        var external = vm.IsExternalUpdate ? "Manual Update" : "Auto (System Clock)";

        vm.PlaybackStatus = $"Mode: {loopStr} | Speed: {speed:F1}× | {external}";
    }
}
