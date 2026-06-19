using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using System;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;

namespace Example.Pages;

public partial class BaseGeometriesPage : UserControl
{
    public BaseGeometriesPage()
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
        var view = sender as Aura3DView;

        if (view == null)
        {
            return;
        }

        view.AutoRequestNextFrameRendering = false;

        try
        {

            var mesh = new Mesh();

            mesh.Geometry = box;

            mesh.Material = new Material();

            Texture? texture = null;

            texture = await Task.Run(() =>
            {

                using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Textures/background.jpg")))
                {
                    return TextureLoader.LoadTexture(stream);
                }
            });

            mesh.Material.BaseColor = texture;

            mesh.Material.BlendMode = BlendMode.Opaque;

            mesh.Material.DoubleSided = true;

            mesh.Position = view.MainCamera.Position + view.MainCamera.Forward * 3;

            mesh.RotationDegrees = new Vector3(90, 0, 0);

            view.AddNode(mesh);



            var dl = new DirectionalLight();

            dl.RotationDegrees = new Vector3(-30, 0, 0);

            dl.LightColor = Color.White;

            view.AddNode(dl);

            this.mesh = mesh;

        }
        catch (Exception ex)
        {

        }

        view.RequestNextFrameRendering();
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

        (sender as Aura3DView)?.RequestNextFrameRendering();
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
