using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Example.ViewModels;

public partial class DebugTestViewModel : ViewModelBase
{
    // ═══════════════════════════════════════════════
    //  Debug Draw
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private bool _showAxes = true;

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _debugEnable;

    [ObservableProperty]
    private bool _showBoundingBox;

    [ObservableProperty]
    private bool _showDirectionalLight;

    [ObservableProperty]
    private bool _showPointLight;

    [ObservableProperty]
    private bool _showSpotLight;

    [ObservableProperty]
    private bool _showCamera;

    [ObservableProperty]
    private bool _showBone;

    [ObservableProperty]
    private bool _showParticleBounds;

    // ═══════════════════════════════════════════════
    //  Picking
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private bool _enablePicking = true;

    [ObservableProperty]
    private string _pickInfo = "Click a model in the 3D view to pick";

    // ═══════════════════════════════════════════════
    //  Lights
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private bool _dirLightEnabled = true;

    [ObservableProperty]
    private float _dirLightRotX = -40f;

    [ObservableProperty]
    private float _dirLightRotY = -30f;

    [ObservableProperty]
    private float _dirLightIrradiance = 80000f;

    [ObservableProperty]
    private Color _dirLightColor = Color.FromArgb(255, 80, 80, 80);

    [ObservableProperty]
    private bool _pointLightEnabled = true;

    [ObservableProperty]
    private float _pointLightX = -8f;

    [ObservableProperty]
    private float _pointLightY = 6f;

    [ObservableProperty]
    private float _pointLightZ = 3f;

    [ObservableProperty]
    private float _pointLightIntensity = 5000f;

    [ObservableProperty]
    private Color _pointLightColor = Color.FromArgb(255, 255, 80, 80);

    [ObservableProperty]
    private float _pointLightRadius = 10f;

    [ObservableProperty]
    private bool _spotLightEnabled = true;

    [ObservableProperty]
    private float _spotLightX = 8f;

    [ObservableProperty]
    private float _spotLightY = 6f;

    [ObservableProperty]
    private float _spotLightZ = 3f;

    [ObservableProperty]
    private float _spotLightRotX = -75f;

    [ObservableProperty]
    private float _spotLightRotY = -20f;

    [ObservableProperty]
    private float _spotLightRotZ;

    [ObservableProperty]
    private float _spotLightIntensity = 10000f;

    [ObservableProperty]
    private Color _spotLightColor = Color.FromArgb(255, 80, 255, 80);

    [ObservableProperty]
    private float _spotLightInnerAngle = 10f;

    [ObservableProperty]
    private float _spotLightOuterAngle = 25f;

    [ObservableProperty]
    private float _spotLightRadius = 12f;

    // ═══════════════════════════════════════════════
    //  Culling & Performance
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private bool _enableFrustumCulling = true;

    [ObservableProperty]
    private float _boundingBoxPadding;

    [ObservableProperty]
    private int _staticMeshCount = 12;

    [ObservableProperty]
    private int _skinnedMeshCount = 25;

    [ObservableProperty]
    private int _visibleCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _detailText = "Click Build to construct the scene";

    // ═══════════════════════════════════════════════
    //  FPS
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private int _currentFps;

    [ObservableProperty]
    private int _minFps = int.MaxValue;

    [ObservableProperty]
    private int _maxFps;

    [ObservableProperty]
    private int _avgFps;

    [ObservableProperty]
    private double _fpsBarWidth;

    [ObservableProperty]
    private string _fpsBarColor = "#6BCB77";

    // ═══════════════════════════════════════════════
    //  Fire
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private float _firePosX = 5f;

    [ObservableProperty]
    private float _firePosY = 1.5f;

    [ObservableProperty]
    private float _firePosZ = 5f;

    // ═══════════════════════════════════════════════
    //  Dust (Mesh Particles)
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private float _dustPosX = 3f;

    [ObservableProperty]
    private float _dustPosY = 0.05f;

    [ObservableProperty]
    private float _dustPosZ = 3f;

    [ObservableProperty]
    private float _dustRotX;

    [ObservableProperty]
    private float _dustRotY;

    [ObservableProperty]
    private float _dustRotZ;

    // ═══════════════════════════════════════════════
    //  Build Command
    // ═══════════════════════════════════════════════

    /// <summary>The code-behind subscribes to the build logic through this event.</summary>
    public event Action BuildRequested;

    [RelayCommand]
    private void Build() => BuildRequested?.Invoke();
}
