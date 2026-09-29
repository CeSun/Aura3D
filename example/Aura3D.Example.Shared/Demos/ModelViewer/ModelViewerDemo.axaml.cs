using Aura3D.Core.Geometries;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Examples.Kit;
using Aura3D.Examples.Localization;
using Avalonia.Interactivity;
using Irihi.Lingua;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ModelNode = Aura3D.Core.Nodes.Model;

namespace Aura3D.Examples.Demos;

/// <summary>
/// glTF/GLB 加载出来的 <see cref="ModelNode"/> 节点树：加载器把 glTF 的哪些东西搬进了引擎，
/// 模型级包围盒的两个属性管什么，以及 <see cref="ModelNode.Clone(CopyType)"/> 三种副本到底共享了什么。
/// 库里的模型分两档取：进页即取的算在本页预算里，页内按需取的不算——这是 web 端控制首屏字节的办法。
/// 参数行的排版全在 <c>ModelViewerDemo.axaml</c> 里，这里只剩场景组装与回调。
/// </summary>
public sealed partial class ModelViewerDemo : Demo
{
    // 三个进页即取（清单里合计约 3 MB），三个挂在页内按需取，选到才下载。
    // 后者不计入功能页的预算，因为 AssetBatch 允许取本页集合之外的 Key（回落到全表）。
    private static readonly (string Key, LinguaKey Label, bool Preload)[] Library =
    [
        ("Stool", Strings.Keys.ModelViewer_OptionStool, true),
        ("CoffeeTable", Strings.Keys.ModelViewer_OptionCoffeeTable, true),
        ("LionHead", Strings.Keys.ModelViewer_OptionLionHead, true),
        ("Lightbulb", Strings.Keys.ModelViewer_OptionLightbulb, false),
        ("Stones", Strings.Keys.ModelViewer_OptionStones, false),
        ("Present", Strings.Keys.ModelViewer_OptionPresent, false),
        ("CelCharacter", Strings.Keys.ModelViewer_OptionCelCharacter, false),
    ];

    private static readonly LinguaKey[] CopyModeKeys =
    [
        Strings.Keys.ModelViewer_CopyOptionSharedResource,
        Strings.Keys.ModelViewer_CopyOptionSharedResourceData,
        Strings.Keys.ModelViewer_CopyOptionFullCopy,
    ];

    // 下拉文案里的 CopyType 枚举名是技术标识，不翻：读数行只引用名字部分。
    private static readonly string[] CopyModeNames = ["SharedResource", "SharedResourceData", "FullCopy"];

    private readonly Dictionary<string, ModelNode> loaded = [];
    private readonly List<ModelNode> clones = [];

    private AssetBatch batch = null!;
    private bool loading;

    private ModelNode? current;
    private string selectedKey = Library[0].Key;
    private float padding = 0.12f;
    private float modelPadding;
    private bool customBox;
    private int copyModeIndex;

    /// <summary>当前模型下拉的选项，供 XAML 的 <c>Options="{Binding ModelLabels}"</c> 绑定。</summary>
    public IList ModelLabels { get; } = Library.Select(i => i.Label.T()).ToList();

    /// <summary>CopyType 下拉的选项，供 XAML 的 <c>Options="{Binding CopyModeOptions}"</c> 绑定。</summary>
    public IList CopyModeOptions { get; } = CopyModeKeys.Select(k => k.T()).ToList();

    /// <summary>
    /// 建页：装配 XAML，并把两个下拉的选中项对齐到本页当前状态。
    /// </summary>
    /// <param name="context">宿主环境。</param>
    public ModelViewerDemo(DemoContext context) : base(context)
    {
        InitializeComponent();

        ModelRow.SelectedItem = LabelOf(selectedKey);
        CopyModeRow.SelectedItem = CopyModeOptions[copyModeIndex];

        // XAML 里这个开关画的是勾选态，而引擎默认是关的：不进设置同步一次，
        // 一进页就看不到线框，面板与真值也对不上。
        Context.Settings.Debug.ShowBoundingBox = true;
    }

    /// <inheritdoc />
    public override async Task LoadAssetsAsync(AssetBatch assets)
    {
        batch = assets;

        foreach (var (key, _, preload) in Library.Where(i => i.Preload))
        {
            var model = await assets.ModelAsync(key);

            model.Name = key;

            loaded[key] = model;
        }
    }

