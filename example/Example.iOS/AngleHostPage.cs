using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Aura3D.Pipeline.PBR;
using Color = System.Drawing.Color;
using Geometry = Aura3D.Core.Resources.Geometry;
using Grid = Avalonia.Controls.Grid;

namespace Example.iOS;

/// <summary>
/// T4 验证页：复刻 Example 各风险点场景内容，在 ANGLE 宿主上逐项验证（临时验证代码，随宿主一起删除）。
/// </summary>
public static class AngleHostPage
{
    /// <summary>ANGLE 宿主页（配合默认 Metal 合成模式），顶部按钮切换场景。</summary>
    public static Control BuildAnglePage()
    {
        var content = new Grid();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 40, 8, 4),
        };

        void Load(string name)
        {
            content.Children.Clear();
            content.Children.Add(name switch
            {
                "Points" => BuildPointsScene(),
                "CSM" => BuildCsmScene(fxaa: true),
                "CSM noFXAA" => BuildCsmScene(fxaa: false),
                _ => BuildBaseScene(),
            });
        }

        foreach (var name in new[] { "Base", "Points", "CSM", "CSM noFXAA" })
        {
            var btn = new Button { Content = name, Padding = new Thickness(10, 4) };
            btn.Click += (s, e) => Load(((Button)s).Content?.ToString() ?? "Base");
            buttons.Children.Add(btn);
        }

        Load(Environment.GetEnvironmentVariable("AURA_SCENE") ?? "Base");

        var page = new DockPanel { Background = Brushes.White };
        DockPanel.SetDock(buttons, Dock.Top);
        page.Children.Add(buttons);
        page.Children.Add(content);
        return page;
    }

    private static Control HostPage(string title, AngleSceneHost host, TextBlock status)
    {
        var page = new Grid();
        page.Children.Add(host);
        page.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            Foreground = Brushes.Black,
            Margin = new Thickness(10, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        });
        status.Margin = new Thickness(10);
        status.HorizontalAlignment = HorizontalAlignment.Left;
        status.VerticalAlignment = VerticalAlignment.Bottom;
        status.FontSize = 13;
        status.Foreground = Brushes.Black;
        page.Children.Add(status);
        return page;
    }

    private static TextBlock NewStatus() => new();

    // ---- Base Geometries（与 BaseGeometriesPage 一致）----

    private static Control BuildBaseScene()
    {
        var background = LoadTexture("avares://Example/Assets/Textures/background.jpg");
        var status = NewStatus();
        Action<double>? animate = null;

        var host = new AngleSceneHost
        {
            Status = status,
            SceneConfigured = scene => animate = AttachBaseGeometries(scene, background),
            FrameUpdating = (scene, dt) => animate?.Invoke(dt),
        };
        return HostPage("Base Geometries on ANGLE", host, status);
    }

    private static Texture LoadTexture(string uri)
    {
        using var stream = AssetLoader.Open(new Uri(uri));
        return TextureLoader.LoadTexture(stream);
    }

    private static Action<double> AttachBaseGeometries(Scene scene, Texture background)
    {
        var mesh = new Mesh
        {
            Geometry = new BoxGeometry(),
            Material = new Material
            {
                BaseColor = background,
                BlendMode = BlendMode.Opaque,
                DoubleSided = true,
            },
            Position = scene.MainCamera.Position + scene.MainCamera.Forward * 3,
            RotationDegrees = new Vector3(90, 0, 0),
        };
        scene.AddNode(mesh);

        scene.AddNode(new DirectionalLight
        {
            RotationDegrees = new Vector3(-30, 0, 0),
            LightColor = Color.White,
        });

        float pitch = 0;
        return dt =>
        {
            pitch += (float)(dt * 10);
            mesh.RotationDegrees = new Vector3(pitch, 0, 0);
        };
    }

    // ---- PointCloud（F9：点尺寸限制，与 PointCloudPage 默认参数一致）----

    private static Control BuildPointsScene()
    {
        var status = NewStatus();
        var rand = new Random(42);

        var host = new AngleSceneHost
        {
            Status = status,
            CreateRenderPipeline = scene => new PointCloudPipeline(scene),
            SceneConfigured = scene =>
            {
                scene.MainCamera.Position = new Vector3(0, 0, 30);
                scene.MainCamera.RotationDegrees = new Vector3(0, 0, 0);

                scene.AddNode(new DirectionalLight
                {
                    RotationDegrees = new Vector3(-30, -15, 0),
                    LightColor = Color.White,
                });

                const int pointCount = 20000;
                const float sizeX = 20f, sizeY = 20f, sizeZ = 20f, pointSize = 5f;
                const int gridDivisions = 4;

                var material = new Material { BlendMode = BlendMode.Opaque };
                material.SetParameterValue("uPointSize", pointSize);

                var halfSize = Math.Max(Math.Max(sizeX, sizeY), sizeZ) / 2f;
                var cellSize = halfSize * 2 / gridDivisions;

                var cells = new Dictionary<(int, int, int), (List<float> Positions, List<float> Colors)>();

                for (int i = 0; i < pointCount; i++)
                {
                    var position = new Vector3(
                        (float)(rand.NextDouble() - 0.5) * sizeX,
                        (float)(rand.NextDouble() - 0.5) * sizeY,
                        (float)(rand.NextDouble() - 0.5) * sizeZ);
                    var color = new Vector4(
                        (position.X / halfSize + 1f) / 2f,
                        (position.Y / halfSize + 1f) / 2f,
                        (position.Z / halfSize + 1f) / 2f,
                        1.0f);

                    int ix = Math.Clamp((int)MathF.Floor((position.X + halfSize) / cellSize), 0, gridDivisions - 1);
                    int iy = Math.Clamp((int)MathF.Floor((position.Y + halfSize) / cellSize), 0, gridDivisions - 1);
                    int iz = Math.Clamp((int)MathF.Floor((position.Z + halfSize) / cellSize), 0, gridDivisions - 1);
                    var key = (ix, iy, iz);

                    if (!cells.TryGetValue(key, out var cell))
                    {
                        cell = (new List<float>(), new List<float>());
                        cells[key] = cell;
                    }

                    cell.Positions.Add(position.X);
                    cell.Positions.Add(position.Y);
                    cell.Positions.Add(position.Z);
                    cell.Colors.Add(color.X);
                    cell.Colors.Add(color.Y);
                    cell.Colors.Add(color.Z);
                    cell.Colors.Add(color.W);
                }

                foreach (var (_, (positions, colors)) in cells)
                {
                    var geometry = new Geometry();
                    geometry.PrimitiveType = PrimitiveType.Points;
                    geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
                    geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, colors);

                    scene.AddNode(new Mesh { Geometry = geometry, Material = material });
                }
            },
        };
        return HostPage("PointCloud 20000 pts uPointSize=5 (F9)", host, status);
    }

    // ---- CascadedShadowMaps（Texture2DArray 深度附件 + HDR 背景 + FXAA/BlitFramebuffer）----

    private static Control BuildCsmScene(bool fxaa)
    {
        var status = NewStatus();
        DirectionalLight? dl = null;

        var settings = new PipelineSettings
        {
            CsmCascadeCount = int.TryParse(Environment.GetEnvironmentVariable("AURA_CSM_CASCADES"), out var cascades) ? cascades : 3,
            CsmSplitLambda = 0.5f,
            DirectionalLightLimit = 4,
            DepthFormat = Environment.GetEnvironmentVariable("AURA_CSM_DEPTH24") == "1"
                ? new PipelineSettings().DepthFormat
                : TextureFormat.DepthComponent32f,
            EnableFxaa = fxaa,
        };

        var noHdr = Environment.GetEnvironmentVariable("AURA_CSM_NOHDR") == "1";
        var noShadow = Environment.GetEnvironmentVariable("AURA_CSM_NOSHADOW") == "1";

        var host = new AngleSceneHost
        {
            Status = status,
            PipelineSettings = settings,
            CreateRenderPipeline = scene => new PBRDeferredPipeline(scene),
            SceneConfigured = scene =>
            {
                if (!noHdr)
                {
                    using var stream = AssetLoader.Open(new Uri("avares://Example/Assets/Textures/buikslotermeerplein_1k.hdr"));
                    var hdri = TextureLoader.LoadHdrTexture(stream);
                    scene.Background = HDRIToCubeTextureConverter.ConvertFromTexture(hdri, 1024);
                }

                scene.AddNode(new Mesh
                {
                    Geometry = new PlaneGeometry(),
                    Material = new Material
                    {
                        BaseColor = Texture.CreateFromColor(Color.FromArgb(220, 220, 220)),
                    },
                    Scale = new Vector3(80, 1, 80),
                    Position = new Vector3(0, -2, 0),
                });

                var sphereGeo = new SphereGeometry();
                float[] distances = { 3, 8, 18, 35, 60, 100 };
                for (int d = 0; d < distances.Length; d++)
                {
                    float z = distances[d];

                    for (int x = -3; x <= 3; x++)
                    {
                        scene.AddNode(new Mesh
                        {
                            Geometry = sphereGeo,
                            Material = new Material
                            {
                                BaseColor = Texture.CreateFromColor(
                                    d switch
                                    {
                                        0 => Color.FromArgb(255, 60, 60),
                                        1 => Color.FromArgb(255, 160, 60),
                                        2 => Color.FromArgb(255, 220, 60),
                                        3 => Color.FromArgb(60, 200, 60),
                                        4 => Color.FromArgb(60, 120, 220),
                                        _ => Color.FromArgb(180, 100, 220),
                                    })
                            },
                            Position = new Vector3(x * 4, 2, z),
                            Scale = new Vector3(1.5f),
                        });
                    }

                    scene.AddNode(new Mesh
                    {
                        Geometry = new CylinderGeometry(),
                        Material = new Material
                        {
                            BaseColor = Texture.CreateFromColor(Color.White),
                        },
                        Position = new Vector3(10, 5, z),
                        Scale = new Vector3(1, 8, 1),
                    });
                }

                dl = new DirectionalLight
                {
                    RotationDegrees = new Vector3(-35, 22, 0),
                    LightColor = Color.White,
                    CastShadow = !noShadow,
                    ShadowConfig = new DirectionalLightShadowMapConfig
                    {
                        Width = 80,
                        Height = 80,
                        NearPlane = 0.5f,
                        FarPlane = 200,
                    },
                };
                scene.AddNode(dl);
                scene.MainDirectionalLight = dl;

                scene.MainCamera.Position = new Vector3(8, 12, -8);
                // 引擎约定 Forward = (0,0,-1) 旋转，yaw -25 会背对 +z 的球阵（视锥剔除后不可见）。
                // yaw 155 ≈ 原意图的镜像朝向，从 -z 侧看向 +z 球阵。
                scene.MainCamera.RotationDegrees = new Vector3(-30, 155, 0);
            },
            FrameUpdating = (scene, dt) =>
            {
                if (dl != null)
                    dl.RotationDegrees += new Vector3(0, 4, 0) * (float)dt;
            },
        };
        return HostPage($"CSM 3-cascade PBR-Deferred FXAA={(fxaa ? "on" : "off")}", host, status);
    }

    // ---- 现有路径基线页（配合 iOSRenderingMode.OpenGl 强制模式，供 A/B 截图）----

    public static Control BuildGlBaselinePage()
    {
        var background = LoadTexture("avares://Example/Assets/Textures/background.jpg");

        var view = new Aura3DView();
        Action<double>? animate = null;
        view.SceneInitialized += (s, e) =>
        {
            var v = (Aura3DView)s;
            v.AutoRequestNextFrameRendering = false;
            animate = AttachBaseGeometries(e.Scene, background);
            v.RequestNextFrameRendering();
        };
        view.SceneUpdated += (s, e) =>
        {
            animate?.Invoke(e.DeltaTime);
            ((Aura3DView)s).RequestNextFrameRendering();
        };

        var page = new Grid { Background = Brushes.White };
        page.Children.Add(view);
        page.Children.Add(new TextBlock
        {
            Text = "Base Geometries baseline (Apple GL)",
            FontSize = 13,
            Foreground = Brushes.Black,
            Margin = new Thickness(10, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        });
        return page;
    }
}
