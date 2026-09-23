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
using Aura3D.Core.Particles;
using Aura3D.Model;
using Aura3D.Pipeline.CelShading;
using Aura3D.Pipeline.PBR;
using Aura3D.Pipeline.PBRForward;
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
                "Cel" => BuildCelScene(),
                "Forward" => BuildForwardScene(),
                "Particles" => BuildParticlesScene(),
                "Instancing" => BuildInstancingScene(),
                "Primitives" => BuildPrimitivesScene(),
                "Anim" => BuildAnimScene(),
                _ => BuildBaseScene(),
            });
        }

        foreach (var name in new[] { "Base", "Points", "CSM", "CSM noFXAA", "Cel", "Forward", "Particles", "Instancing", "Primitives", "Anim" })
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

    // ---- 全量回归：补齐管线/特性族（CelShading、PBRForward、粒子、实例化、图元类型）----

    private static Control BuildCelScene()
    {
        var status = NewStatus();
        DirectionalLight? dl = null;

        var host = new AngleSceneHost
        {
            Status = status,
            CreateRenderPipeline = scene => new CelShadingPipeline(scene),
            SceneConfigured = scene =>
            {
                var streams = new List<System.IO.Stream>();
                foreach (var f in new[] { "px.png", "nx.png", "py.png", "ny.png", "pz.png", "nz.png" })
                    streams.Add(AssetLoader.Open(new Uri($"avares://Example/Assets/Textures/skybox/{f}")));
                scene.Background = TextureLoader.LoadCubeTexture(streams);
                foreach (var s in streams) s.Dispose();

                var cam = scene.MainCamera;
                dl = new DirectionalLight { RotationDegrees = new Vector3(-45, 45, 0), LightColor = Color.White };
                scene.AddNode(dl);

                using (var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/NPC_Avatar_Girl_Sword_Nilou.glb")))
                {
                    var model = ModelLoader.LoadGlbModel(s);
                    scene.AddNode(model);
                    model.Position = cam.Position + cam.Forward * 10 + Vector3.UnitY * 0.5f;
                    model.Scale = Vector3.One * 2f;
                }
                using (var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/coffee_table_round_01_1k.glb")))
                {
                    var model = ModelLoader.LoadGlbModel(s);
                    scene.AddNode(model);
                    model.Position = cam.Position + cam.Forward * 10 - Vector3.UnitY * 2;
                    model.Scale = Vector3.One * 5f;
                }
                cam.Position += Vector3.UnitY * 2 + cam.Forward * 6;
            },
            FrameUpdating = (scene, dt) =>
            {
                if (dl != null) dl.RotationDegrees += new Vector3(0, 30, 0) * (float)dt;
            },
        };
        return HostPage("CelShading: glb skin + skybox + dir light", host, status);
    }

    private static Control BuildForwardScene()
    {
        var status = NewStatus();
        DirectionalLight? dl = null;

        var host = new AngleSceneHost
        {
            Status = status,
            CreateRenderPipeline = scene => new PBRForwardPipeline(scene),
            SceneConfigured = scene =>
            {
                using var stream = AssetLoader.Open(new Uri("avares://Example/Assets/Textures/buikslotermeerplein_1k.hdr"));
                var hdri = TextureLoader.LoadHdrTexture(stream);
                scene.Background = HDRIToCubeTextureConverter.ConvertFromTexture(hdri, 256);

                scene.AddNode(new Mesh
                {
                    Geometry = new PlaneGeometry(),
                    Material = new Material { BaseColor = Texture.CreateFromColor(Color.FromArgb(200, 200, 200)) },
                    Position = new Vector3(0, -1, 0),
                    Scale = new Vector3(20, 1, 20),
                });
                scene.AddNode(new Mesh
                {
                    Geometry = new SphereGeometry(),
                    Material = new Material { BaseColor = Texture.CreateFromColor(Color.FromArgb(220, 80, 80)) },
                    Position = new Vector3(0, 0.5f, -6),
                });
                scene.MainCamera.Position = new Vector3(0, 2, 2);
                scene.MainCamera.RotationDegrees = new Vector3(-10, 0, 0);

                dl = new DirectionalLight { RotationDegrees = new Vector3(-35, 20, 0), LightColor = Color.White };
                scene.AddNode(dl);
                scene.MainDirectionalLight = dl;
            },
            FrameUpdating = (scene, dt) =>
            {
                if (dl != null) dl.RotationDegrees += new Vector3(0, 20, 0) * (float)dt;
            },
        };
        return HostPage("PBRForward: HDR env + sphere", host, status);
    }

    private static Control BuildParticlesScene()
    {
        var status = NewStatus();

        var host = new AngleSceneHost
        {
            Status = status,
            SceneConfigured = scene =>
            {
                scene.MainCamera.Position = new Vector3(0, 5, 15);
                scene.MainCamera.LookAt(new Vector3(0, 2, 0));

                scene.AddNode(new DirectionalLight { RotationDegrees = new Vector3(-30, 0, 0), LightColor = Color.White });

                var fire = new ParticleSystem { Name = "Fire", MaxParticles = 400, Position = new Vector3(0, 2, 0) };
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
                scene.AddNode(fire);
                fire.Play();
            },
        };
        return HostPage("Particles: 2 emitters translucent (InstancedMesh)", host, status);
    }

    private static Control BuildInstancingScene()
    {
        var status = NewStatus();

        var host = new AngleSceneHost
        {
            Status = status,
            SceneConfigured = scene =>
            {
                scene.MainCamera.Position = new Vector3(0, 12, 28);
                scene.MainCamera.LookAt(new Vector3(0, 0, 0));
                scene.AddNode(new DirectionalLight { RotationDegrees = new Vector3(-35, 20, 0), LightColor = Color.White });

                var mat = new Material { BaseColor = Texture.CreateFromColor(Color.White) };
                var src = new Mesh { Geometry = new BoxGeometry(), Material = mat };
                var inst = InstancedMesh.FromMesh(src);
                for (int x = -6; x <= 6; x++)
                    for (int z = -6; z <= 6; z++)
                        inst.AddInstance(Matrix4x4.CreateTranslation(new Vector3(x * 2, MathF.Sin(x * 0.7f) * MathF.Cos(z * 0.7f) * 2, z * 2)));
                scene.AddNode(inst);
            },
        };
        return HostPage("GPU Instancing 169 boxes (1 draw call)", host, status);
    }

    private const string SolidColorVertexShader = """
        #version 300 es
        precision mediump float;

        //{{defines}}

        layout(location = 0) in vec3 position;

        uniform mat4 modelMatrix;
        uniform mat4 viewMatrix;
        uniform mat4 projectionMatrix;

        void main()
        {
            vec4 worldPosition = modelMatrix * vec4(position, 1.0);
            gl_Position = projectionMatrix * viewMatrix * worldPosition;
            gl_PointSize = 10.0;
        }
        """;

    private const string SolidColorFragmentShader = """
        #version 300 es
        precision mediump float;
        out vec4 outColor;

        uniform vec4 uColor;

        void main()
        {
            outColor = uColor;
        }
        """;

    private static Control BuildPrimitivesScene()
    {
        var status = NewStatus();
        Node? container = null;
        float angle = 0;

        var host = new AngleSceneHost
        {
            Status = status,
            SceneConfigured = scene =>
            {
                scene.MainCamera.Position = new Vector3(0, 3, 20);
                scene.MainCamera.RotationDegrees = new Vector3(-10, 0, 0);
                scene.AddNode(new DirectionalLight { RotationDegrees = new Vector3(-30, -15, 0), LightColor = Color.White });

                container = new Node();
                scene.AddNode(container);

                float x = -7.5f;
                const float step = 2.5f;
                AddPrimitive(container, Aura3D.Core.Resources.PrimitiveType.Triangles,
                    new() { -0.5f, -0.4f, 0, 0.5f, -0.4f, 0, 0f, 0.5f, 0 }, new() { 0u, 1u, 2u },
                    new Vector4(0.2f, 0.4f, 1f, 1f), new Vector3(x, 0, 0)); x += step;
                AddPrimitive(container, Aura3D.Core.Resources.PrimitiveType.Points,
                    new() { 0, 0, 0, 0.3f, 0.25f, 0, -0.25f, 0.35f, 0, 0.15f, -0.3f, 0, -0.35f, -0.1f, 0 }, null,
                    new Vector4(1f, 0.2f, 0.2f, 1f), new Vector3(x, 0, 0)); x += step;
                AddPrimitive(container, Aura3D.Core.Resources.PrimitiveType.Lines,
                    new() { -0.4f, 0, 0, -0.15f, 0.3f, 0, 0, 0.1f, 0, 0.2f, 0.35f, 0, 0.4f, 0, 0, 0.15f, -0.3f, 0 }, null,
                    new Vector4(0.2f, 0.9f, 0.3f, 1f), new Vector3(x, 0, 0)); x += step;
                AddPrimitive(container, Aura3D.Core.Resources.PrimitiveType.LineStrip,
                    new() { -0.5f, -0.3f, 0, -0.2f, 0.35f, 0, 0.1f, -0.35f, 0, 0.45f, 0.3f, 0 }, null,
                    new Vector4(1f, 0.9f, 0.1f, 1f), new Vector3(x, 0, 0)); x += step;
                var loop = new List<float>();
                for (int i = 0; i < 5; i++)
                {
                    float a = (float)(i * Math.PI * 2 / 5 - Math.PI / 2);
                    loop.Add(0.45f * MathF.Cos(a)); loop.Add(0.45f * MathF.Sin(a)); loop.Add(0);
                }
                AddPrimitive(container, Aura3D.Core.Resources.PrimitiveType.LineLoop, loop, null,
                    new Vector4(0.1f, 0.9f, 0.9f, 1f), new Vector3(x, 0, 0)); x += step;
                AddPrimitive(container, Aura3D.Core.Resources.PrimitiveType.TriangleStrip,
                    new() { 0.3f, -0.25f, 0, 0.3f, 0.25f, 0, -0.1f, -0.35f, 0, -0.1f, 0.35f, 0, -0.5f, -0.3f, 0, -0.5f, 0.3f, 0 }, null,
                    new Vector4(0.9f, 0.2f, 0.9f, 1f), new Vector3(x, 0, 0)); x += step;
                var fan = new List<float> { 0f, 0f, 0f };
                for (int i = 0; i <= 6; i++)
                {
                    float a = (float)(i * Math.PI * 2 / 6);
                    fan.Add(0.45f * MathF.Cos(a)); fan.Add(0.45f * MathF.Sin(a)); fan.Add(0);
                }
                AddPrimitive(container, Aura3D.Core.Resources.PrimitiveType.TriangleFan, fan, null,
                    new Vector4(1f, 0.5f, 0.15f, 1f), new Vector3(x, 0, 0));
            },
            FrameUpdating = (scene, dt) =>
            {
                if (container == null) return;
                angle += (float)dt * 20;
                container.RotationDegrees = new Vector3(0, angle, 0);
            },
        };
        return HostPage("PrimitiveType x7 (tri/pt/lines/strip/loop/fan)", host, status);
    }

    private static void AddPrimitive(Node parent, Aura3D.Core.Resources.PrimitiveType type,
        List<float> positions, List<uint>? indices, Vector4 color, Vector3 offset)
    {
        var geometry = new Geometry();
        geometry.PrimitiveType = type;
        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
        if (indices != null && indices.Count > 0)
            geometry.SetIndices(indices);

        var material = new Material { BlendMode = BlendMode.Opaque };
        material.SetShaderSource("LightPass", ShaderType.Vertex, SolidColorVertexShader);
        material.SetShaderSource("LightPass", ShaderType.Fragment, SolidColorFragmentShader);
        material.SetParameterValue("uColor", color);

        var mesh = new Mesh { Geometry = geometry, Material = material, LocalTransform = Matrix4x4.CreateTranslation(offset) };
        parent.AddChild(mesh, AttachToParentRule.KeepLocal);
    }

    private static Control BuildAnimScene()
    {
        var status = NewStatus();

        var host = new AngleSceneHost
        {
            Status = status,
            SceneConfigured = scene =>
            {
                scene.MainCamera.Position = new Vector3(0, 12, 25);
                scene.MainCamera.RotationDegrees = new Vector3(-20, 0, 0);
                scene.AddNode(new DirectionalLight { RotationDegrees = new Vector3(-30, -15, 0), LightColor = Color.White });

                using var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/Soldier.glb"));
                var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(s);
                scene.AddNode(model);
                model.Position = new Vector3(0, 0, 5);
                model.Scale = Vector3.One * 5f;
                model.AnimationSampler = new AnimationSampler(animations[0])
                {
                    TimeScale = 1.0f,
                    LoopMode = LoopMode.Loop,
                };
            },
        };
        return HostPage("Skinned animation: Soldier.glb jog (Loop)", host, status);
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
