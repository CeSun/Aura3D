using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Avalonia;

public class Aura3DView : Aura3DViewBase
{
    public UpdateRoutedEventArgs updateRoutedEventArgs;

    public Aura3DView()
    {
        updateRoutedEventArgs = new UpdateRoutedEventArgs(OnSceneUpdatedEvent);
    }


    public static readonly RoutedEvent<RoutedEventArgs> SetupPipelineEvent =
      RoutedEvent.Register<Aura3DView, RoutedEventArgs>(nameof(SceneInitialized), RoutingStrategies.Direct);

    public event EventHandler<RoutedEventArgs> SetupPipeline
    {
        add => AddHandler(SetupPipelineEvent, value);
        remove => RemoveHandler(SetupPipelineEvent, value);
    }


    public static readonly RoutedEvent<RoutedEventArgs> SceneInitializedEvent =
      RoutedEvent.Register<Aura3DView, RoutedEventArgs>(nameof(SceneInitialized), RoutingStrategies.Direct);

    public event EventHandler<RoutedEventArgs> SceneInitialized
    {
        add => AddHandler(SceneInitializedEvent, value);
        remove => RemoveHandler(SceneInitializedEvent, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> SceneDestroyedEvent =
     RoutedEvent.Register<Aura3DView, RoutedEventArgs>(nameof(SceneDestroyed), RoutingStrategies.Direct);


    public event EventHandler<RoutedEventArgs> SceneDestroyed
    {
        add => AddHandler(SceneInitializedEvent, value);
        remove => RemoveHandler(SceneInitializedEvent, value);
    }

    public static readonly RoutedEvent<UpdateRoutedEventArgs> OnSceneUpdatedEvent =
     RoutedEvent.Register<Aura3DView, UpdateRoutedEventArgs>(nameof(SceneUpdated), RoutingStrategies.Direct);

    public event EventHandler<UpdateRoutedEventArgs> SceneUpdated
    {
        add => AddHandler(OnSceneUpdatedEvent, value);
        remove => RemoveHandler(OnSceneUpdatedEvent, value);
    }
    protected override void OnOpenGlInit(GlInterface gl)
    {
        RoutedEventArgs args = new RoutedEventArgs(SetupPipelineEvent);
        RaiseEvent(args);
        base.OnOpenGlInit(gl);
    }

    protected override void OnSceneInitialized()
    {
        RoutedEventArgs args = new RoutedEventArgs(SceneInitializedEvent);
        RaiseEvent(args);
    }

    protected override void OnSceneDestroyed()
    {
        RoutedEventArgs args = new RoutedEventArgs(SceneDestroyedEvent);
        RaiseEvent(args);
    }

    protected override void OnSceneUpdated(double deltaTime)
    {
        updateRoutedEventArgs.DeltaTime = deltaTime;
        RaiseEvent(updateRoutedEventArgs);
    }
}


public class UpdateRoutedEventArgs : RoutedEventArgs
{
    public double DeltaTime { get; set; }
    public UpdateRoutedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}