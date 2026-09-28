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

        // Skybox
        using (var stream = AssetLoader.Open(new Uri("avares://Example/Assets/Textures/buikslotermeerplein_1k.hdr")))
        {
            var hdri = TextureLoader.LoadHdrTexture(stream);
            scene.Background = HDRIToCubeTextureConverter.ConvertFromTexture(hdri, 1024);
        }

        // Large ground plane - used to receive shadows
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

        // Place pillars at near and far distances to show the CSM cascade effect
        var sphereGeo = new SphereGeometry();
        float[] distances = { 3, 8, 18, 35, 60, 100 };
        for (int d = 0; d < distances.Length; d++)
        {
            float z = distances[d];

            // One row of spheres per distance
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
                                0 => Color.FromArgb(255, 60, 60),    // near - red
                                1 => Color.FromArgb(255, 160, 60),   // orange
                                2 => Color.FromArgb(255, 220, 60),   // yellow
                                3 => Color.FromArgb(60, 200, 60),    // green
                                4 => Color.FromArgb(60, 120, 220),   // blue
                                _ => Color.FromArgb(180, 100, 220),  // purple
                            })
                    },
                    Position = new Vector3(x * 4, 2, z),
                    Scale = new Vector3(1.5f)
                };
                scene.AddNode(mesh);
            }

            // One tall pillar per distance to cast shadows
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

        // Directional light (casts shadows)
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

        // Camera position
        scene.MainCamera.Position = new Vector3(8, 12, -8);
        scene.MainCamera.RotationDegrees = new Vector3(-30, -25, 0);

        UpdateDebugInfo();
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        // Slowly rotate the light source to observe shadow changes
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
