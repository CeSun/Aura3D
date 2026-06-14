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

    private async void ChannelThumbnail_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.CurrentMaterial == null) return;

        var border = sender as Border;
        if (border?.DataContext is not ChannelItem channelItem) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

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
            _vm.CurrentMaterial.SetTexture(channelItem.Name, newTexture);

            // Refresh the channel display
            var index = _vm.Channels.IndexOf(channelItem);
            if (index >= 0)
            {
                var thumbnail = TextureToThumbnail(newTexture);
                _vm.Channels[index] = new ChannelItem(channelItem.Name, newTexture, thumbnail);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load texture: {ex.Message}");
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
