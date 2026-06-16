using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using System.Drawing;
using System.Numerics;
using Silk.NET.Input;
using Aura3D.Model;

namespace RenderDebug;

public class TestView
{
    private Scene scene;

    private IInputContext inputContext;

    Vector2 point = new(-1, -1);

    double deltaTime = 0;

    InstancedMesh? instancedMesh;
    List<float> instanceRotationAngles = new();
    List<float> instanceRotationSpeeds = new();
    List<Vector3> instancePositions = new();

    Func<string, Stream> loadFileFun;

    public TestView(Scene scene, IInputContext inputContext, Func<string, Stream> loadFileFun)
    {
        this.scene = scene;
        this.inputContext = inputContext;
        this.loadFileFun = loadFileFun;
    }

    public void OnInit()
    {
        /*
        using var hdriFileStream = loadFileFun("Textures/buikslotermeerplein_1k.hdr");

        var hdriTexture = TextureLoader.LoadHdrTexture(hdriFileStream);

        var cubemap = HDRIToCubeTextureConverter.ConvertFromTexture(hdriTexture, 1024);

        scene.Background = cubemap;
        */
        var m = inputContext.Mice.First();

        m.MouseMove += (m, p) =>
        {
            if (m.IsButtonPressed(MouseButton.Left) == false)
                return;

            var newPosition = m.Position;
          
            var delta = newPosition - point;

            if (scene.MainCamera != null)
            {
                scene.MainCamera!.RotationDegrees = new Vector3(
                    (float)(scene.MainCamera.RotationDegrees.X + (float)delta.Y * (float)deltaTime * 20),
                    (float)(scene.MainCamera.RotationDegrees.Y + (float)delta.X * (float)deltaTime * 20), 0);
            }

            point = newPosition;
        };


        m.MouseDown += (m, p) =>
        {
            point = m.Position;
        };


        var camera = scene.MainCamera;
        // 大面积地面 — 用来接收阴影
        var ground = new Mesh
        {
            Geometry = new PlaneGeometry(),
            Material = new Material
            {
                BaseColor = Texture.CreateFromColor(Color.FromArgb(220, 220, 220))
            }
        };
        ground.Scale = new Vector3(80, 1, 80);
        ground.Position = new Vector3(0, -2, 0);
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
                    Material = new Material
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
                    }
                };
                mesh.Position = new Vector3(x * 4, 2, z);
                mesh.Scale = new Vector3(1.5f);
                scene.AddNode(mesh);
            }

            // 每个距离放一个高柱子用于投影
            var tallPillar = new Mesh
            {
                Geometry = new CylinderGeometry(),
                Material = new Material
                {
                    BaseColor = Texture.CreateFromColor(Color.White)
                }
            };
            tallPillar.Position = new Vector3(10, 5, z);
            tallPillar.Scale = new Vector3(1, 8, 1);
            scene.AddNode(tallPillar);
        }

        // 方向光（投射阴影）
        var _dl = new DirectionalLight
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
    }

    public void OnUpdate(double deltaTime)
    {
        var kb = inputContext.Keyboards.First();

        this.deltaTime = deltaTime;

        if (kb.IsKeyPressed(Key.W))
        {
            scene.MainCamera!.Position += scene.MainCamera.Forward * 0.1F * (float)deltaTime;
        }

        if (kb.IsKeyPressed(Key.S))
        {
            scene.MainCamera!.Position += scene.MainCamera.Backward * 0.1F * (float)deltaTime;
        }

        if (kb.IsKeyPressed(Key.A))
        {
            scene.MainCamera!.Position += scene.MainCamera.Left * 0.1F * (float)deltaTime;
        }

        if (kb.IsKeyPressed(Key.D))
        {
            scene.MainCamera!.Position += scene.MainCamera.Right * 0.1F * (float)deltaTime;
        }

        /*
        // Animate instanced mesh around the camera
        if (instancedMesh != null)
        {
            var camPos = scene.MainCamera!.Position;

            for (int i = 0; i < instancedMesh.InstanceCount; i++)
            {
                instanceRotationAngles[i] += instanceRotationSpeeds[i] * (float)deltaTime;

                Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(
                    instanceRotationAngles[i],
                    instanceRotationAngles[i] * 0.7f,
                    instanceRotationAngles[i] * 0.3f);

                Matrix4x4 transform = rotation * Matrix4x4.CreateTranslation(camPos + instancePositions[i]);
                instancedMesh.UpdateInstance(i, transform);
            }
        }
        */
    }

}
