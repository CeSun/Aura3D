using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;

namespace Aura3D.Core.Scenes;

public class Scene
{
    public IReadOnlySet<Node> Nodes => _nodes;

    private HashSet<Node> _nodes = new HashSet<Node>();

    public RenderPipeline RenderPipeline { get; set; }

    public Scene(Func<Scene, RenderPipeline> createRenderPipeline)
    {
        RenderPipeline = createRenderPipeline(this);
    }

    public HashSet<ControlRenderTarget> ControlRenderTargets { get; } = new HashSet<ControlRenderTarget>();

    public void AddNode(Node node)
    {
        if (node.CurrentScene != null)
            throw new InvalidOperationException("Node already add to scene");

        if (Nodes.Contains(node))
            throw new InvalidOperationException("Node already exits");

        _nodes.Add(node);

        node.CurrentScene = this;

        RenderPipeline.AddNode(node);

        if (node is Camera camera)
        {
            if (camera.RenderTarget != null && camera.RenderTarget is ControlRenderTarget controlRenderTarget)
            {
                if (ControlRenderTargets.Contains(controlRenderTarget) == false)
                {
                    ControlRenderTargets.Add(controlRenderTarget);
                }
            }
        }

        foreach (var child in node.Children)
        {
            AddNode(child);
        }
    }

    public void RemoveNode(Node node)
    {
        if (node.CurrentScene == null) 
            throw new InvalidOperationException("Node is not attached to any scene.");

        if (Nodes.Contains(node) == false)
            throw new InvalidOperationException("Node does not exist in this scene.");

        _nodes.Remove(node);

        node.CurrentScene = null;

        RenderPipeline.RemoveNode(node);


        if (node is Camera camera)
        {
            if (camera.RenderTarget != null && camera.RenderTarget is ControlRenderTarget controlRenderTarget)
            {
                if (ControlRenderTargets.Contains(controlRenderTarget))
                {
                    ControlRenderTargets.Remove(controlRenderTarget);
                }
            }
        }

        foreach (var child in node.Children)
        {
            RemoveNode(child);
        }
    }


    public void Update(double deltaTime)
    {
        foreach(var node in Nodes)
        {
            node.Update(deltaTime);
        }
    }
}
