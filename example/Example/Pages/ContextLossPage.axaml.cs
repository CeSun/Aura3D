using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Example.ViewModels;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;

namespace Example.Pages;

/// <summary>
/// Test view: <see cref="Aura3DViewBase.SimulateContextLost"/> is already public on every platform backend of the control base class,
/// this subclass is kept only as a type marker.
/// </summary>
public class ContextLossTestView : Aura3DView
{
}

public partial class ContextLossPage : UserControl
{
    private readonly DispatcherTimer autoCycleTimer;
    private readonly Stopwatch lossStopwatch = new();
    private Texture? texture;
    private Mesh? box;
    private float rotation;

    public ContextLossPage()
    {
        InitializeComponent();

        autoCycleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        autoCycleTimer.Tick += (_, _) => SimulateContextLost();
    }

    private ContextLossViewModel? Vm => DataContext as ContextLossViewModel;

    private void Log(string message) => Vm?.AddLog(message);

    private void SetStatus(string text, string color)
    {
        if (Vm is not { } vm)
            return;

        vm.StatusText = text;
        vm.StatusColor = color;
    }

    private async void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        if (Vm is not { } vm)
            return;

        vm.SceneBuildCount++;
        vm.ViewStateText = "View attached";

        var view = aura3DView;

        texture ??= await LoadTextureAsync();

        // The view may have been detached while loading; in that case do not add nodes to an already destroyed scene.
        if (view.Scene is not { } scene)
            return;

        box = new Mesh
        {
            Geometry = new BoxGeometry(),
            Material = new Material { BaseColor = texture, DoubleSided = true },
            Position = new Vector3(-1.7f, 0, 0)
        };

        var sphere = new Mesh
        {
            Geometry = new SphereGeometry(),
            Material = new Material { BaseColor = texture, DoubleSided = true },
            Position = new Vector3(1.7f, 0, 0)
        };

        var light = new DirectionalLight
        {
            RotationDegrees = new Vector3(-30, -30, 0),
            LightColor = System.Drawing.Color.White
        };

        view.MainCamera.Position = new Vector3(0, 0, 8);
        view.MainCamera.LookAt(Vector3.Zero);

        view.AddNode(box);
        view.AddNode(sphere);
        view.AddNode(light);

        SimulateButton.IsEnabled = true;

        SetStatus("Rendering", "#6BCB77");
        Log($"Scene initialized: new context + new scene, {scene.Nodes.Count} nodes");
    }

    private async Task<Texture?> LoadTextureAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                using var stream = AssetLoader.Open(new Uri("avares://Example/Assets/Textures/background.jpg"));
                return TextureLoader.LoadTexture(stream);
            });
        }
        catch (Exception ex)
        {
            Log($"Texture load failed: {ex.Message}");
            return null;
        }
    }

    private void Aura3DView_SceneDestroyed(object? sender, DestroyedRoutedEventArgs e)
    {
        box = null;
        SimulateButton.IsEnabled = false;

        SetStatus("Scene destroyed", "#FFD93D");
        Log($"Scene destroyed: GPU resources released, pipeline terminated, {e.Scene.Nodes.Count} nodes discarded with the scene");
    }

    private void Aura3DView_ContextLost(object? sender, ContextLostRoutedEventArgs e)
    {
        if (Vm is { } vm)
            vm.LossCount++;

        lossStopwatch.Restart();
        SimulateButton.IsEnabled = false;

        SetStatus("GPU handles invalidated", "#FF6B6B");
        Log($"Context lost: GPU handles invalidated, the scene and its {e.Scene.Nodes.Count} nodes are retained");
    }

    private void Aura3DView_ContextRestored(object? sender, ContextRestoredRoutedEventArgs e)
    {
        if (Vm is { } vm)
            vm.RestoreCount++;

        SimulateButton.IsEnabled = true;

        SetStatus("Rendering", "#6BCB77");
        Log($"Context restored: GPU resources rebuilt, {e.Scene.Nodes.Count} nodes, elapsed {lossStopwatch.Elapsed.TotalMilliseconds:F0} ms");
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        if (box != null)
        {
            rotation += (float)(e.DeltaTime * 40);
            box.RotationDegrees = new Vector3(rotation * 0.5f, rotation, 0);
        }

        if (Vm is { } vm)
            vm.FrameCount++;
    }

    private void SimulateContextLost_Click(object? sender, RoutedEventArgs e) => SimulateContextLost();

    private void SimulateContextLost()
    {
        if (aura3DView.Scene == null)
        {
            Log("View not initialized yet; cannot simulate a context loss");
            return;
        }

        if (aura3DView.IsContextLost)
            return;

        aura3DView.SimulateContextLost();
    }

    private void ToggleAttach_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewHost.Content == null)
        {
            ViewHost.Content = aura3DView;
            AttachButton.Content = "Detach View (Release GPU Resources)";

            if (Vm is { } vm)
                vm.ViewStateText = "View attached";

            Log("View re-attached: reuses the existing scene and rebuilds GPU resources on demand");
        }
        else
        {
            ViewHost.Content = null;
            AttachButton.Content = "Re-attach View";

            if (Vm is { } vm)
                vm.ViewStateText = "View detached";

            Log("View detached: OnOpenGlDeinit → ReleaseGpuResources(); video memory is returned and the scene is retained");
        }
    }

    private void ReleaseGpuResources_Click(object? sender, RoutedEventArgs e)
    {
        if (aura3DView.Scene == null)
        {
            Log("View not initialized yet; cannot release GPU resources");
            return;
        }

        aura3DView.ReleaseGpuResources();
        Log("GPU resources released: video memory returned, scene and nodes retained, rebuilt on demand next frame");
    }

    private void DestroyScene_Click(object? sender, RoutedEventArgs e)
    {
        if (aura3DView.Scene == null)
        {
            Log("View not initialized yet; cannot destroy the scene");
            return;
        }

        aura3DView.DestroyScene();
        Log("Scene destroyed: Scene is set to null, an empty scene is rebuilt automatically next frame");
    }

    private void AddNode_Click(object? sender, RoutedEventArgs e)
    {
        if (aura3DView.Scene is not { } scene)
        {
            Log("View not initialized yet; cannot add nodes");
            return;
        }

        var mesh = new Mesh
        {
            Geometry = new BoxGeometry(),
            Material = new Material { DoubleSided = true },
            Position = new Vector3(Random.Shared.NextSingle() * 6 - 3, Random.Shared.NextSingle() * 4 - 2, Random.Shared.NextSingle() * 3)
        };

        aura3DView.AddNode(mesh);
        Log($"Cube added: {scene.Nodes.Count} nodes now (they still exist after a context loss)");
    }

    private void AutoCycle_Click(object? sender, RoutedEventArgs e)
    {
        if (AutoCycleCheckBox.IsChecked == true)
        {
            autoCycleTimer.Start();
            Log("Auto context loss enabled: triggers every 2 seconds");
        }
        else
        {
            autoCycleTimer.Stop();
            Log("Auto context loss stopped");
        }
    }

    private void ClearLog_Click(object? sender, RoutedEventArgs e) => Vm?.Log.Clear();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        autoCycleTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (AutoCycleCheckBox.IsChecked == true)
            autoCycleTimer.Start();
    }
}
