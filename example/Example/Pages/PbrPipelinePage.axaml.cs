using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using System;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using Ursa.Common;
using Aura3D.Pipeline.PBR;

namespace Example.Pages;

public partial class PbrPipelinePage : UserControl
{
    private CameraController _cameraController;

    public PbrPipelinePage()
    {
        InitializeComponent();
        box = new BoxGeometry();
        sphere = new SphereGeometry();
        cylinder = new CylinderGeometry();
        plane = new PlaneGeometry();
    }

    BoxGeometry box;
    SphereGeometry sphere;
    CylinderGeometry cylinder;
    PlaneGeometry plane;

    private async void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {

        _cameraController = new CameraController(aura3Dview)
        {
            MoveSpeed = 50f
        };
        var scene = e.Scene;
        try
        {

            for(int i = 0; i < 7; i++)
            {

                for(int j = 0; j < 7; j++)
                {

                    var mesh = new Mesh
                    {
                        Geometry = new SphereGeometry(),

                        Material = new Material()
                        {
                            BaseColor = Texture.CreateFromColor(Color.FromArgb(255, 0, 0)),
                            Normal = Texture.CreateFromColor(Color.FromArgb(128, 128, 255)),
                            MetallicRoughness = Texture.CreateFromColor(Color.FromArgb((255 / 7) * i, (255 / 7) * j, 0))
                        }
                    };


                    var v = scene.MainCamera.Position + scene.MainCamera.Forward * 2;

                    mesh.Position = new Vector3(i * 3, j * 3, v.Z);

                    scene.AddNode(mesh);

                }

            }

            scene.MainCamera.Position = new Vector3(-1.1829785F, 8.988152F, 9.307376F);
            scene.MainCamera.RotationDegrees = new Vector3(1.1555548F, -31.027235F, 0);

            
            var dl = new DirectionalLight();

            dl.RotationDegrees = new Vector3(-30, 0, 0);

            dl.LightColor = Color.White;

            scene.AddNode(dl);
            
            using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Textures/buikslotermeerplein_1k.hdr")))
            {
                var hdriTexture = TextureLoader.LoadHdrTexture(stream);

                var cubemap = HDRIToCubeTextureConverter.ConvertFromTexture(hdriTexture, 1024);

                scene.Background = cubemap;
            }

            // 加载狮子头和凳子模型，Z 轴与球阵对齐
            var gridZ = scene.MainCamera.Position.Z + scene.MainCamera.Forward.Z * 2;
            await Task.WhenAll(
                LoadModel("avares://Example/Assets/Models/lion_head_1k.glb",
                    m => { m.Position = new Vector3(-5, 9, gridZ); m.Scale = new Vector3(5f); }),
                LoadModel("avares://Example/Assets/Models/wooden_stool_02_1k.glb",
                    m => { m.Position = new Vector3(5, 9, gridZ); m.Scale = new Vector3(3f); })
            );
        }
        catch (Exception ex)
        {

        }
    }

    float pitch = 0;

    Mesh? mesh;
    Action<double>? update;
    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        if (this.mesh == null)
            return;
        pitch += (float)(e.DeltaTime * 10);
        mesh.RotationDegrees = new Vector3(pitch, 0, 0);
    }

    private async Task LoadModel(string uri, Action<Model> configure)
    {
        try
        {
            var model = await Task.Run(() =>
            {
                using var stream = AssetLoader.Open(new Uri(uri));
                return ModelLoader.LoadGlbModel(stream);
            });
            if (model != null && aura3Dview.Scene != null)
            {
                configure(model);
                aura3Dview.Scene.AddNode(model);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load {uri}: {ex.Message}");
        }
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button == null)
            return;
        if (mesh == null)
            return;
        var s = button.Content?.ToString();
        switch (s)
        {
            case "Box":
                mesh.Geometry = box;
                break;
            case "Sphere":
                mesh.Geometry = sphere;
                break;
            case "Cylinder":
                mesh.Geometry = cylinder;
                break;
            case "Plane":
                mesh.Geometry = plane;
                break;
            default: 
                break;
        }
    }
}
