using Aura3D.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the animation blend space type.
/// </summary>
public class AnimationBlendSpace : AnimationSamplerBase
{
    /// <summary>
    /// Initializes a new instance of the animation blend space type.
    /// </summary>
    public AnimationBlendSpace(Skeleton skeleton)
        : base(skeleton)
    {
        InitializePoseFromWorldMatrices();
    }

    private readonly List<(Vector2 Point, IAnimationSampler Sampler)> _animationSamplers = [];
    private readonly List<float> _weights = [];

    /// <summary>
    /// Adds the animation sampler.
    /// </summary>
    public void AddAnimationSampler(Vector2 point, IAnimationSampler animationSampler)
    {
        if (point.X > 1 || point.X < -1)
            throw Aura3D.Core.Exceptions.AnimationErrors.BlendPointXOutOfRange(nameof(point));

        if (point.Y > 1 || point.Y < -1)
            throw Aura3D.Core.Exceptions.AnimationErrors.BlendPointYOutOfRange(nameof(point));

        _animationSamplers.Add((point, animationSampler));
        _weights.Add(0);
    }

    private Vector2 _axisValue;

    /// <summary>
    /// Sets the axis.
    /// </summary>
    public void SetAxis(float x, float y)
    {
        if (x < -1 || y < -1 || x > 1 || y > 1)
            throw Aura3D.Core.Exceptions.AnimationErrors.BlendAxisOutOfRange(nameof(x));
        _axisValue.X = x;
        _axisValue.Y = y;
    }

    /// <summary>
    /// Gets or sets the idw power.
    /// </summary>
    public float IdwPower { get; set; } = 2f;

    /// <summary>
    /// Initializes the pose.
    /// </summary>
    public void InitializePose()
    {
        if (_animationSamplers.Count == 0)
            return;
        computeBlend(0);
    }

    /// <inheritdoc />
    public override void Update(double deltaTime)
    {
        computeBlend(deltaTime);
        BoneMatrixBuffer.MarkModified();
    }

    private void computeBlend(double deltaTime)
    {
        float totalRawWeight = 0f;

        int index = 0;
        foreach (var (point, _) in _animationSamplers)
        {
            float distance = CalculateDistance(_axisValue.X, _axisValue.Y, point.X, point.Y);

            if (distance < 0.000001)
            {
                // Exact match: clear all previous weights and assign full weight
                for (int j = 0; j < _weights.Count; j++)
                    _weights[j] = 0f;
                _weights[index] = 1f;
                totalRawWeight = 1f;
                index++;
                break;
            }

            _weights[index] = 1f / (float)MathF.Pow(distance, IdwPower);
            totalRawWeight += _weights[index];
            index++;
        }

        index = 0;
        for (int i = 0; i < _weights.Count; i++)
        {
            float weight = _weights[i] / totalRawWeight;
            if (weight < 0.0001)
                weight = 0;
            if (weight > 0.9999)
                weight = 1;
            _weights[i] = weight;
        }

        index = 0;
        bool firstContributor = true;
        foreach (var weight in _weights)
        {
            if (weight > 0)
            {
                _animationSamplers[index].Sampler.Update(deltaTime);
                for (int j = 0; j < BonesTransform.Count; j++)
                {
                    if (firstContributor)
                        _bonesTransform[j] = _animationSamplers[index].Sampler.BonesTransform[j] * weight;
                    else
                        _bonesTransform[j] += _animationSamplers[index].Sampler.BonesTransform[j] * weight;
                }
                firstContributor = false;
            }
            index++;
        }
    }

    /// <summary>
    /// Calculates the distance.
    /// </summary>
    private float CalculateDistance(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return (float)MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _axisValue = new(0, 0);
    }
}
