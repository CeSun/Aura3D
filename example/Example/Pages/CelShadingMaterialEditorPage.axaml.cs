using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AvaloniaVector = Avalonia.Vector;

namespace Example.Pages;

public partial class CelShadingMaterialEditorPage : UserControl
{
    DirectionalLight dl;

    private CameraController _cameraController;
    private CelShadingMaterialEditorViewModel? _vm;

    public CelShadingMaterialEditorPage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3Dview);
    }

    private void Aura3DView_SceneInitialized(object? sender, Aura3D.Avalonia.InitializedRoutedEventArgs e)
    {
        var view = (Aura3DView)sender;

        _vm = DataContext as CelShadingMaterialEditorViewModel;

        var camera = view.MainCamera;

        camera.ProjectionType = ProjectionType.Perspective;


        var list = new List<Stream>();
        List<string> name =
        [
            "px.png",
                "nx.png",
                "py.png",
                "ny.png",
                "pz.png",
                "nz.png",
            ];
        foreach (var filename in name)
        {
            var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Textures/skybox/{filename}"));
            list.Add(stream);
        }

        var cubeTexture = TextureLoader.LoadCubeTexture(list);

        foreach (var stream in list)
        {
            stream.Dispose();
        }

        view.Scene.Background = cubeTexture;

        PointLight pl = new PointLight();

        pl.AttenuationRadius = 2f;

        pl.LightColor = System.Drawing.Color.Green;

        //view.AddNode(pl);

        PointLight pl2 = new PointLight();

        pl2.AttenuationRadius = 2f;

        pl2.LightColor = System.Drawing.Color.Red;

        pl2.CastShadow = true;

        //view.AddNode(pl2);

        dl = new DirectionalLight();

        dl.RotationDegrees = new Vector3(-45, 45, 0);

        dl.CastShadow = false;

        view.AddNode(dl);


        using (var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/NPC_Avatar_Girl_Sword_Nilou.glb")))
        {
            var model = ModelLoader.LoadGlbModel(s);
            model.Name = "Nilou";

            view.AddNode(model);

            model.Position = camera.Position + camera.Forward * 10;

            model.Position += model.Up * 0.5f;

            model.Scale = Vector3.One * 2f;
            model.RotationDegrees = new Vector3(0, 0, 0);

            pl.Position = model.Position + pl.Up * 2 + pl.Left * 2f;

            pl.Position = pl.Position + pl.Backward * 1;

            pl2.Position = model.Position + pl2.Up * 2 + pl2.Right * 2f;

            pl2.Position = pl2.Position + pl2.Backward * 1;

        }


        using (var s = AssetLoader.Open(new Uri("avares://Example/Assets/Models/coffee_table_round_01_1k.glb")))
        {
            var model = ModelLoader.LoadGlbModel(s);

            view.AddNode(model);

            model.Position = camera.Position + camera.Forward * 10;

            model.Position += camera.Down * 2;

            model.Scale = Vector3.One * 5f;
        }

        camera.Position = camera.Position + camera.Up * 2 + camera.Forward * 3;

        camera.Position = camera.Position + camera.Forward * 3;

        RefreshNodeTree(view);
    }

    private void Aura3DView_SceneUpdated(object? sender, Aura3D.Avalonia.UpdateRoutedEventArgs args)
    {
        dl.RotationDegrees = dl.RotationDegrees + (new Vector3(0, 30, 0) * (float)args.DeltaTime);
    }

    private void NodeTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm == null) return;

        if (NodeTree.SelectedItem is not NodeItem nodeItem || nodeItem.Node is not Mesh mesh)
        {
            _vm.Channels.Clear();
            _vm.Parameters.Clear();
            _vm.HasMaterial = false;
            _vm.CurrentMaterial = null;
            _vm.CurrentMesh = null;
            return;
        }

        var material = mesh.Material;
        if (material == null)
        {
            _vm.Channels.Clear();
            _vm.Parameters.Clear();
            _vm.HasMaterial = false;
            _vm.CurrentMaterial = null;
            _vm.CurrentMesh = null;
            return;
        }

        _vm.CurrentMaterial = material;
        _vm.CurrentMesh = mesh;
        _vm.HasMaterial = true;

        // Channels
        _vm.Channels.Clear();
        foreach (var channel in material.Channels)
        {
            if (channel.Texture is Texture tex)
            {
                var thumbnail = TextureToThumbnail(tex);
                _vm.Channels.Add(new ChannelItem(channel.Name, tex, thumbnail));
            }
            else if (channel.Texture != null)
            {
                _vm.Channels.Add(new ChannelItem(channel.Name, channel.Texture, null));
            }
        }

        // Parameters
        _vm.Parameters.Clear();
        foreach (var kv in material.EnumerateParameters())
        {
            _vm.Parameters.Add(new ParameterItem(kv.Key, kv.Value, material));
        }
    }

    private async void OpenModel_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open GLB Model",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("GLB Files") { Patterns = ["*.glb"] },
                new FilePickerFileType("glTF Files") { Patterns = ["*.gltf"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] }
            ]
        });

        if (files.Count == 0) return;

        try
        {
            var path = files[0].Path.LocalPath;
            var view = aura3Dview;
            var camera = view.MainCamera;

            Aura3D.Core.Nodes.Model model;
            if (path.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                model = ModelLoader.LoadGltfModel(path);
            else
                model = ModelLoader.LoadGlbModel(path);

            model.Position = camera.Position + camera.Forward * 2;

            view.AddNode(model);
            RefreshNodeTree(view);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open model: {ex.Message}");
        }
    }

    private async void SaveModelMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (NodeTree.SelectedItem is not NodeItem nodeItem || nodeItem.Node is not Aura3D.Core.Nodes.Model model)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window owner)
            {
                var dialog = new Window
                {
                    Title = "提示",
                    Width = 300,
                    Height = 120,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    Content = new TextBlock
                    {
                        Text = "请先在场景大纲中选择一个 Model 类型的节点",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Margin = new Avalonia.Thickness(16)
                    }
                };
                await dialog.ShowDialog(owner);
            }
            return;
        }

        await DoSaveModel(model);
    }

    private async void SaveModel_Click(object? sender, RoutedEventArgs e)
    {
        if (NodeTree.SelectedItem is not NodeItem nodeItem) return;
        if (nodeItem.Node is not Aura3D.Core.Nodes.Model model) return;
        await DoSaveModel(model);
    }

    private async Task DoSaveModel(Aura3D.Core.Nodes.Model model)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Model as GLB",
            SuggestedFileName = $"{model.Name ?? "model"}.glb",
            FileTypeChoices =
            [
                new FilePickerFileType("GLB File") { Patterns = ["*.glb"] }
            ]
        });

        if (file == null) return;

        try
        {
            ModelExporter.SaveGlbModel(model, file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save model: {ex.Message}");
        }
    }

    private async void ChannelThumbnail_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.CurrentMaterial == null) return;

        var border = sender as Border;
        if (border?.DataContext is not ChannelItem channelItem) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window owner) return;

        // Show dialog with two options
        var dialog = new Window
        {
            Title = "选择贴图来源",
            Width = 320,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"请选择「{channelItem.Name}」贴图的来源",
                        FontSize = 13,
                        Margin = new Avalonia.Thickness(0, 0, 0, 4)
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 12,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Children =
                        {
                            new Button
                            {
                                Content = "打开文件",
                                Width = 120,
                                Tag = "OpenFile"
                            },
                            new Button
                            {
                                Content = "从颜色生成",
                                Width = 120,
                                Tag = "GenerateFromColor"
                            }
                        }
                    }
                }
            }
        };

        string? choice = null;
        var openFileBtn = (Button)((StackPanel)((StackPanel)dialog.Content!).Children[1]).Children[0];
        var genColorBtn = (Button)((StackPanel)((StackPanel)dialog.Content!).Children[1]).Children[1];

        openFileBtn.Click += (_, _) => { choice = "OpenFile"; dialog.Close(); };
        genColorBtn.Click += (_, _) => { choice = "GenerateFromColor"; dialog.Close(); };

        await dialog.ShowDialog(owner);

        if (choice == null) return;

        if (choice == "OpenFile")
        {
            await OpenTextureFromFile(channelItem, topLevel);
        }
        else if (choice == "GenerateFromColor")
        {
            await GenerateTextureFromColor(channelItem, owner);
        }
    }

    private async Task OpenTextureFromFile(ChannelItem channelItem, TopLevel topLevel)
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Texture",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image Files") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga", "*.webp"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] }
            ]
        });

        if (files.Count == 0) return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            var newTexture = TextureLoader.LoadTexture(stream);
            ApplyTextureToChannel(channelItem, newTexture);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load texture: {ex.Message}");
        }
    }

    private async Task GenerateTextureFromColor(ChannelItem channelItem, Window owner)
    {
        var colorPicker = new ColorPicker
        {
            Color = Avalonia.Media.Colors.White,
            Width = 260,
            Height = 280
        };

        var dialog = new Window
        {
            Title = "选择颜色",
            Width = 300,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Children =
                {
                    colorPicker,
                    new Button
                    {
                        Content = "确定",
                        Width = 120,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    }
                }
            }
        };

        var confirmBtn = (Button)((StackPanel)dialog.Content!).Children[1];
        bool confirmed = false;
        confirmBtn.Click += (_, _) => { confirmed = true; dialog.Close(); };

        await dialog.ShowDialog(owner);

        if (!confirmed) return;

        var avaloniaColor = colorPicker.Color;
        var drawingColor = System.Drawing.Color.FromArgb(avaloniaColor.A, avaloniaColor.R, avaloniaColor.G, avaloniaColor.B);
        var newTexture = Texture.CreateFromColor(drawingColor);
        ApplyTextureToChannel(channelItem, newTexture);
    }

    private void ApplyTextureToChannel(ChannelItem channelItem, Texture newTexture)
    {
        _vm!.CurrentMaterial!.SetTexture(channelItem.Name, newTexture);

        var index = _vm.Channels.IndexOf(channelItem);
        if (index >= 0)
        {
            var thumbnail = TextureToThumbnail(newTexture);
            _vm.Channels[index] = new ChannelItem(channelItem.Name, newTexture, thumbnail);
        }
    }

    private const string CelShadingExtensionName = "AURA3D_TEXTURES_CELSHADING";

    private static readonly string[] CelShadingTextureChannels = ["ILM", "SDF", "ShadowRamp", "SpecularRamp"];

    private void InitCelShading_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.CurrentMaterial == null) return;

        var material = _vm.CurrentMaterial;

        if (!material.ExtensionNames.Contains(CelShadingExtensionName))
            material.ExtensionNames.Add(CelShadingExtensionName);

        // Set default parameters
        material.SetParameterValue<int>("RenderType", 0);

        material.SetParameterValue<float>("_RampIndex0", 0);
        material.SetParameterValue<float>("_RampIndex1", 1);
        material.SetParameterValue<float>("_RampIndex2", 2);
        material.SetParameterValue<float>("_RampIndex3", 3);
        material.SetParameterValue<float>("_RampIndex4", 4);

        material.SetParameterValue<float>("_BrightFac", 1.0f);
        material.SetParameterValue<float>("_GreyFac", 0.5f);
        material.SetParameterValue<float>("_DarkFac", 0.2f);
        material.SetParameterValue<float>("_BrightAreaShadowFac", 1.0f);

        material.SetParameterValue<Vector4>("_LightAreaColorTint", new Vector4(1, 1, 1, 1));
        material.SetParameterValue<Vector4>("_DarkShadowColor", new Vector4(0.5f, 0.5f, 0.5f, 1));
        material.SetParameterValue<Vector4>("_CoolDarkShadowColor", new Vector4(0.5f, 0.5f, 0.6f, 1));

        material.SetParameterValue<float>("_FaceShadowOffset", 0);
        material.SetParameterValue<float>("_FaceShadowTransitionSoftness", 1.0f);

        // Reset CelShading texture channels to 2x2 white
        var whiteTex = Texture.CreateFromColor(System.Drawing.Color.White);
        foreach (var channelName in CelShadingTextureChannels)
        {
            material.SetTexture(channelName, whiteTex);
        }

        // Refresh display
        _vm.Channels.Clear();
        foreach (var channel in material.Channels)
        {
            if (channel.Texture is Texture tex)
            {
                var thumbnail = TextureToThumbnail(tex);
                _vm.Channels.Add(new ChannelItem(channel.Name, tex, thumbnail));
            }
            else if (channel.Texture != null)
            {
                _vm.Channels.Add(new ChannelItem(channel.Name, channel.Texture, null));
            }
        }

        _vm.Parameters.Clear();
        foreach (var kv in material.EnumerateParameters())
        {
            _vm.Parameters.Add(new ParameterItem(kv.Key, kv.Value, material));
        }
    }

    private static WriteableBitmap? TextureToThumbnail(Texture tex)
    {
        if (tex.LdrData == null || tex.LdrData.Count == 0 || tex.Width == 0 || tex.Height == 0)
            return null;

        try
        {
            var width = (int)tex.Width;
            var height = (int)tex.Height;
            var bitmap = new WriteableBitmap(new PixelSize(width, height), new AvaloniaVector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

            using var fb = bitmap.Lock();
            var srcData = tex.LdrData.ToArray();
            var dstRowBytes = fb.RowBytes;
            var srcChannels = tex.ColorFormat == ColorFormat.RGBA ? 4 : 3;

            for (int y = 0; y < height; y++)
            {
                var srcOffset = y * width * srcChannels;
                var dstOffset = y * dstRowBytes;
                for (int x = 0; x < width; x++)
                {
                    var si = srcOffset + x * srcChannels;
                    byte r = srcData[si];
                    byte g = srcData[si + 1];
                    byte b = srcData[si + 2];
                    byte a = srcChannels == 4 ? srcData[si + 3] : (byte)255;

                    // Write BGRA
                    Marshal.WriteByte(fb.Address + dstOffset + x * 4, b);
                    Marshal.WriteByte(fb.Address + dstOffset + x * 4 + 1, g);
                    Marshal.WriteByte(fb.Address + dstOffset + x * 4 + 2, r);
                    Marshal.WriteByte(fb.Address + dstOffset + x * 4 + 3, a);
                }
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void RefreshNodeTree(Aura3DView view)
    {
        if (_vm == null || view.Scene == null) return;

        var rootNodes = view.Scene.Nodes.Where(n => n.Parent == null);
        _vm.RootNodes.Clear();
        foreach (var node in rootNodes)
        {
            _vm.RootNodes.Add(BuildNodeItem(node));
        }
    }

    private static NodeItem BuildNodeItem(Node node)
    {
        var typeName = node.GetType().Name;
        var displayName = string.IsNullOrEmpty(node.Name) ? $"NoName ({typeName})" : $"{node.Name} ({typeName})";
        var item = new NodeItem(displayName, node);

        foreach (var child in node.Children)
        {
            item.Children.Add(BuildNodeItem(child));
        }

        return item;
    }
}
