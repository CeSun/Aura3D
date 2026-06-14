using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;

namespace Example.Pages;

public partial class CelShadingPage : UserControl
{
    DirectionalLight dl;

    private CameraController _cameraController;

    public CelShadingPage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3Dview);
        
    }

    private void Aura3DView_SceneInitialized(object? sender, Aura3D.Avalonia.InitializedRoutedEventArgs e)
    {
        var view = (Aura3DView)sender;

        var camera = view.MainCamera;

        camera.ProjectionType = ProjectionType.Perspective;


        var list = new List<Stream>();
        List<string> name =
        [
            "px.png",
                "nx.png",
                "py.png",
                "ny.png",
                "pz.png",
                "nz.png",
            ];
        foreach (var filename in name)
        {
            var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Textures/skybox/{filename}"));
            list.Add(stream);
        }

        var cubeTexture = TextureLoader.LoadCubeTexture(list);

        foreach (var stream in list)
        {
            stream.Dispose();
        }

        view.Scene.Background = cubeTexture;

        PointLight pl = new PointLight();

        pl.AttenuationRadius = 2f;

        pl.LightColor = Color.Green;

        //view.AddNode(pl);

        PointLight pl2 = new PointLight();

        pl2.AttenuationRadius = 2f;

        pl2.LightColor = Color.Red;

        pl2.CastShadow = true;

        //view.AddNode(pl2);

        dl = new DirectionalLight();

        dl.RotationDegrees = new Vector3(-45, 45, 0);

        dl.CastShadow = false;

        view.AddNode(dl);


        // using (var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/Soldier.glb")))
        using (var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/NPC_Avatar_Girl_Sword_Nilou.glb")))
        {

            //var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(s);
            var model = ModelLoader.LoadGlbModel(s);

            //model.AnimationSampler = new AnimationSampler(animations.First());

            view.AddNode(model);

            model.Position = camera.Position + camera.Forward * 10;

            model.Position += model.Up * 0.5f;

            model.Scale = Vector3.One * 2f;
            // model.Scale = Vector3.One * 0.03f;
            model.RotationDegrees = new Vector3(0, 0, 0);

            pl.Position = model.Position + pl.Up * 2 + pl.Left * 2f;

            pl.Position = pl.Position + pl.Backward * 1;

            pl2.Position = model.Position + pl2.Up * 2 + pl2.Right * 2f;

            pl2.Position = pl2.Position + pl2.Backward * 1;

        }


        using (var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/coffee_table_round_01_1k.glb")))
        {

            var model = ModelLoader.LoadGlbModel(s);

            view.AddNode(model);

            model.Position = camera.Position + camera.Forward * 10;

            model.Position += camera.Down * 2;

            model.Scale = Vector3.One * 5f;
        }

        camera.Position = camera.Position + camera.Up * 2 + camera.Forward * 3;

        camera.Position = camera.Position + camera.Forward * 3;
    }

    private void Aura3DView_SceneUpdated(object? sender, Aura3D.Avalonia.UpdateRoutedEventArgs args)
    {
        dl.RotationDegrees = dl.RotationDegrees + (new Vector3(0, 30, 0) * (float)args.DeltaTime);
    }
}