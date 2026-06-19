using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Controls;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class RenderingPerformancePage : UserControl
{
    private CameraController _cameraController;
    private List<double> _deltaTimes = [];

    // Individual Meshes
    private List<Mesh> _fcMeshes = [];

    // GPU Instancing
    private InstancedMesh? _instMesh;

    // HISM
    private InstancedMeshGroup? _hismGroup;

    // Shared
    private BoxGeometry? _sharedBoxGeo;
    private Material? _sharedWhiteMat;
    private int _gridSize;
    private float _spacing;

    public RenderingPerformancePage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3Dview) { MoveSpeed = 15f };

        buildButton.Click += (s, e) => BuildGrid();
        cullingCheck.IsCheckedChanged += (s, e) => ApplyCulling();
        hismCullingCheck.IsCheckedChanged += (s, e) => ApplyCulling();

        techniqueCombo.SelectionChanged += (s, e) =>
        {
            if (aura3Dview.Scene == null) return;
            BuildGrid();
        };

        updateInstButton.Click += (s, e) => UpdateHISMInstance();
        randomMoveButton.Click += (s, e) => RandomMoveHISM();
    }

    // ─── Scene Init ─────────────────────────────────────────

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        aura3Dview.AutoRequestNextFrameRendering = false;

        _sharedBoxGeo = new BoxGeometry();
        _sharedWhiteMat = new Material
        {
            BlendMode = BlendMode.Opaque,
            BaseColor = Texture.CreateFromColor(Color.White)
        };

        var dl = new DirectionalLight
        {
            RotationDegrees = new Vector3(-35, -20, 0),
            LightColor = Color.White
        };
        aura3Dview.AddNode(dl);

        BuildGrid();

        float dist = _gridSize * _spacing * 1.2f;
        aura3Dview.MainCamera.Position = new Vector3(0, dist * 0.4f, dist);
        aura3Dview.MainCamera.RotationDegrees = new Vector3(-25, 0, 0);
    }

    // ─── Scene Updated ──────────────────────────────────────

    private int _fpsMin = int.MaxValue;
    private int _fpsMax;
    private long _fpsSum;
    private int _fpsFrameCount;

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        // Rolling-average FPS
        if (_deltaTimes.Count >= 30) _deltaTimes.RemoveAt(0);
        _deltaTimes.Add(args.DeltaTime);
        var fps = (int)(1 / _deltaTimes.Average());

        // Accumulate min/max/avg since last build
        if (_fpsFrameCount == 0)
        {
            _fpsMin = fps;
            _fpsMax = fps;
            _fpsSum = 0;
        }
        if (fps < _fpsMin) _fpsMin = fps;
        if (fps > _fpsMax) _fpsMax = fps;
        _fpsSum += fps;
        _fpsFrameCount++;

        if (DataContext is RenderingPerformanceViewModel vm)
        {
            vm.CurrentFps = fps;
            vm.MinFps = _fpsMin;
            vm.MaxFps = _fpsMax;
            vm.AvgFps = (int)(_fpsSum / _fpsFrameCount);

            // FPS bar: scale to 0–100%, capped at 200 FPS for visual range
            double barRatio = Math.Clamp(fps / 200.0, 0.0, 1.0);
            vm.FpsBarWidth = barRatio * 200;
            vm.FpsBarColor = fps switch
            {
                < 30 => "#FF6B6B",
                < 60 => "#FFD93D",
                _ => "#6BCB77"
            };

            int actualCubes = _gridSize * _gridSize * _gridSize;

            if (vm.IsIndividualMode)
            {
                bool culling = cullingCheck.IsChecked == true;
                vm.DetailText = $"{actualCubes} meshes  |  {actualCubes} draw calls  |  Culling: {(culling ? "ON" : "OFF")}";
            }
            else if (vm.IsInstancedMode)
            {
                vm.DetailText = $"{actualCubes} instances  |  1 draw call";
            }
            else if (vm.IsHISMMode && _hismGroup != null)
            {
                vm.DetailText = $"{_hismGroup.InstanceCount} instances  |  {_hismGroup.GroupCount} groups  |  InPlace:{_hismGroup.InPlaceUpdateCount} Rebuild:{_hismGroup.RebuildCount}";
            }
        }

        aura3Dview.RequestNextFrameRendering();
    }

    // ─── Build ──────────────────────────────────────────────

    private void BuildGrid()
    {
        if (DataContext is RenderingPerformanceViewModel vm)
        {
            if (!int.TryParse(vm.CubeCountText?.Trim(), out var count) || count <= 0) count = 1000;
            _gridSize = (int)Math.Ceiling(Math.Pow(count, 1.0 / 3.0));
            if (!float.TryParse(vm.SpacingText?.Trim(), out _spacing) || _spacing <= 0) _spacing = 2.5f;
        }
        else { _gridSize = 10; _spacing = 2.5f; }

        ClearAll();

        // Reset FPS tracking for new grid
        _deltaTimes.Clear();
        _fpsFrameCount = 0;

        if (DataContext is RenderingPerformanceViewModel vm2)
        {
            if (vm2.IsIndividualMode)
                BuildIndividualMeshes();
            else if (vm2.IsInstancedMode)
                BuildInstanced();
            else
                BuildHISM();
        }
    }

    private void ClearAll()
    {
        foreach (var m in _fcMeshes) aura3Dview.Remove(m);
        _fcMeshes.Clear();
        if (_instMesh != null) { aura3Dview.Remove(_instMesh); _instMesh = null; }
        if (_hismGroup != null) { aura3Dview.Remove(_hismGroup); _hismGroup = null; }
    }

    private void ApplyCulling()
    {
        if (aura3Dview.Scene?.RenderPipeline == null) return;
        if (DataContext is RenderingPerformanceViewModel vm)
        {
            aura3Dview.Scene.RenderPipeline.EnableFrustumCulling =
                vm.IsHISMMode ? hismCullingCheck.IsChecked == true : cullingCheck.IsChecked == true;
        }
    }

    /// <summary>
    /// Generate all cube positions for an N×N×N grid centered at origin.
    /// All three techniques use the exact same positions.
    /// </summary>
    private IEnumerable<Vector3> GeneratePositions()
    {
        float half = (_gridSize - 1) * _spacing / 2f;
        for (int x = 0; x < _gridSize; x++)
            for (int y = 0; y < _gridSize; y++)
                for (int z = 0; z < _gridSize; z++)
                    yield return new Vector3(x * _spacing - half, y * _spacing - half, z * _spacing - half);
    }

    // ═══════════════════════════════════════════════════════════
    //  Individual Meshes — N³ Mesh nodes, N³ draw calls
    // ═══════════════════════════════════════════════════════════

    private void BuildIndividualMeshes()
    {
        foreach (var pos in GeneratePositions())
        {
            var mesh = new Mesh
            {
                Geometry = _sharedBoxGeo,
                Material = _sharedWhiteMat,
                Position = pos
            };
            aura3Dview.AddNode(mesh);
            _fcMeshes.Add(mesh);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  GPU Instancing — 1 InstancedMesh, 1 draw call
    // ═══════════════════════════════════════════════════════════

    private void BuildInstanced()
    {
        var src = new Mesh { Geometry = new BoxGeometry(), Material = _sharedWhiteMat };
        _instMesh = InstancedMesh.FromMesh(src);

        foreach (var pos in GeneratePositions())
            _instMesh.AddInstance(Matrix4x4.CreateTranslation(pos));

        aura3Dview.AddNode(_instMesh);
    }

    // ═══════════════════════════════════════════════════════════
    //  HISM — InstancedMeshGroup, spatial partition tree
    // ═══════════════════════════════════════════════════════════

    private void BuildHISM()
    {
        var maxPerGroup = 1024;
        var maxDepth = 6;
        if (DataContext is RenderingPerformanceViewModel vm)
        {
            if (!int.TryParse(vm.HismMaxPerGroupText?.Trim(), out maxPerGroup) || maxPerGroup <= 0) maxPerGroup = 1024;
            if (!int.TryParse(vm.HismMaxDepthText?.Trim(), out maxDepth) || maxDepth <= 0) maxDepth = 6;
        }

        var src = new Mesh { Geometry = new BoxGeometry(), Material = _sharedWhiteMat };
        _hismGroup = new InstancedMeshGroup(src)
        {
            MaxInstancesPerGroup = maxPerGroup,
            MaxDepth = maxDepth
        };

        var transforms = new List<Matrix4x4>();
        foreach (var pos in GeneratePositions())
            transforms.Add(Matrix4x4.CreateTranslation(pos));

        _hismGroup.SetInstances(transforms);
        _hismGroup.Build();
        aura3Dview.AddNode(_hismGroup);
    }

    // ─── HISM Instance Update ────────────────────────────────

    private void UpdateHISMInstance()
    {
        if (_hismGroup?.InstanceCount == 0) return;
        if (DataContext is not RenderingPerformanceViewModel vm) return;

        if (!int.TryParse(vm.InstanceIdxText?.Trim(), out var idx) || idx < 0 || idx >= _hismGroup!.InstanceCount) return;
        if (!float.TryParse(vm.PosXText?.Trim(), out var px)) return;
        if (!float.TryParse(vm.PosYText?.Trim(), out var py)) return;
        if (!float.TryParse(vm.PosZText?.Trim(), out var pz)) return;

        _hismGroup.UpdateInstance(idx, Matrix4x4.CreateTranslation(new Vector3(px, py, pz)));
    }

    private void RandomMoveHISM()
    {
        if (_hismGroup?.InstanceCount == 0) return;

        var rand = new Random();
        int idx = rand.Next(_hismGroup!.InstanceCount);
        float range = _gridSize * _spacing * 2;
        float x = (float)((rand.NextDouble() - 0.5) * range);
        float y = (float)(rand.NextDouble() * range * 0.5f);
        float z = (float)((rand.NextDouble() - 0.5) * range);

        _hismGroup.UpdateInstance(idx, Matrix4x4.CreateTranslation(new Vector3(x, y, z)));

        if (DataContext is RenderingPerformanceViewModel vm)
        {
            vm.InstanceIdxText = idx.ToString();
            vm.PosXText = x.ToString("F1");
            vm.PosYText = y.ToString("F1");
            vm.PosZText = z.ToString("F1");
        }
    }
}
