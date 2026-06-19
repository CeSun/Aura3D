using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class PrimitiveTypePage : UserControl
{
    private CameraController _cameraController;
    bool _autoRotate = true;
    float _rotationAngle = 0;

    Node? container;
    List<double> deltaTimes = new();

    // 纯色顶点着色器
    private const string SolidColorVertexShader = """
        #version 300 es
        precision mediump float;

        //{{defines}}

        layout(location = 0) in vec3 position;

        uniform mat4 modelMatrix;
        uniform mat4 viewMatrix;
        uniform mat4 projectionMatrix;

        void main()
        {
            vec4 worldPosition = modelMatrix * vec4(position, 1.0);
            gl_Position = projectionMatrix * viewMatrix * worldPosition;
            gl_PointSize = 10.0;
        }
        """;

    // 纯色片段着色器
    private const string SolidColorFragmentShader = """
        #version 300 es
        precision mediump float;
        out vec4 outColor;

        uniform vec4 uColor;

        void main()
        {
            outColor = uColor;
        }
        """;

    public PrimitiveTypePage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3Dview);

        // 用户手动旋转时停止自动旋转
        aura3Dview.PointerPressed += (s, e) => { _autoRotate = false; };
    }

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var view = sender as Aura3DView;
        if (view == null) return;

        view.AutoRequestNextFrameRendering = false;

        view.MainCamera.Position = new Vector3(0, 3, 20);
        view.MainCamera.RotationDegrees = new Vector3(-10, 0, 0);

        var dl = new DirectionalLight();
        dl.RotationDegrees = new Vector3(-30, -15, 0);
        dl.LightColor = Color.White;
        view.AddNode(dl);

        container = new Node();
        view.AddNode(container);

        BuildAllPrimitives();

        view.RequestNextFrameRendering();
    }

    private void BuildAllPrimitives()
    {
        if (container == null) return;

        float x = -7.5f;
        const float step = 2.5f;

        // ── TRIANGLES ── 蓝色三角形
        CreateAndAdd(
            Aura3D.Core.Resources.PrimitiveType.Triangles,
            new List<float> { -0.5f, -0.4f, 0, 0.5f, -0.4f, 0, 0f, 0.5f, 0 },
            new List<uint> { 0, 1, 2 },
            new Vector4(0.2f, 0.4f, 1f, 1f),
            new Vector3(x, 0, 0));
        x += step;

        // ── POINTS ── 红色散点
        CreateAndAdd(
            Aura3D.Core.Resources.PrimitiveType.Points,
            new List<float> { 0,0,0, 0.3f,0.25f,0, -0.25f,0.35f,0, 0.15f,-0.3f,0, -0.35f,-0.1f,0 },
            null,
            new Vector4(1f, 0.2f, 0.2f, 1f),
            new Vector3(x, 0, 0));
        x += step;

        // ── LINES ── 绿色独立线段
        CreateAndAdd(
            Aura3D.Core.Resources.PrimitiveType.Lines,
            new List<float> { -0.4f,0,0, -0.15f,0.3f,0, 0,0.1f,0, 0.2f,0.35f,0, 0.4f,0,0, 0.15f,-0.3f,0 },
            null,
            new Vector4(0.2f, 0.9f, 0.3f, 1f),
            new Vector3(x, 0, 0));
        x += step;

        // ── LINE_STRIP ── 黄色折线
        CreateAndAdd(
            Aura3D.Core.Resources.PrimitiveType.LineStrip,
            new List<float> { -0.5f,-0.3f,0, -0.2f,0.35f,0, 0.1f,-0.35f,0, 0.45f,0.3f,0 },
            null,
            new Vector4(1f, 0.9f, 0.1f, 1f),
            new Vector3(x, 0, 0));
        x += step;

        // ── LINE_LOOP ── 青色五边形
        {
            var positions = new List<float>();
            for (int i = 0; i < 5; i++)
            {
                float a = (float)(i * Math.PI * 2 / 5 - Math.PI / 2);
                positions.Add(0.45f * MathF.Cos(a));
                positions.Add(0.45f * MathF.Sin(a));
                positions.Add(0);
            }
            CreateAndAdd(Aura3D.Core.Resources.PrimitiveType.LineLoop, positions, null,
                new Vector4(0.1f, 0.9f, 0.9f, 1f), new Vector3(x, 0, 0));
        }
        x += step;

        // ── TRIANGLE_STRIP ── 品红色带状（逆时针绕序，从右侧往左侧走，首三角形 CCW）
        CreateAndAdd(
            Aura3D.Core.Resources.PrimitiveType.TriangleStrip,
            new List<float> { 0.3f,-0.25f,0, 0.3f,0.25f,0, -0.1f,-0.35f,0, -0.1f,0.35f,0, -0.5f,-0.3f,0, -0.5f,0.3f,0 },
            null,
            new Vector4(0.9f, 0.2f, 0.9f, 1f),
            new Vector3(x, 0, 0));
        x += step;

        // ── TRIANGLE_FAN ── 橙色六边形扇（逆时针绕序，与 Triangles 一致）
        {
            var positions = new List<float> { 0f, 0f, 0f };
            for (int i = 0; i <= 6; i++)
            {
                float a = (float)(i * Math.PI * 2 / 6); // CCW
                positions.Add(0.45f * MathF.Cos(a));
                positions.Add(0.45f * MathF.Sin(a));
                positions.Add(0);
            }
            CreateAndAdd(Aura3D.Core.Resources.PrimitiveType.TriangleFan, positions, null,
                new Vector4(1f, 0.5f, 0.15f, 1f), new Vector3(x, 0, 0));
        }
    }

    private void CreateAndAdd(
        Aura3D.Core.Resources.PrimitiveType type,
        List<float> positions,
        List<uint>? indices,
        Vector4 color,
        Vector3 offset)
    {
        if (container == null) return;

        var geometry = new Geometry();
        geometry.PrimitiveType = type;
        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
        if (indices != null && indices.Count > 0)
            geometry.SetIndices(indices);

        var mesh = new Mesh { Geometry = geometry };

        var material = new Material { BlendMode = BlendMode.Opaque };
        material.SetShaderSource("LightPass", ShaderType.Vertex, SolidColorVertexShader);
        material.SetShaderSource("LightPass", ShaderType.Fragment, SolidColorFragmentShader);
        material.SetParameterValue("uColor", color);
        mesh.Material = material;

        mesh.LocalTransform = Matrix4x4.CreateTranslation(offset);
        container.AddChild(mesh, AttachToParentRule.KeepLocal);
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        var dt = args.DeltaTime;

        if (deltaTimes.Count >= 10)
            deltaTimes.RemoveAt(0);
        deltaTimes.Add(dt);
        var avgDt = deltaTimes.Average();
        infoText.Text = $"FPS: {(int)(1 / avgDt)}";

        if (_autoRotate && container != null)
        {
            _rotationAngle += (float)dt * 20;
            container.RotationDegrees = new Vector3(0, _rotationAngle, 0);
        }

        (sender as Aura3DView)?.RequestNextFrameRendering();
    }

    private void RotateButton_Click(object? sender, RoutedEventArgs e)
    {
        _autoRotate = !_autoRotate;
    }
}
