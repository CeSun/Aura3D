using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Serialization;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Ursa.Common;
using Ursa.Controls;
using Camera = Aura3D.Core.Nodes.Camera;

namespace Example.Pages;

public partial class ModelPreviewPage : UserControl
{
    Model? lion;

    Model? solider;

    Model? woodenStool;

    Model? _currentModel;

    Vector3 modelPosition;

    Node node;

    Model? currentModel
    {
        get => _currentModel;
        set
        {
            if (_currentModel == value)
                return;
            if (aura3d.Scene == null)
                return;
            if (_currentModel != null)
            {
                aura3d.Remove(node);
                node.RemoveChild(_currentModel, AttachToParentRule.KeepWorld);
                _currentModel = null;
            }
            if (value != null)
            {

                _currentModel = value;

                if (DataContext is ModelPreviewViewModel vm == false)
                    return;

                vm.Roll = 0;
                vm.Pitch = 0;
                vm.Yaw = 0;

                if (currentModel == null || currentModel.BoundingBox == null)
                    return;

                var model = currentModel;


                var center = (model.BoundingBox.Max - model.BoundingBox.Min) / 2 + model.BoundingBox.Min;

                node.Position = center;

                node.RotationDegrees = Vector3.Zero;


                node.Scale = Vector3.One;
                
                node.AddChild(model, AttachToParentRule.KeepWorld);

                aura3d.Scene.AddNode(node);

                aura3d.MainCamera.FitToBoundingBox(_currentModel.BoundingBox);
            }
        }
    }
    private CameraController _cameraController;

    public ModelPreviewPage()
    {
        node = new Node();
        InitializeComponent();
        _cameraController = new CameraController(aura3d);
    }

