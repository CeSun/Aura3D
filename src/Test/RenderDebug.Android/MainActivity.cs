using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Scenes;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;
using System.Drawing;
using System.Numerics;

namespace Example.Test.Android;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SilkActivity
{
    Scene scene = null;
    protected override void OnRun()
    {
        var view = Silk.NET.Windowing.Window.GetView(ViewOptions.Default with { API = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 0))});

        scene = new Scene(scene => new BlinnPhongPipeline(scene));

        view.Load += () =>
        {

            scene.RenderPipeline.Initialize(str =>
            {
                view.GLContext.TryGetProcAddress(str, out var p);
                return p;
            });

            var camera = new Camera();

            camera.ClearColor = Color.Gray;
            camera.NearPlane = 1;

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
                list.Add(Assets.Open($"Example/Assets/Textures/skybox/{filename}"));
            }

            var cubeTexture = TextureLoader.LoadCubeTexture(list);

            foreach (var stream in list)
            {
                stream.Dispose();
            }

            camera.ClearType = ClearType.Skybox;

            camera.SkyboxTexture = cubeTexture;

            AddNode(camera);

            PointLight pl = new PointLight();

            pl.AttenuationRadius = 1.5f;

            pl.LightColor = Color.Green;

            AddNode(pl);


            PointLight pl2 = new PointLight();

            pl2.AttenuationRadius = 1.5f;

            pl2.LightColor = Color.Red;
            AddNode(pl2);



            using (var stream = Assets.Open($"Example/Assets/Models/Soldier.glb"))
            {

                var model = ModelLoader.LoadGlbModel(stream);

                AddNode(model);

                model.Position = camera.Position + camera.Forward * 10;

                // model.Position += model.Up * 1.5f;

                model.Scale = Vector3.One * 5f;

                pl.Position = model.Position + pl.Up * 2 + pl.Left * 2f;

                pl2.Position = model.Position + pl2.Up * 2 + pl2.Right * 2f;

            }


            using (var stream = Assets.Open($"Example/Assets/Models/coffee_table_round_01_1k.glb"))
            {

                var model = ModelLoader.LoadGlbModel(stream);

                AddNode(model);

                model.Position = camera.Position + camera.Forward * 10;

                model.Position += camera.Down * 2;

                model.Scale = Vector3.One * 5f;

                camera.Position = model.Position + camera.Backward * 10 + camera.Up * 5;

                // camera.RotationDegrees = new Vector3(-90, 0, 0);

            }



        };

        view.Render += (delta) =>
        {

            scene.RenderPipeline.DefaultFramebuffer = (uint)0;

            scene.RenderPipeline.Render();

            scene.Update(delta);

            foreach (var renderTarget in scene.ControlRenderTargets)
            {
                renderTarget.Width = (uint)(view.Size.X);
                renderTarget.Height = (uint)(view.Size.Y);
            }



        };

        view.Run();
    }



    void AddNode<T>(T node) where T : Node
    {
        scene.AddNode(node);
    }
}