using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Example.Views
{
    public partial class MainView : UserControl
    {

        bool _isPressed = false;

        Avalonia.Point point = new(-1, -1);

        public MainView()
        {
            InitializeComponent();
            aura3Dview.Focusable = true;
            this.aura3Dview.PointerPressed += (s, e) =>
            {
                _isPressed = true;
                point = new(-1, -1);

            };

            this.aura3Dview.PointerReleased += (s, e) =>
            {
                _isPressed = false;
                point = new(-1, -1);
            };

            this.aura3Dview.PointerMoved += (s, e) =>
            {
                if (_isPressed == false)
                    return;
                if (e.Pointer.IsPrimary == false)
                    return;

                var newPosition = e.GetCurrentPoint(this).Position;
                if (point.X != -1 && point.Y != -1)
                {
                    var delta = newPosition - point;

                    camera!.RotationDegrees = new Vector3(
                        (float)(camera.RotationDegrees.X + (float)delta.Y * (float)deltaTime * 20),
                        (float)(camera.RotationDegrees.Y + (float)delta.X * (float)deltaTime * 20f), 0);

                }
                point = newPosition;

            };

            this.aura3Dview.KeyDown += (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.W)
                {
                    camera!.Position += camera.Forward * (float)deltaTime;
                }
                else if (e.Key == Avalonia.Input.Key.S)
                {
                    camera!.Position -= camera.Forward * (float)deltaTime;
                }
                else if (e.Key == Avalonia.Input.Key.A)
                {
                    camera!.Position -= camera.Right * (float)deltaTime;
                }
                else if (e.Key == Avalonia.Input.Key.D)
                {
                    camera!.Position += camera.Right * (float)deltaTime;
                }
            };
        }

        Camera? camera;

        public void OnSetupPipeline(object sender, RoutedEventArgs args)
        {
            var view = (Aura3DView)sender;
            view.CreateRenderPipeline = scene => 
            {
                var pipeline = new CelShadingPipeline(scene);
                // ≈‰÷√≤Œ ˝
                return pipeline;
            };
        }

        public void OnSceneInitialized(object sender, RoutedEventArgs args)
        {
            var view = (Aura3DView)sender;

            camera = new Camera();

            camera.ClearColor = Color.Gray;

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

            camera.ClearType = ClearType.Skybox;

            camera.SkyboxTexture = cubeTexture;


            view.AddNode(camera);

            PointLight pl = new PointLight();

            pl.AttenuationRadius = 2f;

            pl.LightColor = Color.Green;

            //view.AddNode(pl);

            PointLight pl2 = new PointLight();

            pl2.AttenuationRadius = 2f;

            pl2.LightColor = Color.Red;

            pl2.CastShadow = true;

            //view.AddNode(pl2);

            DirectionalLight dl = new DirectionalLight();

            dl.RotationDegrees = new Vector3(-45, 45, 0);

            dl.CastShadow = true;

            view.AddNode(dl);


            // using (var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/Soldier.glb")))
            using (var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/Soldier.glb")))
            {

                var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(s);

                model.AnimationSampler = new AnimationSampler(animations.First());

                view.AddNode(model);

                model.Position = camera.Position + camera.Forward * 10;

                model.Position += model.Up * 0.5f;

                // model.Scale = Vector3.One * 2f;
                // model.Scale = Vector3.One * 0.03f;
                // model.RotationDegrees = new Vector3(90, 0, 0);

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

        double deltaTime = 0;
        public void SceneUpdated(object sender, UpdateRoutedEventArgs args)
        {
            var view = (Aura3DView)sender;
            deltaTime = args.DeltaTime;
        }

    }

}