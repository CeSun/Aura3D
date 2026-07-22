using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the animation graph type.
/// </summary>
public class AnimationGraph : AnimationSamplerBase
{
    /// <summary>
    /// Initializes a new instance of the animation graph type.
    /// </summary>
    public AnimationGraph(Skeleton skeleton, AnimationGraphNode root)
        : base(skeleton)
    {
        Root = root;
        currentNode = root;
        lastNode = currentNode;
        startTime = DateTime.Now;

        // Copy the initial pose from the root node's sampler to avoid
        // showing T-pose before the first Update() call.
        InitializePoseFrom(currentNode.Sampler.BonesTransform);
    }

    /// <summary>
    /// Gets or sets the root.
    /// </summary>
    public AnimationGraphNode Root { get; set; }

    private AnimationGraphNode lastNode;
    private AnimationGraphNode currentNode;

    /// <summary>
    /// Gets or sets the current weight.
    /// </summary>
    public float CurrentWeight { get; private set; } = 1f;

    private DateTime startTime { get; set; } = default;

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        var timeSpan = DateTime.Now - startTime;
        double elapsedSeconds = timeSpan.TotalSeconds;

        if (elapsedSeconds < 0)
            elapsedSeconds = 0;

        if (timeSpan.TotalSeconds > currentNode.BlendTime)
        {
            CurrentWeight = 1;
        }
        else
        {
            CurrentWeight = (float)(elapsedSeconds / currentNode.BlendTime);
        }

        if (CurrentWeight < 1)
        {
            lastNode.Sampler.Update(deltaTime);
            currentNode.Sampler.Update(deltaTime);
            for (int i = 0; i < _bonesTransform.Length; i++)
            {
                _bonesTransform[i] = Matrix4x4.Lerp(lastNode.Sampler.BonesTransform[i], currentNode.Sampler.BonesTransform[i], CurrentWeight);
            }
        }
        else
        {
            currentNode.Sampler.Update(deltaTime);
            for (int i = 0; i < _bonesTransform.Length; i++)
            {
                _bonesTransform[i] = currentNode.Sampler.BonesTransform[i];
            }
        }

        // Only check transitions when the current blend is complete
        if (CurrentWeight >= 1)
        {
            foreach(var (fun, nextNode) in currentNode.NextNodes)
            {
                if (fun(currentNode.Sampler, deltaTime) == true)
                {
                    lastNode = currentNode;
                    currentNode = nextNode;
                    currentNode.Sampler.Reset();
                    startTime = DateTime.Now;
                    CurrentWeight = 0;
                    break;
                }
            }
        }

        BoneMatrixBuffer.MarkModified();
    }

    /// <inheritdoc />
    public override void Reset()
    {
        currentNode = Root;
        lastNode = currentNode;
        startTime = DateTime.Now;
    }
}

/// <summary>
/// Represents the animation graph node type.
/// </summary>
public class AnimationGraphNode
{
    /// <summary>
    /// Initializes a new instance of the animation graph node type.
    /// </summary>
    public AnimationGraphNode(IAnimationSampler sampler)
    {
        Sampler = sampler;
    }

    /// <summary>
    /// Gets or sets the blend time.
    /// </summary>
    public float BlendTime { get; set; }

    /// <summary>
    /// Gets the sampler.
    /// </summary>
    public IAnimationSampler Sampler { get; }

    /// <summary>
    /// Adds the next node.
    /// </summary>
    public void AddNextNode(Func<IAnimationSampler, double, bool> func, AnimationGraphNode node)
    {
        if (this == node)
            throw Aura3D.Core.Exceptions.AnimationErrors.GraphSelfReference();
        NextNodes.Add((func, node));
    }

    /// <summary>
    /// Gets the next nodes.
    /// </summary>
    internal List<(Func<IAnimationSampler, double, bool>, AnimationGraphNode)> NextNodes { get; } = [];
}
