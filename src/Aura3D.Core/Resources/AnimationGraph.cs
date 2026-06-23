using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// 动画图，用于管理复杂的状态机动画过渡逻辑。
/// </summary>
public class AnimationGraph : AnimationSamplerBase
{
    /// <summary>
    /// 初始化 <see cref="AnimationGraph"/> 类的新实例。
    /// </summary>
    /// <param name="skeleton">骨骼数据。</param>
    /// <param name="root">动画图的根节点。</param>
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
    /// 获取或设置动画图的根节点。
    /// </summary>
    public AnimationGraphNode Root { get; set; }

    private AnimationGraphNode lastNode;
    private AnimationGraphNode currentNode;

    /// <summary>
    /// 获取当前混合权重。
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
/// 动画图节点，表示状态机中的一个动画状态。
/// </summary>
public class AnimationGraphNode
{
    /// <summary>
    /// 初始化 <see cref="AnimationGraphNode"/> 类的新实例。
    /// </summary>
    /// <param name="sampler">节点的动画采样器。</param>
    public AnimationGraphNode(IAnimationSampler sampler)
    {
        Sampler = sampler;
    }

    /// <summary>
    /// 获取或设置混合时间（秒）。
    /// </summary>
    public float BlendTime { get; set; }

    /// <summary>
    /// 获取节点的动画采样器。
    /// </summary>
    public IAnimationSampler Sampler { get; }

    /// <summary>
    /// 添加下一个可能的节点。
    /// </summary>
    /// <param name="func">判断是否应该切换到下一个节点的函数。</param>
    /// <param name="node">下一个节点。</param>
    /// <exception cref="InvalidOperationException">当尝试将节点自身添加为下一个节点时抛出。</exception>
    public void AddNextNode(Func<IAnimationSampler, double, bool> func, AnimationGraphNode node)
    {
        if (this == node)
            throw new InvalidOperationException("An animation graph node cannot reference itself as a next node.");
        NextNodes.Add((func, node));
    }

    /// <summary>
    /// 获取下一个节点的列表。
    /// </summary>
    internal List<(Func<IAnimationSampler, double, bool>, AnimationGraphNode)> NextNodes { get; } = [];
}