using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Scenes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Avalonia;

public class Aura3DView<T> : Aura3DView where T : IRenderPipelineCreateInstance
{ 
    public Aura3DView()
    {
        CreateRenderPipeline = T.CreateInstance;
    }

}
public class Aura3DView : Aura3DViewBase
{
    public UpdateRoutedEventArgs? updateRoutedEventArgs;

    public Aura3DView()
    {
    }


    public static readonly RoutedEvent<InitializedRoutedEventArgs> SceneInitializedEvent =
      RoutedEvent.Register<Aura3DView, InitializedRoutedEventArgs>(nameof(SceneInitialized), RoutingStrategies.Direct);

    public event EventHandler<InitializedRoutedEventArgs> SceneInitialized
    {
        add => AddHandler(SceneInitializedEvent, value);
        remove => RemoveHandler(SceneInitializedEvent, value);
    }

    public static readonly RoutedEvent<DestroyedRoutedEventArgs> SceneDestroyedEvent =
     RoutedEvent.Register<Aura3DView, DestroyedRoutedEventArgs>(nameof(SceneDestroyed), RoutingStrategies.Direct);


    public event EventHandler<DestroyedRoutedEventArgs> SceneDestroyed
    {
        add => AddHandler(SceneDestroyedEvent, value);
        remove => RemoveHandler(SceneDestroyedEvent, value);
    }

    public static readonly RoutedEvent<UpdateRoutedEventArgs> OnSceneUpdatedEvent =
     RoutedEvent.Register<Aura3DView, UpdateRoutedEventArgs>(nameof(SceneUpdated), RoutingStrategies.Direct);

    public event EventHandler<UpdateRoutedEventArgs> SceneUpdated
    {
        add => AddHandler(OnSceneUpdatedEvent, value);
        remove => RemoveHandler(OnSceneUpdatedEvent, value);
    }

    public static readonly RoutedEvent<ContextLostRoutedEventArgs> ContextLostEvent =
     RoutedEvent.Register<Aura3DView, ContextLostRoutedEventArgs>(nameof(ContextLost), RoutingStrategies.Direct);

    /// <summary>
    /// 当 OpenGL 上下文丢失、场景资源失效时触发。场景与节点保持不变，可在新上下文中自动恢复。
    /// </summary>
    public event EventHandler<ContextLostRoutedEventArgs> ContextLost
    {
        add => AddHandler(ContextLostEvent, value);
        remove => RemoveHandler(ContextLostEvent, value);
    }

    public static readonly RoutedEvent<ContextRestoredRoutedEventArgs> ContextRestoredEvent =
     RoutedEvent.Register<Aura3DView, ContextRestoredRoutedEventArgs>(nameof(ContextRestored), RoutingStrategies.Direct);

    /// <summary>
    /// 当渲染管线在重建后的上下文中就绪时触发。不会重复触发 <see cref="SceneInitialized"/>。
    /// </summary>
    public event EventHandler<ContextRestoredRoutedEventArgs> ContextRestored
    {
        add => AddHandler(ContextRestoredEvent, value);
        remove => RemoveHandler(ContextRestoredEvent, value);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
    }

    protected override void OnContextLost()
    {
        RaiseEvent(new ContextLostRoutedEventArgs(ContextLostEvent, Scene!));
    }

    protected override void OnContextRestored()
    {
        RaiseEvent(new ContextRestoredRoutedEventArgs(ContextRestoredEvent, Scene!));
    }

    protected override void OnSceneInitialized()
    {
        updateRoutedEventArgs = new UpdateRoutedEventArgs(OnSceneUpdatedEvent, Scene!);
        RoutedEventArgs args = new InitializedRoutedEventArgs(SceneInitializedEvent, Scene!);
        RaiseEvent(args);
    }

    protected override void OnSceneDestroyed()
    {
        RoutedEventArgs args = new DestroyedRoutedEventArgs(SceneDestroyedEvent, Scene!);
        RaiseEvent(args);
    }

    protected override void OnSceneUpdated(double deltaTime)
    {
        updateRoutedEventArgs!.DeltaTime = deltaTime;
        RaiseEvent(updateRoutedEventArgs);
    }
}


public class UpdateRoutedEventArgs : RoutedEventArgs
{
    public Scene Scene { get; set; }
    public double DeltaTime { get; set; }
    public UpdateRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}

public class InitializedRoutedEventArgs : RoutedEventArgs
{
    public Scene Scene { get; set; }
    public InitializedRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}
public class DestroyedRoutedEventArgs : RoutedEventArgs
{
    public Scene Scene { get; set; }
    public DestroyedRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}

/// <summary>
/// OpenGL 上下文丢失事件的参数。
/// </summary>
public class ContextLostRoutedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// 关联的场景。场景与节点未受影响，仅 GPU 状态失效。
    /// </summary>
    public Scene Scene { get; set; }

    /// <summary>
    /// 初始化 <see cref="ContextLostRoutedEventArgs"/> 类的新实例。
    /// </summary>
    /// <param name="routedEvent">路由事件。</param>
    /// <param name="scene">关联的场景。</param>
    public ContextLostRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}

/// <summary>
/// OpenGL 上下文恢复事件的参数。
/// </summary>
public class ContextRestoredRoutedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// 关联的场景。与丢失前为同一实例。
    /// </summary>
    public Scene Scene { get; set; }

    /// <summary>
    /// 初始化 <see cref="ContextRestoredRoutedEventArgs"/> 类的新实例。
    /// </summary>
    /// <param name="routedEvent">路由事件。</param>
    /// <param name="scene">关联的场景。</param>
    public ContextRestoredRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}

/// <summary>
/// 物体拾取事件参数，包含被拾取命中的节点信息。
/// </summary>
public class ObjectPickedEventArgs : EventArgs
{
    /// <summary>
    /// 拾取命中的结果。
    /// </summary>
    public PickResult PickResult { get; }

    /// <summary>
    /// 被拾取到的节点（Mesh、Model 或 InstancedMesh）。
    /// </summary>
    public Node Node => PickResult.Node;

    /// <summary>
    /// 命中点在世界空间中的坐标。
    /// </summary>
    public Vector3 WorldPosition => PickResult.WorldPosition;

    /// <summary>
    /// 从射线原点到命中点的距离。
    /// </summary>
    public float Distance => PickResult.Distance;

    /// <summary>
    /// 如果是 InstancedMesh 实例被命中，则为实例索引。
    /// </summary>
    public int? InstanceIndex => PickResult.InstanceIndex;

    /// <summary>
    /// 初始化 <see cref="ObjectPickedEventArgs"/> 类的新实例。
    /// </summary>
    /// <param name="pickResult">拾取结果。</param>
    public ObjectPickedEventArgs(PickResult pickResult)
    {
        PickResult = pickResult;
    }
}