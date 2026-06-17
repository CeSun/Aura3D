using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Aura3D.Core.Serialization;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using DrawingColor = System.Drawing.Color;

namespace Example.Pages;

public partial class SceneEditorPage : UserControl
{
    private readonly CameraController _cameraController;
    private readonly Dictionary<Node, SceneNodeItem> _outlineLookup = [];
    private SceneEditorViewModel? _vm;
    private bool _isSyncingTreeSelection;
    private Point? _dragStartPoint;
    private PointerPressedEventArgs? _dragStartArgs;
    private SceneNodeItem? _pendingDragItem;
    private Node? _activeDraggedNode;
    private bool _isDraggingNode;

    public SceneEditorPage()
    {
        InitializeComponent();
        NodeTree.AddHandler(DragDrop.DragOverEvent, NodeTree_DragOver);
        NodeTree.AddHandler(DragDrop.DropEvent, NodeTree_Drop);
        _cameraController = new CameraController(aura3DView)
        {
            MoveSpeed = 12f,
            PanSpeed = 12f,
            ZoomSpeed = 8f
        };
        aura3DView.ObjectPicked += Aura3DView_ObjectPicked;
    }

    private async void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        _vm = DataContext as SceneEditorViewModel;
        if (_vm == null)
            return;

        _vm.SetPrimaryCamera(e.Scene.MainCamera);

        ConfigureScene(e.Scene);
        await PopulateDemoContentAsync(e.Scene);

        RefreshOutline();

        var initialSelection = e.Scene.Nodes
            .Where(static node => node is Model)
            .Cast<Node>()
            .FirstOrDefault() ?? e.Scene.MainCamera;

        SelectNode(initialSelection);

        _vm.StatusMessage = "Scene Editor ready. Load models, import .aura nodes, edit transforms, and export selected nodes.";
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        if (_vm == null || _vm.SelectedNode == null)
            return;

        if (ReferenceEquals(_vm.SelectedNode, aura3DView.MainCamera))
        {
            _vm.SyncSelectionFromNode();
            return;
        }

