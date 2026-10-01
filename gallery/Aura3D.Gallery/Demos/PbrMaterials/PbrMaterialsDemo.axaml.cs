using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Gallery.Kit;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// 三颗球各挂一套 Poly Haven（CC0）实拍 PBR 材质，三档一眼可分：
/// 锈蚀铁皮（锈是电介质、露铁处才有金属度，arm 的 B 通道斑驳）、混砌砖墙（法线主导的粗糙介质）、
/// 镀锌钢板（metal≈0.9、粗糙度≈0.13 的光滑裸金属，IBL 下有锐利的环境反射）。
/// 选材时用 arm 图的通道均值把关：Poly Haven 名字带 metal 的多是涂漆/锈蚀（B 通道≈0），
/// 进引擎就是非金属——之前四颗球看起来一个样就是这么来的。每套三张贴图：
/// diff → BaseColor，nor_gl（OpenGL 约定）→ Normal，arm（R=AO/G=粗糙/B=金属）→ MetallicRoughness——
/// 后者不用拆通道，glTF 的打包约定跟引擎着色器读法（G=roughness、B=metallic）一字不差。
/// 环境挂六面天空盒：金属反射没有 IBL 就是一坨死灰，环境立方图是这页能不能看的前提。
/// </summary>
public sealed partial class PbrMaterialsDemo : Demo
{
    // 从左到右的排布顺序，也是面板读出行的顺序
    private static readonly (string Name, string BaseKey, string NormalKey, string ArmKey)[] Materials =
    [
        ("Rusty Metal", "PbrRustyMetalBase", "PbrRustyMetalNormal", "PbrRustyMetalArm"),
        ("Brick Wall", "PbrBrickWallBase", "PbrBrickWallNormal", "PbrBrickWallArm"),
        ("Galvanized Metal", "PbrGalvanizedBase", "PbrGalvanizedNormal", "PbrGalvanizedArm"),
    ];

    private readonly List<Mesh> spheres = [];

    private CubeTexture environment = null!;

    /// <summary>
    /// 建页：装配 XAML。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public PbrMaterialsDemo(DemoContext context) : base(context)
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        environment = await assets.CubeTextureAsync();

        var textures = new Dictionary<string, Texture>();

        foreach (var (_, baseKey, normalKey, armKey) in Materials)
        {
            textures[baseKey] = await assets.TextureAsync(baseKey);
            textures[normalKey] = await assets.TextureAsync(normalKey);
            textures[armKey] = await assets.TextureAsync(armKey);
        }

        for (var i = 0; i < Materials.Length; i++)
        {
            var (name, baseKey, normalKey, armKey) = Materials[i];

            // TextureLoader 默认按线性上传；BaseColor 是 sRGB 编码的 JPEG，必须标成 gamma 空间，
            // 驱动才会用 SRGB8 内部格式硬件解码到线性——否则 sRGB 值被当线性参与光照，
            // 末尾 GammaCorrectionPass 再提亮一次，整页就会发白发灰。Normal/ARM 是数据图，保持线性。
            textures[baseKey].SetIsGammaSpace(true);

            var material = new Material();

            material.SetTexture("BaseColor", textures[baseKey]);
            material.SetTexture("Normal", textures[normalKey]);
            material.SetTexture("MetallicRoughness", textures[armKey]);

            var sphere = new Mesh
            {
                Name = name,
                Geometry = new SphereGeometry(1.2f, 64, 40),
                Material = material,
            };

            // 三颗球等距排一排；Update 里自转起来，让法线图和反射随角度变化
            sphere.Position = new Vector3((i - (Materials.Length - 1) / 2f) * 2.4f, 1.5f, 0);

            spheres.Add(sphere);
        }
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        // 默认 IBL 强度是按白天空调的，介质球会被烘成一片白蒙蒙；
        // 压到 0.45 让定向光的高光与阴影把三档材质拉开。
        Context.Settings.IblAmbientIntensity = 0.45f;

        scene.ShowGrid = true;
        // 取景中心略偏右：把三颗球整体挪离右侧参数面板的遮挡区。
        scene.MainCamera.Position = new Vector3(0.5f, 2.2f, 9.2f);
        scene.MainCamera.LookAt(new Vector3(0.5f, 1.4f, 0));
        // 30×30 地面 + 拉远余量，抬过默认 far 100。
        scene.MainCamera.FarPlane = 150f;

        // 环境立方图：IBL 只认 Scene.Background 的立方图分支。挂上就行——
        // 渲染先于本回调（Update → Render → SceneUpdated），首帧烘的其实是引擎那张默认纯白立方图，
        // 而 Scene.Background 的 setter 会自动把两张烘焙缓存作废，下一帧就用这个天空盒重烘。
        scene.Background = environment;

        var sun = new DirectionalLight
        {
            LightColor = System.Drawing.Color.White,
            CastShadow = true,
        };

        sun.RotationDegrees = new Vector3(-40f, -30f, 0);

        scene.AddNode(sun);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(30f, 30f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 16));

        scene.AddNode(ground);

        foreach (var sphere in spheres)
            scene.AddNode(sphere);

        OrderReadout.Text = string.Join(" → ", Array.ConvertAll(Materials, m => m.Name));
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        foreach (var sphere in spheres)
            sphere.RotationDegrees = new Vector3(0, sphere.RotationDegrees.Y + 12f * (float)deltaTime, 0);

        Context.RequestFrame();
    }
}
