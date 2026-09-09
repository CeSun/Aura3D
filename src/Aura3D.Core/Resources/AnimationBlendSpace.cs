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
    public float IdwPower
    {
        get => _idwPower;
        set
        {
            if (!float.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(IdwPower), "IDW power must be finite and greater than zero.");
            _idwPower = value;
        }
    }

    private float _idwPower = 2f;

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
        deltaTime = ValidateDeltaTime(deltaTime);
        if (_animationSamplers.Count == 0)
            return;

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

        var normalizedWeight = 0f;
        for (int i = 0; i < _weights.Count; i++)
        {
            float weight = _weights[i] / totalRawWeight;
            if (weight < 0.0001)
                weight = 0;
            if (weight > 0.9999)
                weight = 1;
            _weights[i] = weight;
            normalizedWeight += weight;
        }

        if (normalizedWeight <= 0)
            return;

        for (int i = 0; i < _weights.Count; i++)
            _weights[i] /= normalizedWeight;

        for (int i = 0; i < _weights.Count; i++)
        {
            if (_weights[i] > 0)
                _animationSamplers[i].Sampler.Update(deltaTime);
        }

        for (int boneIndex = 0; boneIndex < BonesTransform.Count; boneIndex++)
        {
            var blended = default(Matrix4x4);
            var firstContributor = true;
            var accumulatedWeight = 0f;

            for (int i = 0; i < _weights.Count; i++)
            {
                var weight = _weights[i];
                if (weight <= 0)
                    continue;

                var transform = _animationSamplers[i].Sampler.BonesTransform[boneIndex];
                if (firstContributor)
                {
                    blended = transform;
                    accumulatedWeight = weight;
                    firstContributor = false;
                    continue;
                }

                accumulatedWeight += weight;
                blended = BlendTransforms(blended, transform, weight / accumulatedWeight);
            }

            if (!firstContributor)
                _bonesTransform[boneIndex] = blended;
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
