using Aura3D.Core.Nodes;
using Aura3D.Core.Geometries;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Aura3D.Examples.Localization;

namespace Aura3D.Examples.Demos;

/// <summary>
/// 相机能做的事：透视/正交切换、视锥参数、<see cref="Camera.LookAt"/>、
/// <see cref="Camera.FitToBoundingBox"/> 与 <see cref="Camera.WorldToScreen"/>。
/// 屏幕投影的读数配画面左上角那个十字标记，能直接验证 <c>WorldToScreen</c> 的坐标原点与 Y 方向。
/// 参数行与十字标记的排版全在 <c>CameraDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class CameraDemo : Demo
{
    private readonly List<Mesh> boxes = [];

    private Mesh? focusTarget;
    private Node? markerRoot;
    private ProjectionType projection = ProjectionType.Perspective;
    private double fieldOfView = 60;
    private double orthographicSize = 4;
    private double nearPlane = 0.1;
    private double farPlane = 100;

    /// <summary>投影方式下拉的选项，XAML 里以 <c>Options="{Binding ProjectionOptions}"</c> 取用。</summary>
    public IList ProjectionOptions { get; } = new List<ProjectionType>
    {
        ProjectionType.Perspective,
        ProjectionType.Orthographic,
    };

    /// <summary>
    /// 建页：装配 XAML，并把下拉的选中项对齐到本页真正生效的投影方式。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public CameraDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        ProjectionRow.SelectedItem = projection;
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.ShowGrid = true;
        scene.ShowAxisGizmo = true;

        var light = new DirectionalLight { LightColor = System.Drawing.Color.White, Irradiance = 90000 };

        light.RotationDegrees = new Vector3(-50f, 30f, 0);

        scene.AddNode(light);

        var random = new Random(20260929);

        for (int i = 0; i < 9; i++)
        {
            var size = 0.6f + (float)random.NextDouble() * 1.4f;

            var box = new Mesh
            {
                Name = $"Box{i}",
                Geometry = new BoxGeometry(size, size, size),
                Material = new Material(),
            };

            box.Material.SetTexture("BaseColor", Procedural.Checker(128, 3));
            box.Position = new Vector3(
                (float)(random.NextDouble() - 0.5) * 9,
                size * 0.5f,
                (float)(random.NextDouble() - 0.5) * 9);
            box.RotationDegrees = new Vector3(0, (float)random.NextDouble() * 90, 0);

            scene.AddNode(box);
            boxes.Add(box);
        }

        focusTarget = boxes[4];
        markerRoot = boxes[0];

        ApplyProjection();

        scene.MainCamera.Position = new Vector3(7f, 6f, 9f);
        scene.MainCamera.LookAt(new Vector3(0, 1f, 0));
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        var scene = Context.Scene;
        var camera = scene?.MainCamera;

        if (camera == null || markerRoot == null)
            return;

        // 慢速自转保证即使没有输入也持续有帧，读数与十字标记才会跟着相机走。
        markerRoot.RotationDegrees = new Vector3(
            0,
            markerRoot.RotationDegrees.Y + 18f * (float)deltaTime,
            0);

        var screen = camera.WorldToScreen(markerRoot.Position + new Vector3(0, 1.6f, 0));

        if (screen == null)
        {
            Overlay.IsVisible = false;

            ScreenReadout.Text = Strings.Keys.Camera_BehindCamera.T();

            return;
        }

        var scale = camera.ScreenScale;

        Overlay.IsVisible = true;

        // WorldToScreen 返回的是设备像素，覆盖层用逻辑像素，所以除回 ScreenScale；
        // Y 轴方向由引擎负责，这里直接按左上角摆放标记。
        Canvas.SetLeft(Crosshair, screen.Value.X / scale - 8);
        Canvas.SetTop(Crosshair, screen.Value.Y / scale - 8);

        ScreenReadout.Text = Strings.Keys.Camera_ScreenReadout.Format(
            screen.Value.X / scale, screen.Value.Y / scale, camera.Width, camera.Height);
    }

    private void OnProjectionChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        projection = (ProjectionType)e.Value!;

        ApplyProjection();
    }

    private void OnFieldOfViewChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        fieldOfView = e.ValueAs<double>();

        ApplyProjection();
    }

    private void OnOrthographicSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        orthographicSize = e.ValueAs<double>();

        ApplyProjection();
    }

    private void OnNearChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        nearPlane = e.ValueAs<double>();

        ApplyProjection();
    }

    private void OnFarChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        farPlane = e.ValueAs<double>();

        ApplyProjection();
    }

    private void OnFitClicked(object? sender, RoutedEventArgs e)
    {
        if (focusTarget?.BoundingBox is { } box)
        {
            Context.Scene?.MainCamera.FitToBoundingBox(box, 0.25f);

            ApplyProjection();
        }
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        Context.Scene?.MainCamera.Position = new Vector3(7, 6, 9);
        Context.Scene?.MainCamera.LookAt(new Vector3(0, 1, 0));
    }

    private void OnRenderBackgroundToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        if (Context.Scene != null)
            Context.Scene.MainCamera.IsRenderBackground = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void ApplyProjection()
    {
        var camera = Context.Scene?.MainCamera;

        if (camera == null)
            return;

        camera.ProjectionType = projection;
        camera.FieldOfView = (float)fieldOfView;
        camera.OrthographicSize = (float)orthographicSize;
        camera.SetClippingPlanes((float)nearPlane, (float)farPlane);

        Context.InvalidateRender();
    }
}