    /// <inheritdoc />
    public override void BuildScene()
    {
        var scene = Context.Scene!;

        scene.ShowGrid = true;
        scene.MainCamera.Position = new Vector3(3.4f, 2.6f, 5.2f);
        scene.MainCamera.LookAt(new Vector3(0, 0.9f, 0));

        var light = new DirectionalLight
        {
            Name = "Key",
            LightColor = System.Drawing.Color.White,
        };

        light.RotationDegrees = new Vector3(-42f, -28f, 0);

        scene.AddNode(light);

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(40f, 40f),
            Material = new Material(),
        };

        ground.Material.SetTexture("BaseColor", Procedural.Checker(256, 24));

        scene.AddNode(ground);

        foreach (var key in loaded.Keys)
        {
            var model = loaded[key];

            model.Enable = key == selectedKey;

            scene.AddNode(model);
        }

        current = loaded[selectedKey];

        Fit();
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        ReportBox();

        Context.RequestFrame();
    }

    private void OnModelChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        // 建页时模板会把初值推给下拉，触发一次「没变」的合成选中，用相等判断挡掉。
        if (e.Value is not string label || label == LabelOf(selectedKey))
            return;

        Choose(Library[ModelLabels.IndexOf(label)].Key);
    }

    private void OnCopyModeChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        // 同上：初值推入时 CopyType 会触发一次「没变」的选中，别让它凭空建出副本。
        if (e.Value is not string mode || mode == (string)CopyModeOptions[copyModeIndex]!)
            return;

        copyModeIndex = Math.Max(0, CopyModeOptions.IndexOf(mode));

        ClearClones();
        AddClones();
    }

    private void OnFit(object? sender, RoutedEventArgs e) => Fit();

    private void OnClearClones(object? sender, RoutedEventArgs e) => ClearClones();

    private void OnPaddingChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        padding = (float)e.ValueAs<double>();

        Fit();
    }

    private void OnBoxPaddingChanged(object? sender, InspectorValueChangedEventArgs e)
    {
        modelPadding = (float)e.ValueAs<double>();

        if (current != null)
            current.BoundingBoxPadding = modelPadding;

        Context.Settings.Debug.ShowBoundingBox = true;

        ReportBox();
    }

    private void OnCustomBoxToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        customBox = e.ValueAs<bool>();

        if (current != null)
            current.CustomBoundingBox = customBox
                ? new BoundingBox(new Vector3(-2f, -0.2f, -2f), new Vector3(2f, 3.4f, 2f))
                : null;

        Context.Settings.Debug.ShowBoundingBox = true;

        ReportBox();
    }

    private void OnShowBoxToggled(object? sender, InspectorValueChangedEventArgs e)
    {
        Context.Settings.Debug.ShowBoundingBox = e.ValueAs<bool>();
    }

    private void Fit()
    {
        if (current == null)
            return;

        Context.Scene?.MainCamera.FitToBoundingBox(current.BoundingBox, padding);
    }

    private CopyType ModeOf() => copyModeIndex switch
    {
        1 => CopyType.SharedResourceData,
        2 => CopyType.FullCopy,
        _ => CopyType.SharedResource,
    };

    private void AddClones()
    {
        if (current == null)
            return;

        for (int i = 0; i < 2; i++)
        {
            var clone = current.Clone(ModeOf());

            clone.Name = $"{current.Name}-clone{i}";
            clone.Enable = true;
            clone.Position = new Vector3(2.6f + i * 2.6f, 0, 0);

            // 第二份换掉自己的 BaseColor：材质若与原模型同源，原模型会跟着换图，
            // 这是判断共享边界最直白的办法——BaseColor 是 LightPass/PBR 都真读的通道。
            if (i == 1)
            {
                foreach (var mesh in clone.Meshes)
                {
                    mesh.Material?.SetTexture("BaseColor", Procedural.Checker(128, 6,
                        new Procedural.Rgb(240, 90, 40), new Procedural.Rgb(40, 200, 235)));
                }
            }

            Context.Scene!.AddNode(clone);

            clones.Add(clone);
        }

        ReportClone();
    }

    private void ClearClones()
    {
        foreach (var clone in clones)
            Context.Scene?.RemoveNode(clone);

        clones.Clear();

        ReportClone();
    }

    private string LabelOf(string key) => Library.First(i => i.Key == key).Label.T();

    // 按需那一档第一次选中时才取字节并解码；下载与解码都在批处理里排到线程池，不堵渲染。
    private async void Choose(string key)
    {
        if (loading)
            return;

        if (!loaded.TryGetValue(key, out var model))
        {
            loading = true;

            var asset = Assets.AssetManifest.All.First(a => a.Key == key);

            LoadReadout.Text = Strings.Keys.ModelViewer_LoadProgress.Format(key, asset.Bytes / 1024);

            var stopwatch = Stopwatch.StartNew();

            model = await batch.ModelAsync(key);

            stopwatch.Stop();

            model.Name = key;

            loaded[key] = model;

            Context.Scene!.AddNode(model);

            LoadReadout.Text = Strings.Keys.ModelViewer_LoadDone.Format(
                key, asset.Bytes / 1024, stopwatch.ElapsedMilliseconds, asset.Path);

            loading = false;
        }

        selectedKey = key;
        current = model;

        foreach (var loadedKey in loaded.Keys)
            loaded[loadedKey].Enable = loadedKey == key;

        ClearClones();

        Fit();

        ReportStructure();
    }

    private void ReportStructure()
    {
        if (current == null)
            return;

        var meshes = current.Meshes;

        var channels = meshes
            .Where(m => m.Material != null)
            .SelectMany(m => m.Material!.Channels)
            .Select(c => $"{c.Name}{(c.Texture == null ? "∅" : $"({c.Texture.Width}×{c.Texture.Height})")}")
            .Distinct()
            .ToList();

        var first = meshes.FirstOrDefault(m => m.Material != null)?.Material;

        StructureReadout.Text = Strings.Keys.ModelViewer_StructureReadout.Format(
            LabelOf(selectedKey),
            current.Children.Count,
            meshes.Count,
            meshes.Sum(m => m.Geometry?.VertexCount ?? 0),
            string.Join(" ", channels),
            first?.BlendMode,
            first?.DoubleSided,
            first?.AlphaCutoff);
    }

    private void ReportBox()
    {
        if (current == null)
            return;

        var box = current.BoundingBox;

        BoxReadout.Text = Strings.Keys.ModelViewer_BoxReadout.Format(
            box.Min.X, box.Min.Y, box.Min.Z,
            box.Max.X, box.Max.Y, box.Max.Z,
            current.BoundingBoxPadding,
            current.CustomBoundingBox == null
                ? Strings.Keys.ModelViewer_BoxNone.T()
                : Strings.Keys.ModelViewer_BoxSet.T());
    }

    private void ReportClone()
    {
        if (clones.Count == 0 || current == null)
        {
            CloneReadout.Text = Strings.Keys.ModelViewer_NoClones.T();

            return;
        }

        var sharedGeometry = clones
            .SelectMany(c => c.Meshes)
            .Zip(current.Meshes, (a, b) => ReferenceEquals(a.Geometry, b.Geometry))
            .All(x => x);

        var sharedMaterial = clones
            .SelectMany(c => c.Meshes)
            .Zip(current.Meshes, (a, b) => ReferenceEquals(a.Material, b.Material))
            .All(x => x);

        var originalTint = current.Meshes
            .FirstOrDefault(m => m.Material != null)?
            .Material!.GetTexture("BaseColor");

        var followed = originalTint != null && clones
            .SelectMany(c => c.Meshes)
            .Any(m => m.Material != null && ReferenceEquals(m.Material.GetTexture("BaseColor"), originalTint));

        CloneReadout.Text = Strings.Keys.ModelViewer_CloneReadout.Format(
            CopyModeNames[copyModeIndex],
            sharedGeometry ? Strings.Keys.ModelViewer_SameInstance.T() : Strings.Keys.ModelViewer_OwnCopies.T(),
            sharedMaterial ? Strings.Keys.ModelViewer_SameInstance.T() : Strings.Keys.ModelViewer_OwnCopies.T(),
            followed
                ? Strings.Keys.ModelViewer_TintFollowed.T()
                : Strings.Keys.ModelViewer_TintIndependent.T());
    }
}
