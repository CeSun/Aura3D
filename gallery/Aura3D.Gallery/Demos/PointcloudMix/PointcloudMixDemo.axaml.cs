using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Gallery.Kit;
using Aura3D.Gallery.Localization;
using Irihi.Lingua;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ModelNode = Aura3D.Core.Nodes.Model;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// Issue #21 的对照页：点云和 glTF 模型跑在同一个默认管线里，不写任何着色器覆盖。
/// 点尺寸、无光照、顶点色都是 <see cref="Material"/> 上的普通参数（uPointSize / uUnlit /
/// uUseVertexColor），由 RenderPass 按图元类型与设备上限落到着色器；页面不锁管线，
/// 切到 PBR 前向/延迟或卡通风，同一份场景照常出图。xyz/rgb 的 .ply 经 AssimpLoader
/// 加载后会得到同一套材质预设。参数行的排版全在 <c>PointcloudMixDemo.axaml</c> 里。
/// </summary>
public sealed partial class PointcloudMixDemo : Demo
{
    private const int PointCount = 6000;

    private Material pointMaterial = null!;
    private ModelNode? table;

    /// <summary>
    /// 建页：装配 XAML。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public PointcloudMixDemo(DemoContext context) : base(context)
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        table = await assets.ModelAsync("CoffeeTable");

        table.Name = "CoffeeTable";
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.ShowGrid = true;
        scene.MainCamera.Position = new Vector3(3.4f, 2.6f, 5.2f);
        scene.MainCamera.LookAt(new Vector3(0, 1.1f, 0));

        var light = new DirectionalLight
        {
            Name = "Key",
            LightColor = System.Drawing.Color.White,
        };

        light.RotationDegrees = new Vector3(-42f, -28f, 0);

        scene.AddNode(light);

        if (table != null)
            scene.AddNode(table);

        // 点云材质：三个参数就是本页的全部秘密，对比 PrimitivesDemo 里 NoLight 管线
        // 的 Shaders.Points 覆盖——这里是引擎默认路径。
        pointMaterial = new Material();

        pointMaterial.SetTexture("BaseColor", Texture.CreateFromColor(System.Drawing.Color.White));
        pointMaterial.SetParameterValue(Material.UseVertexColorParameterName, 1f);
        pointMaterial.SetParameterValue(Material.UnlitParameterName, 1f);
        pointMaterial.SetParameterValue(Material.PointSizeParameterName, 4f);

        var points = new Mesh
        {
            Name = "Pointcloud",
            Geometry = BuildPointSphere(PointCount),
            Material = pointMaterial,
        };

        points.Position = new Vector3(0f, 2.2f, 0f);

        scene.AddNode(points);
    }

    private void OnPointSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        pointMaterial.SetParameterValue(Material.PointSizeParameterName, (float)e.ValueAs<double>());

        Context.InvalidateRender();
    }

    private void OnUnlitToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        // 关掉后点云会按 fallback 法线（0,1,0）参与光照——正好演示为什么点云默认 unlit。
        pointMaterial.SetParameterValue(Material.UnlitParameterName, e.ValueAs<bool>() ? 1f : 0f);

        Context.InvalidateRender();
    }

    private void OnVertexColorToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        pointMaterial.SetParameterValue(Material.UseVertexColorParameterName, e.ValueAs<bool>() ? 1f : 0f);

        Context.InvalidateRender();
    }

    /// <summary>
    /// 斐波那契球壳：均匀分布的 xyz + 沿序号铺开的 HSV 彩虹 rgb，就是 .ply 点云的形状。
    /// </summary>
    private static Geometry BuildPointSphere(int count)
    {
        var positions = new List<float>(count * 3);
        var colors = new List<float>(count * 4);

        // 黄金角 ≈ 2.39996323（π(3-√5)），保证球壳上的点分布均匀。
        const float goldenAngle = 2.39996323f;

        for (var i = 0; i < count; i++)
        {
            var y = 1f - 2f * i / (count - 1);
            var radius = MathF.Sqrt(Math.Max(0f, 1f - y * y));
            var theta = goldenAngle * i;

            positions.Add(MathF.Cos(theta) * radius);
            positions.Add(y);
            positions.Add(MathF.Sin(theta) * radius);

            var (r, g, b) = HsvToRgb(i % 360 / 360f, 0.85f, 1f);

            colors.Add(r);
            colors.Add(g);
            colors.Add(b);
            colors.Add(1f);
        }

        var geometry = new Geometry
        {
            PrimitiveType = PrimitiveType.Points,
        };

        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
        geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, colors);

        return geometry;
    }

    private static (float R, float G, float B) HsvToRgb(float h, float s, float v)
    {
        var sector = (int)(h * 6f);
        var fraction = h * 6f - sector;
        var p = v * (1f - s);
        var q = v * (1f - fraction * s);
        var t = v * (1f - (1f - fraction) * s);

        return sector switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
    }
}
