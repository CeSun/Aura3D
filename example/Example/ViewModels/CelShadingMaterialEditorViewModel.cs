using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Example.ViewModels;

public partial class CelShadingMaterialEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<NodeItem> _rootNodes = [];

    [ObservableProperty]
    private ObservableCollection<ChannelItem> _channels = [];

    [ObservableProperty]
    private ObservableCollection<ParameterItem> _parameters = [];

    [ObservableProperty]
    private bool _hasMaterial;

    public Material? CurrentMaterial { get; set; }
    public Mesh? CurrentMesh { get; set; }
}

public class NodeItem
{
    public string DisplayName { get; }
    public Node Node { get; }
    public ObservableCollection<NodeItem> Children { get; } = [];

    public NodeItem(string displayName, Node node)
    {
        DisplayName = displayName;
        Node = node;
    }
}

public class ChannelItem
{
    public string Name { get; }
    public ITexture? Texture { get; }
    public IImage? Thumbnail { get; }

    public ChannelItem(string name, ITexture? texture, IImage? thumbnail)
    {
        Name = name;
        Texture = texture;
        Thumbnail = thumbnail;
    }
}

public class ParameterItem : ObservableObject
{
    public string Key { get; }
    public string TypeName { get; }
    public ParameterKind Kind { get; }

    private string _stringValue;
    public string StringValue
    {
        get => _stringValue;
        set => SetProperty(ref _stringValue, value);
    }

    private Color _colorValue;
    public Color ColorValue
    {
        get => _colorValue;
        set
        {
            if (SetProperty(ref _colorValue, value))
            {
                StringValue = OriginalValue is Vector3
                    ? $"({value.R / 255f:F3}, {value.G / 255f:F3}, {value.B / 255f:F3})"
                    : $"({value.R / 255f:F3}, {value.G / 255f:F3}, {value.B / 255f:F3}, {value.A / 255f:F3})";
            }
        }
    }

    public bool IsColor => Kind == ParameterKind.Color;
    public bool IsFloat => Kind == ParameterKind.Float;
    public bool IsInt => Kind == ParameterKind.Int;
    public bool IsOther => Kind == ParameterKind.Other;

    private bool _applying;

    public ParameterItem(string key, object value, Material material)
    {
        Key = key;
        TypeName = value.GetType().Name;
        _stringValue = "";
        _colorValue = Colors.White;

        switch (value)
        {
            case Vector4 v4:
                Kind = ParameterKind.Color;
                _colorValue = Color.FromArgb(
                    (byte)(Math.Clamp(v4.W, 0, 1) * 255),
                    (byte)(Math.Clamp(v4.X, 0, 1) * 255),
                    (byte)(Math.Clamp(v4.Y, 0, 1) * 255),
                    (byte)(Math.Clamp(v4.Z, 0, 1) * 255));
                _stringValue = $"({v4.X:F3}, {v4.Y:F3}, {v4.Z:F3}, {v4.W:F3})";
                break;
            case Vector3 v3:
                Kind = ParameterKind.Color;
                _colorValue = Color.FromArgb(255,
                    (byte)(Math.Clamp(v3.X, 0, 1) * 255),
                    (byte)(Math.Clamp(v3.Y, 0, 1) * 255),
                    (byte)(Math.Clamp(v3.Z, 0, 1) * 255));
                _stringValue = $"({v3.X:F3}, {v3.Y:F3}, {v3.Z:F3})";
                break;
            case float f:
                Kind = ParameterKind.Float;
                _stringValue = f.ToString("F4");
                break;
            case int i:
                Kind = ParameterKind.Int;
                _stringValue = i.ToString();
                break;
            default:
                Kind = ParameterKind.Other;
                _stringValue = value.ToString() ?? "";
                break;
        }

        Material = material;
        OriginalValue = value;

        PropertyChanged += OnPropertyChanged;
    }

    public object OriginalValue { get; }
    private Material Material { get; }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_applying) return;

        switch (e.PropertyName)
        {
            case nameof(ColorValue):
                ApplyColorToMaterial();
                break;
            case nameof(StringValue):
                if (Kind is ParameterKind.Float or ParameterKind.Int) ApplyScalarToMaterial();
                break;
        }
    }

    private void ApplyColorToMaterial()
    {
        _applying = true;
        var c = ColorValue;
        switch (OriginalValue)
        {
            case Vector4:
                Material.SetParameterValue(Key, new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f));
                break;
            case Vector3:
                Material.SetParameterValue(Key, new Vector3(c.R / 255f, c.G / 255f, c.B / 255f));
                break;
        }
        _applying = false;
    }

    private void ApplyScalarToMaterial()
    {
        switch (OriginalValue)
        {
            case float:
                if (float.TryParse(StringValue, out var fv))
                    Material.SetParameterValue(Key, fv);
                break;
            case int:
                if (int.TryParse(StringValue, out var iv))
                    Material.SetParameterValue(Key, iv);
                break;
        }
    }
}

public enum ParameterKind
{
    Color,
    Float,
    Int,
    Other,
}
