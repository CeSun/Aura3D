using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Aura3D.Examples.Localization;
using Irihi.Lingua;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Aura3D.Core.Nodes;

namespace Aura3D.Examples.Demos;

/// <summary>
/// 手写 <see cref="Geometry"/> 的顶点缓冲与七种 <see cref="PrimitiveType"/> 的连线规则：
/// 同一批顶点换一种解释方式就得到点云、线框、剖面骨架或实心面。
/// 顶点色走槽位 2，着色由 <see cref="Shaders.VertexColors"/> 与 <see cref="Shaders.Points"/> 覆盖
/// <c>NoLightPass</c> 完成，所以本页锁死在 NoLight 管线上——换到需要法线的管线会得到误导的结果。
/// 参数行的排版全在 <c>PrimitivesDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class PrimitivesDemo : Demo
{
    private static readonly (PrimitiveType Type, LinguaKey Label)[] Kinds =
    [
        (PrimitiveType.Points, Strings.Keys.Primitives_KindPoints),
        (PrimitiveType.Lines, Strings.Keys.Primitives_KindLines),
        (PrimitiveType.LineStrip, Strings.Keys.Primitives_KindLineStrip),
        (PrimitiveType.LineLoop, Strings.Keys.Primitives_KindLineLoop),
        (PrimitiveType.Triangles, Strings.Keys.Primitives_KindTriangles),
        (PrimitiveType.TriangleStrip, Strings.Keys.Primitives_KindTriangleStrip),
        (PrimitiveType.TriangleFan, Strings.Keys.Primitives_KindTriangleFan),
    ];

    private readonly Dictionary<PrimitiveType, Mesh> meshes = new();

    private int columns = 12;
    private int rows = 6;
    private float wave = 0.9f;
    private float pointSize = 12f;
    private Mesh? selected;

    /// <summary>「聚焦图元」下拉的选项，供 XAML 绑定（ComboRow.Options 是 IList）。</summary>
    public IList KindOptions { get; } = Kinds.Select(k => k.Label.T()).ToList();

    /// <summary>
    /// 建页：装配 XAML，并把下拉的选中项对齐到 <see cref="BuildScene"/> 里默认的 Triangles。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public PrimitivesDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        FocusRow.SelectedItem = KindOptions[4];
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.ShowGrid = true;
        scene.MainCamera.Position = new Vector3(0, 6f, 14f);
        scene.MainCamera.LookAt(new Vector3(0, 0.5f, 0));

        foreach (var (type, label) in Kinds)
        {
            var mesh = new Mesh
            {
                Name = label.T(),
                Geometry = BuildGeometry(type),
                Material = type == PrimitiveType.Points
                    ? Shaders.Points("NoLightPass", pointSize)
                    : Shaders.VertexColors("NoLightPass"),
            };

            scene.AddNode(mesh);

            meshes[type] = mesh;
        }

        selected = meshes[PrimitiveType.Triangles];

        Layout();
    }

    private void OnFocusChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        // 模板应用时初始选中会先进这里一次，那时场景还没建，直接放过。
        if (meshes.Count == 0)
            return;

        var label = (string)e.Value!;
        var index = KindOptions.IndexOf(label);

        if (index >= 0)
            selected = meshes[Kinds[index].Type];

        Layout();
        Report();
        Context.InvalidateRender();
    }

    private void OnColumnsChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        columns = (int)e.ValueAs<double>();

        Rebuild();
    }

    private void OnRowsChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        rows = (int)e.ValueAs<double>();

        Rebuild();
    }

    private void OnWaveChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        wave = (float)e.ValueAs<double>();

        Rebuild();
    }

    private void OnPointSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        pointSize = (float)e.ValueAs<double>();

        if (meshes.TryGetValue(PrimitiveType.Points, out var points))
            points.Material?.SetParameterValue("uPointSize", pointSize);

        Context.InvalidateRender();
    }

    private Geometry BuildGeometry(PrimitiveType type)
    {
        var positions = new List<float>();
        var colors = new List<float>();
        var indices = new List<uint>();

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                var u = columns == 1 ? 0f : column / (float)(columns - 1);
                var v = rows == 1 ? 0f : row / (float)(rows - 1);

                positions.Add((u - 0.5f) * 8f);
                positions.Add(0.6f + MathF.Sin(u * MathF.PI * 2f) * wave * MathF.Cos(v * MathF.PI));
                positions.Add((v - 0.5f) * 6f);

                var color = new Vector4(0.25f + 0.7f * u, 0.35f + 0.5f * v, 0.85f - 0.5f * u, 1f);

                colors.Add(color.X);
                colors.Add(color.Y);
                colors.Add(color.Z);
                colors.Add(color.W);
            }
        }

        AppendIndices(type, indices);

        var geometry = new Geometry { PrimitiveType = type };

        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
        geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, colors);
        geometry.SetIndices(indices);

        return geometry;
    }

    /// <summary>
    /// 各图元的索引写法差别：点与线只数顶点，三角形族才需要真正的三角形拓扑。
    /// </summary>
    private void AppendIndices(PrimitiveType type, List<uint> indices)
    {
        int stride = columns;

        switch (type)
        {
            case PrimitiveType.Triangles:
                for (int row = 0; row + 1 < rows; row++)
                {
                    for (int column = 0; column + 1 < columns; column++)
                    {
                        int upper = row * stride + column;
                        int lower = upper + stride;

                        indices.Add((uint)upper);
                        indices.Add((uint)lower);
                        indices.Add((uint)(upper + 1));

                        indices.Add((uint)(upper + 1));
                        indices.Add((uint)lower);
                        indices.Add((uint)(lower + 1));
                    }
                }

                break;

            case PrimitiveType.TriangleStrip:
            case PrimitiveType.TriangleFan:
                for (int row = 0; row + 1 < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        indices.Add((uint)(row * stride + column));
                        indices.Add((uint)((row + 1) * stride + column));
                    }
                }

                break;

            default:
                for (int i = 0; i < rows * columns; i++)
                    indices.Add((uint)i);

                break;
        }
    }

    private void Rebuild()
    {
        foreach (var (type, _) in Kinds)
        {
            meshes[type].Geometry = BuildGeometry(type);
        }

        Layout();
        Report();
        Context.InvalidateRender();
    }

    private void Layout()
    {
        for (int i = 0; i < Kinds.Length; i++)
        {
            var mesh = meshes[Kinds[i].Type];

            // 聚焦某一种图元时，其余挪到相机背后：留在原地会互相遮挡，看不出连线规则。
            mesh.Position = ReferenceEquals(mesh, selected)
                ? new Vector3(0, 0, 0)
                : new Vector3(0, -50f - i, 0);
        }
    }

    private void Report()
    {
        if (selected?.Geometry == null)
            return;

        var geometry = selected.Geometry;

        CurrentReadout.Text = Strings.Keys.Primitives_CurrentReadout.Format(
            geometry.VertexCount, geometry.IndicesCount, EstimatedPrimitives(geometry));
    }

    private static double EstimatedPrimitives(Geometry geometry) => geometry.PrimitiveType switch
    {
        PrimitiveType.Points => geometry.VertexCount,
        PrimitiveType.Lines => geometry.IndicesCount / 2.0,
        PrimitiveType.LineStrip => Math.Max(geometry.IndicesCount - 1, 0),
        PrimitiveType.LineLoop => geometry.IndicesCount,
        PrimitiveType.Triangles => geometry.IndicesCount / 3.0,
        PrimitiveType.TriangleStrip => Math.Max(geometry.IndicesCount - 2, 0),
        _ => Math.Max(geometry.IndicesCount - 2, 0),
    };
}
