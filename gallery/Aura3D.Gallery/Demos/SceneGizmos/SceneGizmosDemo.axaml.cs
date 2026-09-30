using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Aura3D.Gallery.Kit;
using Aura3D.Gallery.Localization;
using System.Numerics;
using System.Threading.Tasks;

namespace Aura3D.Gallery.Demos;

/// <summary>
/// 场景辅助层：<see cref="Scene.Grid"/>、<see cref="Scene.AxisGizmo"/> 与
/// <c>PipelineSettings.Debug</c> 的八个开关全部出自同一条 DebugDrawPass。
/// 总开关 <c>Debug.Enable</c> 关掉后细分项一律无效，因此外壳默认把它打开，本页只翻细分项。
/// 参数行的排版全在 <c>SceneGizmosDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class SceneGizmosDemo : Demo
{
    private Node? lightOrbit;
    private SpotLight? spot;

    private double gridHalfWidth = 12;
    private double gridDivisions = 12;
    private double axisLength = 2.5;
    private double arrowheadSize = 0.35;
    private double orbitSpeed = 24;
    private double spotCone = 24;
    private double orbitDegrees;

    /// <summary>
    /// 建页：装配 XAML，并把调试开关对齐到面板上画出来的那一组值。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public SceneGizmosDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        var debug = Context.Settings.Debug;

        // XAML 里点光、聚光与总开关画的是勾选态，这里把设置同步成一样的，免得面板与真值不符。
        debug.Enable = true;
        debug.ShowPointLight = true;
        debug.ShowSpotLight = true;
    }

    /// <inheritdoc />
    public override Task LoadAssetsAsync(AssetBatch assets) => Task.CompletedTask;

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.MainCamera.Position = new Vector3(0, 4.2f, 12f);
        scene.MainCamera.LookAt(new Vector3(0, 1.6f, 0));

        scene.ShowGrid = true;
        scene.ShowAxisGizmo = true;

        var directional = new DirectionalLight { LightColor = System.Drawing.Color.White };

        directional.RotationDegrees = new Vector3(-52f, -34f, 0);

        scene.AddNode(directional);

        var point = new PointLight
        {
            LightColor = System.Drawing.Color.Orange,
            LuminousIntensity = 6000,
            AttenuationRadius = 9f,
        };

        point.Position = new Vector3(-3f, 3.4f, 2f);

        var spot = new SpotLight
        {
            LightColor = System.Drawing.Color.PowderBlue,
            LuminousIntensity = 6000,
            InnerConeAngleDegree = (float)(spotCone * 0.6),
            OuterConeAngleDegree = (float)spotCone,
            AttenuationRadius = 12f,
        };

        this.spot = spot;

        spot.Position = new Vector3(3.4f, 6f, 3f);
        spot.RotationDegrees = new Vector3(-108f, 12f, 0);

        lightOrbit = new Node { Name = "LightOrbit" };

        scene.AddNode(lightOrbit);

        lightOrbit.AddChild(point, AttachToParentRule.KeepLocal);
        lightOrbit.AddChild(spot, AttachToParentRule.KeepLocal);

        ApplyGrid();
        ApplyAxis();

        AddMesh(scene, "Box", new BoxGeometry(1.6f, 1.6f, 1.6f), new Vector3(-2.6f, 1.1f, -0.6f));
        AddMesh(scene, "Sphere", new SphereGeometry(0.95f, 36, 20), new Vector3(0f, 1.1f, -0.6f));
        AddMesh(scene, "Cylinder", new CylinderGeometry(0.6f, 0.8f, 2.2f, 32), new Vector3(2.6f, 1.1f, -0.6f));

        var hidden = AddMesh(scene, "Hidden", new BoxGeometry(1f, 1f, 1f), new Vector3(0f, 3.6f, -3f));

        hidden.Enable = false;
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        if (lightOrbit != null && orbitSpeed > 0)
        {
            orbitDegrees += orbitSpeed * deltaTime;

            lightOrbit.RotationDegrees = new Vector3(0, (float)orbitDegrees, 0);

            Context.RequestFrame();
        }

        var debug = Context.Settings.Debug;
        var grid = Context.Scene?.Grid;
        var axis = Context.Scene?.AxisGizmo;

        DebugReadout.Text = Strings.Keys.SceneGizmos_DebugReadout.Format(
            debug.Enable,
            debug.ShowBoundingBox,
            debug.ShowDirectionalLight,
            debug.ShowPointLight,
            debug.ShowSpotLight,
            debug.ShowCamera,
            grid?.Enable == true,
            grid?.Size,
            grid?.Divisions,
            axis?.AxisLength,
            axis?.ArrowheadSize);
    }

    private void OnShowGridToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        SetGridEnabled(e.ValueAs<bool>());

        Context.InvalidateRender();
    }

    private void OnShowAxisToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        SetAxisEnabled(e.ValueAs<bool>());

        Context.InvalidateRender();
    }

    private void OnGridSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        gridHalfWidth = e.ValueAs<double>();

        ApplyGrid();
    }

    private void OnGridDivisionsChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        gridDivisions = e.ValueAs<double>();

        ApplyGrid();
    }

    private void OnAxisLengthChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        axisLength = e.ValueAs<double>();

        ApplyAxis();
    }

    private void OnArrowheadSizeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        arrowheadSize = e.ValueAs<double>();

        ApplyAxis();
    }

    private void OnShowBoundingBoxToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowBoundingBox = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnShowDirectionalLightToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowDirectionalLight = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnShowPointLightToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowPointLight = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnShowSpotLightToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowSpotLight = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnShowCameraToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowCamera = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnShowParticleBoundsToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowParticleBounds = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnShowBoneToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowBone = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnDebugEnableToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.Enable = e.ValueAs<bool>();

        Context.InvalidateRender();
    }

    private void OnOrbitSpeedChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        orbitSpeed = e.ValueAs<double>();
    }

    private void OnSpotConeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        spotCone = e.ValueAs<double>();

        if (spot != null)
        {
            spot.OuterConeAngleDegree = (float)spotCone;
            spot.InnerConeAngleDegree = (float)(spotCone * 0.6);
        }

        Context.InvalidateRender();
    }

    private void SetGridEnabled(bool on)
    {
        if (Context.Scene != null)
            Context.Scene.ShowGrid = on;

        Context.InvalidateRender();
    }

    private void SetAxisEnabled(bool on)
    {
        if (Context.Scene != null)
            Context.Scene.ShowAxisGizmo = on;

        Context.InvalidateRender();
    }

    private void ApplyGrid()
    {
        var grid = Context.Scene?.Grid;

        if (grid == null)
            return;

        grid.Size = (float)gridHalfWidth;
        grid.Divisions = (int)gridDivisions;
    }

    private void ApplyAxis()
    {
        var axis = Context.Scene?.AxisGizmo;

        if (axis == null)
            return;

        axis.AxisLength = (float)axisLength;
        axis.ArrowheadSize = (float)arrowheadSize;
    }

    private static Mesh AddMesh(Scene scene, string name, Geometry geometry, Vector3 position)
    {
        var material = new Material();

        material.SetTexture("BaseColor", Procedural.Checker(128, 3));

        var mesh = new Mesh { Name = name, Geometry = geometry, Material = material };

        mesh.Position = position;

        scene.AddNode(mesh);

        return mesh;
    }
}
