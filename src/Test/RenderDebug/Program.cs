// See https://aka.ms/new-console-template for more information
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Scenes;
using Silk.NET.Windowing;
using System.Drawing;
using System.Numerics;

var window = Window.Create(WindowOptions.Default);
Scene scene = new Scene(scene => new CelShadingPipeline(scene));

window.Load += () =>
{

    scene.RenderPipeline.Initialize(str =>
    {
        window.GLContext.TryGetProcAddress(str, out var p);
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
    foreach(var filename in name)
    {
        var stream = new StreamReader($"../../../../../Example/Example/Assets/Textures/skybox/{filename}").BaseStream;
        list.Add(stream);
    }

    var cubeTexture = TextureLoader.LoadCubeTexture(list);

    foreach (var stream in list)
    {
        stream.Dispose();
    }

    camera.ClearType = ClearType.Skybox;

    camera.SkyboxTexture = cubeTexture;

    AddNode(camera);


    using (var sr = new StreamReader("../../../../../Example/Example/Assets/Models/Soldier.glb"))
    {
        var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(sr.BaseStream);

        model.AnimationSampler = new AnimationSampler(animations.First());

        model.Position = camera.Position + camera.Forward * 10;

        model.Position += model.Up * 0.5f;

        AddNode(model);



    }
    var node = new Node();
    using (var sr = new StreamReader("../../../../../Example/Example/Assets/Models/coffee_table_round_01_1k.glb"))
    {

        var model = ModelLoader.LoadGlbModel(sr.BaseStream);

        model.Position = camera.Position + camera.Forward * 10;

        model.Position += camera.Down * 2;

        model.Scale = Vector3.One * 5f;

        AddNode(model);

        node.Position = model.Position;

    }

    camera.Position = camera.Position + camera.Up * 4;

    camera.RotationDegrees = new Vector3(-20, 0, 0);

    camera.Position = camera.Position + camera.Forward * 3;




    node.RotationDegrees = new Vector3(0, 90, 0);

    DirectionalLight dl = new DirectionalLight();

    dl.CastShadow = true;

    dl.RotationDegrees = new Vector3(-45, 45, 0);

    AddNode(dl);
};


window.Render += (delta) =>
{

    foreach (var renderTarget in scene.ControlRenderTargets)
    {
        renderTarget.Width = (uint)(window.Size.X);
        renderTarget.Height = (uint)(window.Size.Y);
    }
    scene.RenderPipeline.DefaultFramebuffer = (uint)0;

    scene.RenderPipeline.Render();

    scene.Update(delta);




};

window.Run();


void AddNode<T>(T node) where T : Node
{
    scene.AddNode(node);
}
