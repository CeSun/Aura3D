using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Example.ViewModels;

public partial class EmitterViewModel : ObservableObject
{
    // Identity
    [ObservableProperty] private string _name = "Emitter";

    // Render mode: 0 = Billboard, 1 = Mesh
    [ObservableProperty] private int _renderMode;

    // Emission
    [ObservableProperty] private int _maxParticles = 50000;
    [ObservableProperty] private float _emissionRate = 500f;
    [ObservableProperty] private int _shapeIndex;
    [ObservableProperty] private float _shapeSizeX = 2f;
    [ObservableProperty] private float _shapeSizeY = 2f;
    [ObservableProperty] private float _shapeSizeZ = 2f;
    [ObservableProperty] private float _coneAngle = 30f;

    // Lifetime & Size
    [ObservableProperty] private float _lifetimeMin = 0.5f;
    [ObservableProperty] private float _lifetimeMax = 2.0f;
    [ObservableProperty] private float _startSizeMin = 0.1f;
    [ObservableProperty] private float _startSizeMax = 0.5f;
    [ObservableProperty] private float _endSizeMin = 0.01f;
    [ObservableProperty] private float _endSizeMax = 0.1f;

    // Velocity
    [ObservableProperty] private float _velocityXMin = -0.5f;
    [ObservableProperty] private float _velocityXMax = 0.5f;
    [ObservableProperty] private float _velocityYMin = 3f;
    [ObservableProperty] private float _velocityYMax = 7f;
    [ObservableProperty] private float _velocityZMin = -0.5f;
    [ObservableProperty] private float _velocityZMax = 0.5f;

    // Color
    [ObservableProperty] private Color _startColor = Color.FromArgb(255, 255, 165, 0);
    [ObservableProperty] private Color _endColor = Color.FromArgb(0, 255, 50, 0);

    // Physics
    [ObservableProperty] private float _gravityY = 1f;
    [ObservableProperty] private float _damping = 0.4f;

    // Rotation
    [ObservableProperty] private float _rotationMin;
    [ObservableProperty] private float _rotationMax = 6.28f;
    [ObservableProperty] private float _angularVelocityMin = -1f;
    [ObservableProperty] private float _angularVelocityMax = 1f;

    // Billboard texture
    [ObservableProperty] private string _texturePath = "";
    [ObservableProperty] private float _flipbookTilesX = 1f;
    [ObservableProperty] private float _flipbookTilesY = 1f;
    public Texture? LoadedTexture { get; set; }

    // Mesh rendering
    [ObservableProperty] private string _meshPath = "";
    [ObservableProperty] private float _meshScale = 1f;
    public Mesh? LoadedMesh { get; set; }

    public bool IsBillboard => RenderMode == 0;
    public bool IsMesh => RenderMode == 1;

    public IRelayCommand ResetCommand { get; }
    public IRelayCommand DeleteCommand { get; }

    public EmitterViewModel()
    {
        ResetCommand = new RelayCommand(Reset);
        DeleteCommand = new RelayCommand(() => _deleteAction?.Invoke(), () => _deleteAction != null);
    }

    private Action? _deleteAction;

    internal void SetDeleteAction(Action deleteAction)
    {
        _deleteAction = deleteAction;
        DeleteCommand.NotifyCanExecuteChanged();
    }

    public static EmitterViewModel CreateDefault(int index)
    {
        return new EmitterViewModel { Name = $"Emitter {index}" };
    }

    partial void OnRenderModeChanged(int value)
    {
        OnPropertyChanged(nameof(IsBillboard));
        OnPropertyChanged(nameof(IsMesh));
    }

    public void Reset()
    {
        MaxParticles = 50000;
        EmissionRate = 500;
        ShapeIndex = 0;
        ShapeSizeX = 2; ShapeSizeY = 2; ShapeSizeZ = 2;
        ConeAngle = 30;
        LifetimeMin = 0.5f; LifetimeMax = 2.0f;
        StartSizeMin = 0.1f; StartSizeMax = 0.5f;
        EndSizeMin = 0.01f; EndSizeMax = 0.1f;
        VelocityXMin = -0.5f; VelocityXMax = 0.5f;
        VelocityYMin = 3; VelocityYMax = 7;
        VelocityZMin = -0.5f; VelocityZMax = 0.5f;
        StartColor = Color.FromArgb(255, 255, 165, 0);
        EndColor = Color.FromArgb(0, 255, 50, 0);
        GravityY = 1; Damping = 0.4f;
        RotationMin = 0; RotationMax = 6.28f;
        AngularVelocityMin = -1; AngularVelocityMax = 1;
        RenderMode = 0;
        TexturePath = ""; FlipbookTilesX = 1; FlipbookTilesY = 1;
        MeshPath = ""; MeshScale = 1;
        LoadedTexture = null;
        LoadedMesh = null;
    }
}