        _vm.RefreshSelectionRuntimeInfo();
    }

    private void Aura3DView_ObjectPicked(object? sender, ObjectPickedEventArgs e)
    {
        SelectNode(e.Node);
    }

    private void NodeTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm == null || _isSyncingTreeSelection)
            return;

        if (NodeTree.SelectedItem is SceneNodeItem item)
        {
            _vm.SelectNode(item.Node, item);
            return;
        }

        _vm.SelectNode(null);
    }

    private void NodeItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not SceneNodeItem item)
            return;

        var point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _pendingDragItem = item;
        _dragStartPoint = point.Position;
        _dragStartArgs = e;
    }

    private async void NodeItem_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingNode || _pendingDragItem == null || _dragStartPoint == null || sender is not Control control)
            return;

        var point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var delta = point.Position - _dragStartPoint.Value;
        if (Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4)
            return;

        _isDraggingNode = true;
        try
        {
            if (_dragStartArgs == null)
                return;

            _activeDraggedNode = _pendingDragItem.Node;

            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(_pendingDragItem.DisplayName));
            await DragDrop.DoDragDropAsync(_dragStartArgs, data, DragDropEffects.Move);
        }
        finally
        {
            _activeDraggedNode = null;
            _isDraggingNode = false;
            _pendingDragItem = null;
            _dragStartPoint = null;
            _dragStartArgs = null;
        }
    }

    private void NodeTree_DragOver(object? sender, DragEventArgs e)
    {
        var targetParent = TryGetTargetNode(e.Source);
        e.DragEffects = CanDropNode(targetParent) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void NodeTree_Drop(object? sender, DragEventArgs e)
    {
        var targetParent = TryGetTargetNode(e.Source);
        if (_activeDraggedNode != null && TryReparentNode(_activeDraggedNode, targetParent))
        {
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void CreateNodeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null || aura3DView.Scene == null)
            return;

        try
        {
            var node = CreateNodeFromToolSelection(Math.Max(0, NewNodeTypeComboBox.SelectedIndex), aura3DView.Scene);
            var parent = _vm.SelectedNode;
            if (parent != null && !ReferenceEquals(parent, aura3DView.Scene.MainCamera))
            {
                parent.AddChild(node, AttachToParentRule.KeepLocal);
                _vm.StatusMessage = $"Created {node.Name} under {parent.Name}.";
            }
            else
            {
                PlaceNodeInFrontOfCamera(node, 0);
                aura3DView.Scene.AddNode(node);
                _vm.StatusMessage = $"Created {node.Name} at scene root.";
            }

            RefreshOutline(node);
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = $"Failed to create node: {ex.Message}";
        }
    }

    private void AddMaterialChannelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null)
            return;

        _vm.StatusMessage = _vm.TryAddMaterialChannel(out var message)
            ? message
            : message;
    }

    private async void LoadMaterialChannelTextureButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not StyledElement { DataContext: MaterialChannelEditorItem item })
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Load Texture for {item.Name}",
            AllowMultiple = false,
            FileTypeFilter = BuildTextureFileTypes()
        });

        if (files.Count == 0)
            return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            var texture = TextureLoader.LoadTexture(stream);
            item.SetTextureSelection(texture);
            _vm.StatusMessage = $"Updated {item.Name} to use texture {files[0].Name}.";
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = $"Failed to load texture for channel {item.Name}: {ex.Message}";
        }
    }

    private void ClearMaterialChannelTextureButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not StyledElement { DataContext: MaterialChannelEditorItem item })
            return;

        item.SetTextureSelection(null);
        _vm.StatusMessage = $"Cleared texture for channel {item.Name}.";
    }

    private void RemoveMaterialChannelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not StyledElement { DataContext: MaterialChannelEditorItem item })
            return;

        _vm.StatusMessage = _vm.TryRemoveMaterialChannel(item, out var message)
            ? message
            : message;
    }

    private void AddMaterialParameterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null)
            return;

        _vm.StatusMessage = _vm.TryAddMaterialParameter(out var message)
            ? message
            : message;
    }

    private void ApplyMaterialParameterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not StyledElement { DataContext: MaterialParameterEditorItem item })
            return;

        _vm.StatusMessage = _vm.TryApplyMaterialParameter(item, out var message)
            ? message
            : message;
    }

    private void RemoveMaterialParameterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not StyledElement { DataContext: MaterialParameterEditorItem item })
            return;

        _vm.StatusMessage = _vm.TryRemoveMaterialParameter(item, out var message)
            ? message
            : message;
    }

    private async void LoadModelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null || aura3DView.Scene == null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Model Into Scene",
            AllowMultiple = true,
            FileTypeFilter = BuildModelFileTypes()
        });

        if (files.Count == 0)
            return;

        Node? lastNode = null;
        var offsetIndex = 0;

        foreach (var file in files)
        {
            try
            {
                var model = await LoadModelAsync(file);
                if (model == null)
                    continue;

                EnsureNodeHasName(model, file.Name);
                PlaceNodeInFrontOfCamera(model, offsetIndex++);
                aura3DView.Scene.AddNode(model);
                lastNode = model;
            }
            catch (Exception ex)
            {
                _vm.StatusMessage = $"Failed to load model {file.Name}: {ex.Message}";
            }
        }

        RefreshOutline(lastNode);
        if (lastNode != null)
        {
            _vm.StatusMessage = $"Loaded {files.Count} model file(s) into the scene.";
        }
    }

    private async void ImportAuraButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null || aura3DView.Scene == null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Aura Node",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Aura3D node") { Patterns = ["*.aura"] }
            ]
        });

        if (files.Count == 0)
            return;

        Node? lastNode = null;
        foreach (var file in files)
        {
            try
            {
                var node = await LoadAuraNodeAsync(file);
                EnsureNodeHasName(node, file.Name);
                aura3DView.Scene.AddNode(node);
                lastNode = node;
            }
            catch (Exception ex)
            {
                _vm.StatusMessage = $"Failed to import aura node {file.Name}: {ex.Message}";
            }
        }

        RefreshOutline(lastNode);
        if (lastNode != null)
        {
            _vm.StatusMessage = $"Imported {files.Count} aura node file(s) into the scene.";
        }
    }

    private async void ExportSelectedButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.SelectedNode == null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Selected Node",
            SuggestedFileName = BuildSuggestedAuraFileName(_vm.SelectedNode.Name),
            DefaultExtension = "aura",
            FileTypeChoices =
            [
                new FilePickerFileType("Aura3D node") { Patterns = ["*.aura"] }
            ]
        });

        if (file == null)
            return;

        try
        {
            var localPath = file.TryGetLocalPath();
            if (localPath != null)
            {
                AssetManager.SaveNode(_vm.SelectedNode, localPath);
            }
            else
            {
                await using var stream = await file.OpenWriteAsync();
                AssetManager.SaveNode(_vm.SelectedNode, stream);
            }

            _vm.StatusMessage = $"Exported {_vm.SelectedNode.Name} to {localPath ?? file.Name}.";
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = $"Failed to export node: {ex.Message}";
        }
    }

    private void DeleteSelectedButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.SelectedNode == null || aura3DView.Scene == null)
            return;

        if (ReferenceEquals(_vm.SelectedNode, aura3DView.Scene.MainCamera))
        {
            _vm.StatusMessage = "The primary scene camera is protected and cannot be deleted.";
            return;
        }

        var node = _vm.SelectedNode;
        var nextSelection = node.Parent;

        try
        {
            if (node.Parent != null)
            {
                node.Parent.RemoveChild(node, AttachToParentRule.KeepWorld);
            }
            else
            {
                aura3DView.Scene.RemoveNode(node);
            }

            RefreshOutline(nextSelection);
            _vm.StatusMessage = $"Deleted node {node.Name}.";
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = $"Failed to delete node: {ex.Message}";
        }
    }

    private void RebuildSelectedButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.SelectedNode is not InstancedMeshGroup instancedMeshGroup)
            return;

        instancedMeshGroup.Build();
        _vm.StatusMessage = $"Triggered rebuild for {instancedMeshGroup.Name}.";
    }

    private void FrameSelectedButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.SelectedNode == null)
            return;

        if (TryFrameNode(_vm.SelectedNode))
        {
            _vm.StatusMessage = $"Framed node {_vm.SelectedNode.Name}.";
        }
        else
        {
            _vm.StatusMessage = $"Node {_vm.SelectedNode.Name} has no mesh bounds. Camera moved to look at its transform.";
        }
    }

    private void RefreshOutlineButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshOutline();
        if (_vm != null)
        {
            _vm.StatusMessage = "Scene outliner refreshed.";
        }
    }

    private void ConfigureScene(Scene scene)
    {
        scene.MainCamera.Name = "Main Camera";
        scene.MainCamera.Position = new Vector3(0, 4, 12);
        scene.MainCamera.LookAt(new Vector3(0, 1.5f, 0));
        scene.MainCamera.NearPlane = 0.1f;
        scene.MainCamera.FarPlane = 150f;
        scene.ShowGrid = true;
        scene.ShowAxisGizmo = true;

        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Example/Assets/Textures/buikslotermeerplein_1k.hdr"));
            var hdriTexture = TextureLoader.LoadHdrTexture(stream);
            scene.Background = HDRIToCubeTextureConverter.ConvertFromTexture(hdriTexture, 1024);
        }
        catch
        {
        }

        var sun = new DirectionalLight
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-35, 40, 0),
            LightColor = DrawingColor.White,
            CastShadow = true,
            Irradiance = 80000
        };
        scene.AddNode(sun);
        scene.MainDirectionalLight = sun;

        var fillLight = new PointLight
        {
            Name = "Fill Light",
            Position = new Vector3(-4, 3, 3),
            AttenuationRadius = 14f,
            LuminousIntensity = 1200,
            LightColor = DrawingColor.FromArgb(255, 255, 224, 192)
        };
        scene.AddNode(fillLight);

        scene.AddNode(CreateGroundNode());
    }

    private async Task PopulateDemoContentAsync(Scene scene)
    {
        var stool = await LoadBuiltInModelAsync("avares://Example/Assets/Models/wooden_stool_02_1k.glb");
        if (stool != null)
        {
            stool.Name = "Wooden Stool";
            stool.Position = new Vector3(-2.5f, 0, 0);
            stool.Scale = Vector3.One * 2.4f;
            scene.AddNode(stool);
        }

        var lion = await LoadBuiltInModelAsync("avares://Example/Assets/Models/lion_head_1k.glb");
        if (lion != null)
        {
            lion.Name = "Lion Head";
            lion.Position = new Vector3(2.5f, 0.5f, 0);
            lion.Scale = Vector3.One * 3f;
            scene.AddNode(lion);
        }
    }

    private Mesh CreateGroundNode()
    {
        return new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(18, 18, 12, 12),
            Material = CreateSolidColorMaterial(DrawingColor.FromArgb(255, 210, 214, 220))
        };
    }

    private Node CreateNodeFromToolSelection(int selectedIndex, Scene scene)
    {
        var node = selectedIndex switch
        {
            1 => new Mesh
            {
                Geometry = new BoxGeometry(),
                Material = CreateSolidColorMaterial(DrawingColor.FromArgb(255, 214, 223, 236))
            },
            2 => new DirectionalLight
            {
                CastShadow = true,
                RotationDegrees = new Vector3(-35, 40, 0),
                Irradiance = 80000,
                LightColor = DrawingColor.White
            },
            3 => new PointLight
            {
                AttenuationRadius = 10f,
                LuminousIntensity = 1000,
                LightColor = DrawingColor.FromArgb(255, 255, 232, 210)
            },
            4 => new SpotLight
            {
                AttenuationRadius = 10f,
                LuminousIntensity = 1000,
                InnerConeAngleDegree = 15f,
                OuterAngleDegree = 25f,
                LightColor = DrawingColor.FromArgb(255, 255, 232, 210)
            },
            5 => new Camera
            {
                ProjectionType = ProjectionType.Perspective,
                FieldOfView = 75f,
                NearPlane = 0.1f,
                FarPlane = 150f
            },
            6 => new ParticleSystem
            {
                MaxParticles = 10000
            },
            7 => new BoneAttachment(),
            _ => new Node()
        };

        node.Name = GenerateUniqueNodeName(scene, selectedIndex switch
        {
            1 => "Mesh",
            2 => "Directional Light",
            3 => "Point Light",
            4 => "Spot Light",
            5 => "Camera",
            6 => "Particle System",
            7 => "Bone Attachment",
            _ => "Node"
        });

        return node;
    }

    private static Material CreateSolidColorMaterial(DrawingColor color)
    {
        return new Material
        {
            BaseColor = Texture.CreateFromColor(color)
        };
    }

    private async Task<Model?> LoadBuiltInModelAsync(string uri)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var stream = AssetLoader.Open(new Uri(uri));
                return ModelLoader.LoadGlbModel(stream);
            });
        }
        catch
        {
            return null;
        }
    }

    private void RefreshOutline(Node? preferredSelection = null)
    {
        if (_vm == null || aura3DView.Scene == null)
            return;

        var targetSelection = preferredSelection ?? _vm.SelectedNode;

        _outlineLookup.Clear();
        _vm.RootNodes.Clear();

        foreach (var node in SortNodes(aura3DView.Scene.Nodes.Where(static node => node.Parent == null)))
        {
            _vm.RootNodes.Add(BuildNodeItem(node));
        }

        if (targetSelection != null && _outlineLookup.TryGetValue(targetSelection, out var item))
        {
            _isSyncingTreeSelection = true;
            NodeTree.SelectedItem = item;
            _isSyncingTreeSelection = false;
            _vm.SelectNode(targetSelection, item);
            return;
        }

        _isSyncingTreeSelection = true;
        NodeTree.SelectedItem = null;
        _isSyncingTreeSelection = false;
        _vm.SelectNode(null);
    }

    private SceneNodeItem BuildNodeItem(Node node)
    {
        var item = new SceneNodeItem(node);
        _outlineLookup[node] = item;

        foreach (var child in SortNodes(node.Children))
        {
            item.Children.Add(BuildNodeItem(child));
        }

        return item;
    }

    private IEnumerable<Node> SortNodes(IEnumerable<Node> nodes)
    {
        return nodes
            .OrderBy(node => ReferenceEquals(node, aura3DView.Scene?.MainCamera) ? 0 : 1)
            .ThenBy(node => string.IsNullOrWhiteSpace(node.Name) ? node.GetType().Name : node.Name)
            .ThenBy(node => node.GetType().Name);
    }

    private void SelectNode(Node? node)
    {
        if (_vm == null)
            return;

        SceneNodeItem? item = null;
        if (node != null)
        {
            _outlineLookup.TryGetValue(node, out item);
        }

        _isSyncingTreeSelection = true;
        NodeTree.SelectedItem = item;
        _isSyncingTreeSelection = false;

        _vm.SelectNode(node, item);
    }

    private bool CanDropNode(Node? targetParent)
    {
        if (_activeDraggedNode == null)
            return false;

        return CanReparentNode(_activeDraggedNode, targetParent);
    }

    private static Node? TryGetTargetNode(object? source)
    {
        if (source is StyledElement { DataContext: SceneNodeItem item })
            return item.Node;

        return null;
    }

    private bool CanReparentNode(Node draggedNode, Node? targetParent)
    {
        var scene = aura3DView.Scene;
        if (scene == null)
            return false;

        if (ReferenceEquals(draggedNode, scene.MainCamera))
            return false;

        if (targetParent != null && targetParent.CurrentScene != scene)
            return false;

        if (ReferenceEquals(draggedNode, targetParent))
            return false;

        if (targetParent != null && IsDescendantOrSelf(targetParent, draggedNode))
            return false;

        if (ReferenceEquals(draggedNode.Parent, targetParent))
            return false;

        return true;
    }

    private bool TryReparentNode(Node draggedNode, Node? targetParent)
    {
        var scene = aura3DView.Scene;
        if (scene == null || _vm == null)
            return false;

        if (!CanReparentNode(draggedNode, targetParent))
        {
            _vm.StatusMessage = "Cannot move a node onto itself, its descendants, or the protected main camera.";
            return false;
        }

        try
        {
            if (draggedNode.Parent != null)
            {
                draggedNode.Parent.RemoveChild(draggedNode, AttachToParentRule.KeepWorld);
            }
            else
            {
                scene.RemoveNode(draggedNode);
            }

            if (targetParent != null)
            {
                targetParent.AddChild(draggedNode, AttachToParentRule.KeepWorld);
                _vm.StatusMessage = $"Moved {draggedNode.Name} under {targetParent.Name}.";
            }
            else
            {
                scene.AddNode(draggedNode);
                _vm.StatusMessage = $"Moved {draggedNode.Name} to scene root.";
            }

            RefreshOutline(draggedNode);
            return true;
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = $"Failed to move node: {ex.Message}";
            return false;
        }
    }

    private static bool IsDescendantOrSelf(Node node, Node ancestorCandidate)
    {
        Node? current = node;
        while (current != null)
        {
            if (ReferenceEquals(current, ancestorCandidate))
                return true;

            current = current.Parent;
        }

        return false;
    }

    private bool TryFrameNode(Node node)
    {
        var boundingBox = GetNodeBoundingBox(node);
        if (boundingBox != null)
        {
            aura3DView.MainCamera.FitToBoundingBox(boundingBox);
            return true;
        }

        var target = node.WorldTransform.Translation;
        var camera = aura3DView.MainCamera;
        var offset = camera.Forward == Vector3.Zero ? new Vector3(0, 2, 6) : -camera.Forward * 6f + Vector3.UnitY * 2f;
        camera.Position = target + offset;
        camera.LookAt(target);
        return false;
    }

    private static BoundingBox? GetNodeBoundingBox(Node node)
    {
        if (node is Mesh mesh && mesh.BoundingBox != null)
            return mesh.BoundingBox;

        if (node is Model model && model.BoundingBox != null)
            return model.BoundingBox;

        if (node is InstancedMesh instancedMesh && instancedMesh.WorldBoundingBox != null)
            return instancedMesh.WorldBoundingBox;

        if (node is InstancedMeshGroup instancedMeshGroup)
        {
            var groupBoxes = instancedMeshGroup.Groups
                .Where(static group => group.WorldBoundingBox != null)
                .Select(static group => group.WorldBoundingBox!)
                .ToList();

            if (groupBoxes.Count > 0)
                return BoundingBox.CreateMerged(groupBoxes);
        }

        var boxes = node
            .GetNodesInChildren<Mesh>()
            .Where(static childMesh => childMesh.BoundingBox != null)
            .Select(static childMesh => childMesh.BoundingBox!)
            .ToList();

        var instancedBoxes = node
            .GetNodesInChildren<InstancedMesh>()
            .Where(static childInstancedMesh => childInstancedMesh.WorldBoundingBox != null)
            .Select(static childInstancedMesh => childInstancedMesh.WorldBoundingBox!)
            .ToList();

        boxes.AddRange(instancedBoxes);

        return boxes.Count > 0 ? BoundingBox.CreateMerged(boxes) : null;
    }

    private void PlaceNodeInFrontOfCamera(Node node, int slot)
    {
        var camera = aura3DView.MainCamera;
        node.Position = camera.Position + camera.Forward * 8f + camera.Right * (slot * 3f);
    }

    private static void EnsureNodeHasName(Node node, string sourceName)
    {
        if (!string.IsNullOrWhiteSpace(node.Name) && node.Name != "Node")
            return;

        node.Name = Path.GetFileNameWithoutExtension(sourceName);
    }

    private static async Task<Node> LoadAuraNodeAsync(IStorageFile file)
    {
        var path = file.TryGetLocalPath();
        if (path != null)
            return await Task.Run(() => AssetManager.LoadNode<Node>(path));

        await using var stream = await file.OpenReadAsync();
        return await Task.Run(() => AssetManager.LoadNode<Node>(stream));
    }

    private static async Task<Model?> LoadModelAsync(IStorageFile file)
    {
        var path = file.TryGetLocalPath();
        var extension = path != null
            ? Path.GetExtension(path).ToLowerInvariant()
            : Path.GetExtension(file.Name).ToLowerInvariant();

        if (path != null)
        {
            return extension switch
            {
                ".glb" => await Task.Run(() => ModelLoader.LoadGlbModel(path)),
                ".gltf" => await Task.Run(() => ModelLoader.LoadGltfModel(path)),
                _ => await Task.Run(() => AssimpLoader.Load(path))
            };
        }

        await using var stream = await file.OpenReadAsync();
        return extension switch
        {
            ".glb" => await Task.Run(() => ModelLoader.LoadGlbModel(stream)),
            _ => await Task.Run(() => AssimpLoader.Load(stream, extension))
        };
    }

    private static List<FilePickerFileType> BuildModelFileTypes()
    {
        return
        [
            new FilePickerFileType("3D models")
            {
                Patterns = ["*.glb", "*.gltf", "*.fbx", "*.obj", "*.dae", "*.3ds", "*.ply", "*.stl", "*.blend"]
            },
            new FilePickerFileType("All files")
            {
                Patterns = ["*"]
            }
        ];
    }

    private static string BuildSuggestedAuraFileName(string? nodeName)
    {
        var baseName = string.IsNullOrWhiteSpace(nodeName) ? "node" : nodeName.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(invalidChar, '_');
        }

        return $"{baseName}.aura";
    }

    private static string GenerateUniqueNodeName(Scene scene, string baseName)
    {
        var existingNames = new HashSet<string>(
            scene.Nodes
                .Select(static node => node.Name)
                .Where(static name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);

        if (!existingNames.Contains(baseName))
            return baseName;

        var suffix = 2;
        while (existingNames.Contains($"{baseName} {suffix}"))
        {
            suffix++;
        }

        return $"{baseName} {suffix}";
    }

    private static List<FilePickerFileType> BuildTextureFileTypes()
    {
        return
        [
            new FilePickerFileType("Image files")
            {
                Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga", "*.webp", "*.gif"]
            },
            new FilePickerFileType("All files")
            {
                Patterns = ["*"]
            }
        ];
    }
}
