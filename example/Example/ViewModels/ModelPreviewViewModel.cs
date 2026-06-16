using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example.ViewModels;

public partial class ModelPreviewViewModel : ViewModelBase
{
    [ObservableProperty]
    public double _scale = 1.0;

    [ObservableProperty]
    public double _yaw = 1.0;
    [ObservableProperty]
    public double _pitch = 1.0;
    [ObservableProperty]
    public double _roll = 1.0;

    [ObservableProperty]
    public string _statusMessage = "Load a glb/gltf model, then save it as an Aura3D file and load it back.";
}