    private async void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ModelPreviewViewModel vm == false)
            return;

        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel == null)
            return;

        List<FilePickerFileType> filePickerFileTypes = [
                    new ("Aura3D model"){ Patterns = ["*.aura"] },
                    new ("gltf file"){ Patterns = ["*.glb", "*.gltf"] },
                    new ("3D Manufacturing Format ") { Patterns = ["*.3mf"] },
                    new ("Collada") { Patterns = ["*.dae", "*.xml"] },
                    new ("Biovision BVH") { Patterns = ["*.bvh"] },
                    new ("3D Studio Max 3DS") { Patterns = ["*.3ds"] },
                    new ("3D Studio Max ASE") { Patterns = ["*.ase"] },
                    new ("FBX-Format, as ASCII and binary") { Patterns = ["*.fbx"] },
                    new ("Stanford Polygon Library") { Patterns = ["*.ply"] },
                    new ("AutoCAD DXF") { Patterns = ["*.dxf"] },
                    new ("IFC-STEP") { Patterns = ["*.ifc"] },
                    new ("IQM-Format") { Patterns = ["*.iqm"] },
                    new ("Neutral File Format") { Patterns = ["*.nff"] },
                    new ("Sense8 WorldToolkit") { Patterns = ["*.nff"] },
                    new ("Valve Model") { Patterns = ["*.smd", "*.vta"] },
                    new ("Quake I") { Patterns = ["*.mdl"] },
                    new ("Quake II") { Patterns = ["*.mdl2"] },
                    new ("Quake III") { Patterns = ["*.mdl3"] },
                    new ("Quake 3 BSP") { Patterns = ["*.pk3"] },
                    new ("RtCW") { Patterns = ["*.mdc"] },
                    new ("Doom 3") { Patterns = ["*.md5mesh", "*.md5anim", "*.md5camera"] },
                    new ("DirectX X") { Patterns = ["*.x"] },
                    new ("Quick3D") { Patterns = ["*.q3o", "*.q3s"] },
                    new ("Raw Triangles") { Patterns = ["*.raw"] },
                    new ("AC3D") { Patterns = ["*.ac", "*.ac3d"] },
                    new ("Stereolithography") { Patterns = ["*.stl"] },
                    new ("Autodesk DXF") { Patterns = ["*.dxf"] },
                    new ("Irrlicht Mesh") { Patterns = ["*.irrmesh", "*.xml"] },
                    new ("Irrlicht Scene ") { Patterns = ["*.irr", "*.xml"] },
                    new ("Object File Format") { Patterns = ["*.off"] },
                    new ("Wavefront Object") { Patterns = ["*.obj"] },
                    new ("Terragen Terrain") { Patterns = ["*.ter"] },
                    new ("3D GameStudio Model") { Patterns = ["*.mdl"] },
                    new ("3D GameStudio Terrain") { Patterns = ["*.hmp"] },
                    new ("Ogre") { Patterns = ["*.mesh.xml", "*.skeleton.xml", "*.material"] },
                    new ("OpenGEX-Fomat") { Patterns = ["*.ogex"] },
                    new ("Milkshape 3D") { Patterns = ["*.ms3d"] },
                    new ("LightWave Model") { Patterns = ["*.lwo"] },
                    new ("LightWave Scene") { Patterns = ["*.lws"] },
                    new ("Modo Model") { Patterns = ["*.lxo"] },
                    new ("CharacterStudio Motion") { Patterns = ["*.csm"] },
                    new ("Stanford Ply") { Patterns = ["*.ply"] },
                    new ("TrueSpace") { Patterns = ["*.cob", "*.scn"] },
                    new ("XGL-3D-Format") { Patterns = ["*.xgl"] }
                    ];
        List<string> allextensions = [];
        foreach(var filePickerFileType in filePickerFileTypes)
        {
            if (filePickerFileType.Patterns == null)
                continue;

            foreach(var pattern in filePickerFileType.Patterns)
            {
                allextensions.Add(pattern);
            }
        }
        filePickerFileTypes.Insert(0, new FilePickerFileType("all type file") { Patterns = allextensions });
        var files = await toplevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions()
            {
                Title = "Select Model File",
                AllowMultiple = false,
                FileTypeFilter = filePickerFileTypes
            });

        foreach (var file in files)
        {
            await LoadModelFileAsync(file, vm);
        }
    }

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {

        var dl = new DirectionalLight();

        dl.RotationDegrees = new Vector3(-30, 0, 0);

        dl.LightColor = Color.White;

        e.Scene.AddNode(dl);



        using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Textures/buikslotermeerplein_1k.hdr")))
        {
            var hdriTexture = TextureLoader.LoadHdrTexture(stream);

            var cubemap = HDRIToCubeTextureConverter.ConvertFromTexture(hdriTexture, 1024);

            e.Scene.Background = cubemap;
        }
    }

    private void Aura3DView_SceneUpdated(object? sender, Aura3D.Avalonia.UpdateRoutedEventArgs e)
    {
    }

    private async void OpenAuraButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ModelPreviewViewModel vm == false)
            return;

        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel == null)
            return;

        var files = await toplevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Aura3D Model",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Aura3D model") { Patterns = ["*.aura"] }
                ]
            });

        foreach (var file in files)
        {
            await LoadAuraFileAsync(file, vm);
        }
    }

    private async void SaveAuraButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ModelPreviewViewModel vm == false)
            return;

        if (currentModel == null)
        {
            vm.StatusMessage = "There is no model to save yet. Load a glb/gltf model first.";
            return;
        }

        var modelToSave = currentModel;

        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel == null)
            return;

        var file = await toplevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save Aura3D Model",
                SuggestedFileName = BuildSuggestedAuraFileName(currentModel.Name),
                DefaultExtension = "aura",
                FileTypeChoices =
                [
                    new FilePickerFileType("Aura3D model") { Patterns = ["*.aura"] }
                ]
            });

        if (file == null)
            return;

        var localPath = file.TryGetLocalPath();
        await Task.Run(async () =>
        {
            if (localPath != null)
            {
                AssetManager.SaveNode(modelToSave, localPath);
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            AssetManager.SaveNode(modelToSave, stream);
        });

        vm.StatusMessage = $"Saved current model to Aura3D file: {localPath ?? file.Name}.";
    }

    private async void Button_Click_1(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ModelPreviewViewModel vm == false)
            return;

        var button = sender as Button;
        if (button == null)
            return;
        var s = button.Content?.ToString();
        switch (s)
        {
            case "lion head":
                if (lion == null)
                {
                    lionButton.IsEnabled = false;
                    lion = await Task.Run(() =>
                    {
                        using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/lion_head_1k.glb")))
                        {
                            var model = ModelLoader.LoadGlbModel(stream);
                            model.RotationDegrees = Vector3.Zero;
                            return model;
                        }
                    });
                    lionButton.IsEnabled = true;
                }
                currentModel = lion;
                vm.StatusMessage = "Loaded built-in glb sample. You can now save it as an Aura3D file.";
                break;
            case "soldier":
                if (solider == null)
                {
                    soldierButton.IsEnabled = false;
                    solider = await Task.Run(() =>
                    {
                        using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/Soldier.glb")))
                        {
                            var model = ModelLoader.LoadGlbModel(stream);
                            model.RotationDegrees = new Vector3(0, 180, 0);
                            return model;
                        }
                    });
                    soldierButton.IsEnabled = true;
                }
                currentModel = solider;
                vm.StatusMessage = "Loaded built-in glb sample. You can now save it as an Aura3D file.";
                break;
            case "wooden stool":
                if (woodenStool == null)
                {
                    woodenButton.IsEnabled = false;
                    woodenStool = await Task.Run(() =>
                    {
                        using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/wooden_stool_02_1k.glb")))
                        {
                            var model = ModelLoader.LoadGlbModel(stream);
                            model.RotationDegrees = Vector3.Zero;
                            return model;
                        }
                    });
                    woodenButton.IsEnabled = true;
                }
                currentModel = woodenStool;
                vm.StatusMessage = "Loaded built-in glb sample. You can now save it as an Aura3D file.";
                break;
            case "ResetCamera":
                if (_currentModel != null)
                {
                    aura3d.MainCamera.FitToBoundingBox(_currentModel.BoundingBox);
                }
                break;
            default:
                break;
        }
        if (currentModel != null)
        {
            // vm.Scale = currentModel.Scale.X;
            //vm.Yaw = currentModel.RotationDegrees.Y;
        }

    }

    private void Slider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {

        if (DataContext is ModelPreviewViewModel vm == false)
            return;
        node.Scale = new Vector3((float)vm.Scale);
    }


    private void Slider_ValueChanged2(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {

        if (DataContext is ModelPreviewViewModel vm == false)
            return;
         node.RotationDegrees = new Vector3(node.RotationDegrees.X, (float)vm.Yaw, node.RotationDegrees.Z);
    }

    private void Slider_ValueChanged3(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {

        if (DataContext is ModelPreviewViewModel vm == false)
            return;
        node.RotationDegrees = new Vector3((float)vm.Pitch, node.RotationDegrees.Y, node.RotationDegrees.Z);
    }

    private void Slider_ValueChanged4(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {

        if (DataContext is ModelPreviewViewModel vm == false)
            return;
        node.RotationDegrees = new Vector3(node.RotationDegrees.X, node.RotationDegrees.Y, (float)vm.Roll);
    }

    private async Task LoadModelFileAsync(IStorageFile file, ModelPreviewViewModel vm)
    {
        var path = file.TryGetLocalPath();
        var extension = path != null ? Path.GetExtension(path).ToLowerInvariant() : null;

        if (extension == ".aura")
        {
            await LoadAuraFileAsync(file, vm);
            return;
        }

        Model? model = null;
        List<Animation> animations = [];

        if (path != null)
        {
            if (extension == ".glb")
            {
                (model, animations) = await Task.Run(() => ModelLoader.LoadGlbModelAndAnimations(path));
            }
            else if (extension == ".gltf")
            {
                (model, animations) = await Task.Run(() => ModelLoader.LoadGltfModelAndAnimations(path));
            }

            if (model == null)
            {
                (model, animations) = await Task.Run(() => AssimpLoader.LoadModelAndAnimations(path));
            }
        }
        else
        {
            await using var stream = await file.OpenReadAsync();
            (model, animations) = await Task.Run(() => AssimpLoader.LoadModelAndAnimations(stream));
        }

        ApplyLoadedModel(model, animations);
        vm.StatusMessage = $"Imported model from {path ?? file.Name}. You can now save it as an Aura3D file.";
    }

    private async Task LoadAuraFileAsync(IStorageFile file, ModelPreviewViewModel vm)
    {
        Model model;
        var path = file.TryGetLocalPath();

        if (path != null)
        {
            model = await Task.Run(() => AssetManager.LoadNode<Model>(path));
        }
        else
        {
            await using var stream = await file.OpenReadAsync();
            model = await Task.Run(() => AssetManager.LoadNode<Model>(stream));
        }

        ApplyLoadedModel(model);
        vm.StatusMessage = $"Loaded model from Aura3D file: {path ?? file.Name}.";
    }

    private void ApplyLoadedModel(Model model, List<Animation>? animations = null)
    {
        if (animations is { Count: > 0 })
        {
            model.AnimationSampler = new AnimationSampler(animations.First());
        }

        model.Position = modelPosition;
        model.Position = modelPosition - model.Up * 1;
        model.RotationDegrees = Vector3.Zero;

        currentModel = model;

        if (DataContext is ModelPreviewViewModel vm)
        {
            vm.Scale = 1;
            vm.Yaw = model.RotationDegrees.Y;
            vm.Pitch = model.RotationDegrees.X;
            vm.Roll = model.RotationDegrees.Z;
        }
    }

    private static string BuildSuggestedAuraFileName(string? modelName)
    {
        var baseName = string.IsNullOrWhiteSpace(modelName) ? "model" : modelName.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(invalidChar, '_');
        }

        return $"{baseName}.aura";
    }
}
