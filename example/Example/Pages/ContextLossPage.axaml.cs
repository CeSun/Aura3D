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
/// 测试用视图：把受保护的 <see cref="Aura3DViewBase.OnOpenGlLost"/> 暴露出来，
/// 以便在不真正丢失上下文的情况下驱动与真实丢失相同的处理路径。
/// </summary>
public class ContextLossTestView : Aura3DView
{
    public void SimulateContextLost() => OnOpenGlLost();
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
        vm.ViewStateText = "视图已挂载";

        var view = aura3DView;

        texture ??= await LoadTextureAsync();

        // 加载期间视图可能已被分离，此时不再向已销毁的场景添加节点。
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

        SetStatus("渲染中", "#6BCB77");
        Log($"场景初始化：新上下文 + 新场景，节点 {scene.Nodes.Count} 个");
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
            Log($"纹理加载失败：{ex.Message}");
            return null;
        }
    }

    private void Aura3DView_SceneDestroyed(object? sender, DestroyedRoutedEventArgs e)
    {
        box = null;
        SimulateButton.IsEnabled = false;

        SetStatus("场景已销毁", "#FFD93D");
        Log($"场景已销毁：GPU 资源已释放、管线终止，节点 {e.Scene.Nodes.Count} 个随场景丢弃");
    }

    private void Aura3DView_ContextLost(object? sender, ContextLostRoutedEventArgs e)
    {
        if (Vm is { } vm)
            vm.LossCount++;

        lossStopwatch.Restart();
        SimulateButton.IsEnabled = false;

        SetStatus("GPU 句柄失效", "#FF6B6B");
        Log($"上下文丢失：GPU 句柄失效，场景与 {e.Scene.Nodes.Count} 个节点保留");
    }

    private void Aura3DView_ContextRestored(object? sender, ContextRestoredRoutedEventArgs e)
    {
        if (Vm is { } vm)
            vm.RestoreCount++;

        SimulateButton.IsEnabled = true;

        SetStatus("渲染中", "#6BCB77");
        Log($"上下文恢复：GPU 资源已重建，节点 {e.Scene.Nodes.Count} 个，耗时 {lossStopwatch.Elapsed.TotalMilliseconds:F0} ms");
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
            Log("视图尚未初始化，无法模拟上下文丢失");
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
            AttachButton.Content = "分离视图（释放 GPU 资源）";

            if (Vm is { } vm)
                vm.ViewStateText = "视图已挂载";

            Log("重新挂载视图：复用原场景，仅按需重建 GPU 资源");
        }
        else
        {
            ViewHost.Content = null;
            AttachButton.Content = "重新挂载视图";

            if (Vm is { } vm)
                vm.ViewStateText = "视图已分离";

            Log("视图已分离：OnOpenGlDeinit → ReleaseGpuResources()，显存已归还且场景保留");
        }
    }

    private void ReleaseGpuResources_Click(object? sender, RoutedEventArgs e)
    {
        if (aura3DView.Scene == null)
        {
            Log("视图尚未初始化，无法释放 GPU 资源");
            return;
        }

        aura3DView.ReleaseGpuResources();
        Log("GPU 资源已释放：显存归还，场景与节点保留，下一帧按需重建");
    }

    private void DestroyScene_Click(object? sender, RoutedEventArgs e)
    {
        if (aura3DView.Scene == null)
        {
            Log("视图尚未初始化，无法销毁场景");
            return;
        }

        aura3DView.DestroyScene();
        Log("场景已销毁：Scene 置空，下一帧自动重建空场景");
    }

    private void AddNode_Click(object? sender, RoutedEventArgs e)
    {
        if (aura3DView.Scene is not { } scene)
        {
            Log("视图尚未初始化，无法添加节点");
            return;
        }

        var mesh = new Mesh
        {
            Geometry = new BoxGeometry(),
            Material = new Material { DoubleSided = true },
            Position = new Vector3(Random.Shared.NextSingle() * 6 - 3, Random.Shared.NextSingle() * 4 - 2, Random.Shared.NextSingle() * 3)
        };

        aura3DView.AddNode(mesh);
        Log($"添加立方体：当前节点 {scene.Nodes.Count} 个（丢失后仍然存在）");
    }

    private void AutoCycle_Click(object? sender, RoutedEventArgs e)
    {
        if (AutoCycleCheckBox.IsChecked == true)
        {
            autoCycleTimer.Start();
            Log("自动丢失已开启：每 2 秒触发一次");
        }
        else
        {
            autoCycleTimer.Stop();
            Log("自动丢失已停止");
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
