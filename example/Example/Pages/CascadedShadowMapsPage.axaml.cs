using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.Drawing;
using System.Numerics;

namespace Example.Pages;

public partial class CascadedShadowMapsPage : UserControl
{
    private CameraController _cameraController;
    private DirectionalLight _dl;

    public CascadedShadowMapsPage()
    {
        InitializeComponent();
    }

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        _cameraController = new CameraController(aura3Dview) { MoveSpeed = 20f };
        var scene = e.Scene;

        // 天空盒
        using (var stream = AssetLoader.Open(new Uri("avares://Example/Assets/Textures/buikslotermeerplein_1k.hdr")))
        {
            var hdri = TextureLoader.LoadHdrTexture(stream);
            scene.Background = HDRIToCubeTextureConverter.ConvertFromTexture(hdri, 1024);
        }

        // 大面积地面 — 用来接收阴影
        var ground = new Mesh
        {
            Geometry = new PlaneGeometry(),
            Material = new Material()
            {
                BaseColor = Texture.CreateFromColor(Color.FromArgb(220, 220, 220))
            },
            Scale = new Vector3(80, 1, 80),
            Position = new Vector3(0, -2, 0)
        };
        scene.AddNode(ground);

        // 在远近不同距离放置柱子，展示 CSM 级联效果
        var sphereGeo = new SphereGeometry();
        float[] distances = { 3, 8, 18, 35, 60, 100 };
        for (int d = 0; d < distances.Length; d++)
        {
            float z = distances[d];

            // 每个距离放一排骨
            for (int x = -3; x <= 3; x++)
            {
                var mesh = new Mesh
                {
                    Geometry = sphereGeo,
                    Material = new Material()
                    {
                        BaseColor = Texture.CreateFromColor(
                            d switch
                            {
                                0 => Color.FromArgb(255, 60, 60),    // 近 — 红
                                1 => Color.FromArgb(255, 160, 60),   // 橙
                                2 => Color.FromArgb(255, 220, 60),   // 黄
                                3 => Color.FromArgb(60, 200, 60),    // 绿
                                4 => Color.FromArgb(60, 120, 220),   // 蓝
                                _ => Color.FromArgb(180, 100, 220),  // 紫
                            })
                    },
                    Position = new Vector3(x * 4, 2, z),
                    Scale = new Vector3(1.5f)
                };
                scene.AddNode(mesh);
            }

            // 每个距离放一个高柱子用于投影
            var tallPillar = new Mesh
            {
                Geometry = new CylinderGeometry(),
                Material = new Material()
                {
                    BaseColor = Texture.CreateFromColor(Color.White)
                },
                Position = new Vector3(10, 5, z),
                Scale = new Vector3(1, 8, 1)
            };
            scene.AddNode(tallPillar);
        }

        // 方向光（投射阴影）
        _dl = new DirectionalLight
        {
            RotationDegrees = new Vector3(-35, 22, 0),
            LightColor = Color.White,
            CastShadow = true,
            ShadowConfig = new DirectionalLightShadowMapConfig
            {
                Width = 80,
                Height = 80,
                NearPlane = 0.5f,
                FarPlane = 200
            }
        };
        scene.AddNode(_dl);
        scene.MainDirectionalLight = _dl;

        // 摄像机位置
        scene.MainCamera.Position = new Vector3(8, 12, -8);
        scene.MainCamera.RotationDegrees = new Vector3(-30, -25, 0);

        UpdateDebugInfo();
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        // 缓慢旋转光源以观察阴影变化
        _dl.RotationDegrees += new Vector3(0, 4, 0) * (float)e.DeltaTime;
    }

    private void CascadeCount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Content?.ToString(), out int count))
        {
            var s = aura3Dview.Scene!.RenderPipeline.Settings;
            s.CsmCascadeCount = count;
            UpdateDebugInfo();
        }
    }

    private void SplitLambda_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && float.TryParse(btn.Content?.ToString(), out float lambda))
        {
            var s = aura3Dview.Scene!.RenderPipeline.Settings;
            s.CsmSplitLambda = lambda;
            UpdateDebugInfo();
        }
    }

    private void UpdateDebugInfo()
    {
        var s = aura3Dview.Scene?.RenderPipeline.Settings;
        if (s == null) return;

        txtInfo.Text = $"CsmCascadeCount: {s.CsmCascadeCount}\n"
                     + $"CsmSplitLambda: {s.CsmSplitLambda}\n"
                     + $"\n"
                     + $"Shader: PBR Deferred\n"
                     + $"DirLight: cast shadow\n"
                     + $"Near: 0.5  Far: 200\n"
                     + $"\n"
                     + $"Cascade=1: no CSM\n"
                     + $"Cascade=2-4: CSM on\n"
                     + $"(recreate pipeline\n"
                     + $" to apply cascade count)";
    }
}
