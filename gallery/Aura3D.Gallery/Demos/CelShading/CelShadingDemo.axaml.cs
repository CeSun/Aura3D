using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Gallery.Kit;
using Aura3D.Gallery.Localization;
using Aura3D.Pipeline.CelShading;
using Irihi.Lingua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ModelNode = Aura3D.Core.Nodes.Model;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// 卡通管线的全部可调面：<see cref="CelShadingPipeline"/> 只吃六个命名通道与一组下划线开头的 float/Vector4。
/// pass 会把每个 uniform 先预载成参考材质（历史示例角色 Nilou 的卡渲参数）再用材质覆盖，
/// 所以缺参数、缺贴图的卡通材质也能正常渲染——「参考」球就是什么都不设、只挂一张底色图的样子。
/// 参数行的排版与初值全在 <c>CelShadingDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class CelShadingDemo : Demo
{
    /// <summary>通道表：Name 是材质认的通道名（身份，不参与本地化），Label 只作展示，与检视面板的开关标签同一批键。</summary>
    private static readonly (string Name, LinguaKey Label)[] Channels =
    [
        ("BaseColor", Strings.Keys.CelShading_BaseColor),
        ("Normal", Strings.Keys.CelShading_Normal),
        ("ILM", Strings.Keys.CelShading_Ilm),
        ("ShadowRamp", Strings.Keys.CelShading_ShadowRamp),
        ("SDF", Strings.Keys.CelShading_Sdf),
        ("SpecularRamp", Strings.Keys.CelShading_SpecularRamp),
    ];

    private readonly Dictionary<string, bool> plugged = new()
    {
        ["BaseColor"] = true,
        ["Normal"] = false,
        ["ILM"] = true,
        ["ShadowRamp"] = true,
        ["SDF"] = true,
        ["SpecularRamp"] = false,
    };

    private readonly Dictionary<string, float> numbers = new()
    {
        ["_GreyFac"] = 0.8f,
        ["_DarkFac"] = 0.15f,
        ["_BrightFac"] = 0.72f,
        ["_BrightAreaShadowFac"] = 0.85f,
        ["_FaceShadowOffset"] = 0.1f,
        ["_FaceShadowTransitionSoftness"] = 0.15f,
        ["_RampIndex0"] = 0f,
        ["_RampIndex1"] = 1f,
        ["_RampIndex2"] = 2f,
        ["_RampIndex3"] = 3f,
        ["_RampIndex4"] = 4f,
    };

    private Vector4 tint = new(1.15f, 1.1f, 1f, 1);
    private Vector4 dark = new(0.55f, 0.42f, 0.6f, 1);
    private Vector4 coolDark = new(0.45f, 0.55f, 0.85f, 1);

    private readonly Material body = new();
    private readonly Material face = new();

    private Mesh? faceMesh;
    private DirectionalLight? sun;
    private OutlinePass? outlinePass;
    private ModelNode? character;

    private float outlineAmbient = 0.1f;
    private bool faceRender = true;

    // 定向光绕 Y 轴自转：明暗交界线扫过角色与球体，卡渲的分档与色调映射一眼可见
    private float sunYaw = -25f;
    private float sunYawSpeed = 20f;

    /// <summary>
    /// 建页：装配 XAML，并把跟随引擎设置的那条滑杆初值对齐到真值。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public CelShadingDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        // Settings.AmbientIntensity 读的是引擎默认，XAML 里写不出这个常量，所以在这里补上。
        SettingsAmbientRow.Value = Context.Settings.AmbientIntensity;
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        character = await assets.ModelAsync("CelCharacter");

        character.Name = "CelCharacter";
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 2.75f, 5.25f);
        scene.MainCamera.LookAt(new Vector3(0, 2.4f, 0));
        // 30×30 地面 + 拉远余量，抬过默认 far 100。
        scene.MainCamera.FarPlane = 120f;

        sun = new DirectionalLight { LightColor = System.Drawing.Color.White };

        sun.RotationDegrees = new Vector3(-30f, -25f, 0);

        scene.AddNode(sun);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(30f, 30f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 14));

        scene.AddNode(ground);

        // 卡通角色 glb 放中央：它的材质没设卡通参数也照常渲染——
        // pass 有参考材质兜底，正好当「真实模型」对照
        if (character != null)
        {
            character.Scale = new Vector3(3f);

            scene.AddNode(character);
        }

        ApplyAll();

        Report();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (sun != null)
        {
            sunYaw += sunYawSpeed * (float)deltaTime;

            sun.RotationDegrees = new Vector3(-30f, sunYaw, 0);
        }

        Report();

        Context.RequestFrame();
    }

    /// <inheritdoc />
    public override void Unload()
    {
        // 描边那个 pass 属于本页创建过的管线，离开时不留悬挂引用。
        outlinePass = null;
    }

    private void OnChannelToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        plugged[(string)((ToggleRow)sender!).Tag!] = e.ValueAs<bool>();

        ApplyAll();
    }

    private void OnNumberChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        numbers[(string)((SliderRow)sender!).Tag!] = (float)e.ValueAs<double>();

        ApplyAll();
    }

    private void OnLightTintChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        var k = (float)e.ValueAs<double>();

        tint = new Vector4(k, k * 0.96f, k * 0.9f, 1);

        ApplyAll();
    }

    private void OnDarkTintChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        var k = (float)e.ValueAs<double>();

        dark = new Vector4(k * 0.72f, k * 0.55f, k * 0.78f, 1);

        ApplyAll();
    }

    private void OnCoolDarkChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        var k = (float)e.ValueAs<double>();

        coolDark = new Vector4(k * 0.5f, k * 0.6f, k, 1);

        ApplyAll();
    }

    private void OnFaceRenderToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        faceRender = e.ValueAs<bool>();

        ApplyAll();
    }

    private void OnSunSpeedChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        sunYawSpeed = (float)e.ValueAs<double>();
    }

    private void OnOutlineAmbientChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        outlineAmbient = (float)e.ValueAs<double>();

        ApplyOutline();

        Context.InvalidateRender();
    }

    private void OnSettingsAmbientChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.AmbientIntensity = (float)e.ValueAs<double>();

        Context.InvalidateRender();
    }

    private void ApplyAll()
    {
        body.SetTexture("BaseColor", plugged["BaseColor"] ? Procedural.Checker(64, 2) : null);
        body.SetTexture("Normal", plugged["Normal"] ? Procedural.BumpNormal(256, 6, 1.8f) : null);
        body.SetTexture("ILM", plugged["ILM"] ? BuildIlm() : null);
        body.SetTexture("ShadowRamp", plugged["ShadowRamp"] ? BuildRamp() : null);
        body.SetTexture("SpecularRamp", plugged["SpecularRamp"] ? BuildRamp() : null);

        body.RemoveParameterValue("RenderType");

        foreach (var (key, value) in numbers)
            body.SetParameterValue(key, value);

        body.SetParameterValue("_LightAreaColorTint", tint);
        body.SetParameterValue("_DarkShadowColor", dark);
        body.SetParameterValue("_CoolDarkShadowColor", coolDark);

        // 脸：SDF + RenderType=1 + Tags 里带 face，三个条件同时满足才进 FACE_RENDER 分支。
        face.SetTexture("SDF", plugged["SDF"] ? BuildSdf() : null);
        face.SetTexture("BaseColor", Procedural.SoftDot(128, 1.1f));
        face.SetTexture("ILM", plugged["ILM"] ? BuildIlm() : null);
        face.SetTexture("ShadowRamp", plugged["ShadowRamp"] ? BuildRamp() : null);

        foreach (var (key, value) in numbers)
            face.SetParameterValue(key, value);

        face.SetParameterValue("_LightAreaColorTint", tint);
        face.SetParameterValue("_DarkShadowColor", dark);
        face.SetParameterValue("_CoolDarkShadowColor", coolDark);

        if (faceRender)
        {
            face.SetParameterValue("RenderType", 1);

            if (faceMesh != null)
                faceMesh.Tags.Add("face");
        }
        else if (faceMesh != null)
        {
            face.RemoveParameterValue("RenderType");

            faceMesh.Tags.Remove("face");
        }

        ApplyOutline();

        Report();

        Context.InvalidateRender();
    }

    private void ApplyOutline()
    {
        var pass = Context.Scene?.RenderPipeline.EveryCameraRenderPasses.OfType<OutlinePass>().FirstOrDefault();

        if (pass == null)
            return;

        outlinePass = pass;
        pass.AmbientIntensity = outlineAmbient;
    }

    /// <summary>
    /// ILM：R=是否受光（0 的部分保持底色），G=AO（决定阴影强度），B=未用，A=材质分区（选 ramp 行）。
    /// </summary>
    private static Texture BuildIlm()
    {
        const int Size = 64;

        var data = new byte[Size * Size * 4];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size;
                float v = y / (float)Size;

                // 中间一条不受光的带（模拟眼睛/装饰），AO 沿竖向渐变，A 按 u 分五档。
                byte mask = MathF.Abs(v - 0.5f) < 0.12f ? (byte)0 : (byte)255;
                byte ao = (byte)(255 - (int)(MathF.Abs(v - 0.5f) * 2f * 180f));
                byte region = (byte)(MathF.Min(4, u * 5f) / 4f * 255f);

                var i = (y * Size + x) * 4;

                data[i] = mask;
                data[i + 1] = ao;
                data[i + 2] = 255;
                data[i + 3] = region;
            }
        }

        return Rgba(data, Size);
    }

    /// <summary>
    /// 阴影渐变图：五行，每行从暗到亮，行序对应 ILM 的 A 通道选到的 ramp 行。
    /// </summary>
    private static Texture BuildRamp()
    {
        const int Width = 128;
        const int Rows = 5;

        var data = new byte[Width * Rows * 4];

        for (int row = 0; row < Rows; row++)
        {
            // 每行一个色相，方便看出五个分区各自挪到了哪一行。
            float hue = row / (float)Rows;

            for (int x = 0; x < Width; x++)
            {
                float t = x / (float)(Width - 1);

                var rgb = FromHue(hue);
                var lifted = MathF.Min(1f, 0.25f + t);

                var i = (row * Width + x) * 4;

                data[i] = (byte)(rgb.X * lifted * 255f);
                data[i + 1] = (byte)(rgb.Y * lifted * 255f);
                data[i + 2] = (byte)(rgb.Z * lifted * 255f);
                data[i + 3] = 255;
            }
        }

        return Rgba(data, Width, Rows);
    }

    /// <summary>
    /// 一张最简单的圆形距离场，用来让脸走 SDF 分支：中心受光、边缘阴影。
    /// </summary>
    private static Texture BuildSdf()
    {
        const int Size = 64;

        var data = new byte[Size * Size * 4];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)(Size - 1) * 2f - 1f;
                float v = y / (float)(Size - 1) * 2f - 1f;

                var d = MathF.Sqrt(u * u + v * v);

                // 距离场值：越靠外越小（越暗）。
                var sdf = MathF.Min(1f, MathF.Max(0f, 1f - d * 0.9f));

                var i = (y * Size + x) * 4;

                data[i] = (byte)(sdf * 255f);
                data[i + 1] = (byte)(sdf * 255f);
                data[i + 2] = (byte)(sdf * 255f);
                data[i + 3] = 255;
            }
        }

        return Rgba(data, Size);
    }

    private static Vector3 FromHue(float hue)
    {
        var h = hue * 6f;

        float Channel(float offset)
        {
            var x = MathF.Abs(((h + offset) % 6f)) - 3f;

            return MathF.Min(1f, MathF.Max(0f, 1f - MathF.Abs(x) * 0.5f));
        }

        return new Vector3(Channel(0f), Channel(2f), Channel(4f));
    }

    private static Texture Rgba(byte[] data, int width, int? height = null)
    {
        var texture = new Texture();

        texture.SetLdrData(data, (uint)width, (uint)(height ?? width));
        texture.SetColorFormat(ColorFormat.RGBA);

        // 遮罩与 ramp 都是按值取的，走 gamma 解码会把分档阈值挪掉。
        texture.SetIsGammaSpace(false);

        texture.MinFilter = TextureFilterMode.Linear;
        texture.MagFilter = TextureFilterMode.Linear;
        texture.WrapS = TextureWrapMode.ClampToEdge;
        texture.WrapT = TextureWrapMode.ClampToEdge;

        return texture;
    }

    private void Report()
    {
        var bound = Channels.Where(c => plugged[c.Name]).Select(c => c.Name);

        Readout.Text = Strings.Keys.CelShading_Readout.Format(
            string.Join("/", bound),
            numbers.Count + 4,
            tint.X,
            dark.X,
            coolDark.X,
            faceRender ? "FACE_RENDER" : Strings.Keys.CelShading_FaceBodyBranch.T(),
            faceMesh?.Tags.Count,
            outlinePass == null ? Strings.Keys.CelShading_OutlineNotFound.T() : Strings.Keys.CelShading_OutlineBound.T(),
            outlineAmbient);
    }
}
