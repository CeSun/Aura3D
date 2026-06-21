using Aura3D.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// 动画混合空间，用于在多个动画之间进行基于距离的插值混合。
/// </summary>
public class AnimationBlendSpace : IAnimationSampler
{
    /// <summary>
    /// 重置混合空间到初始状态。
    /// </summary>
    public void Reset()
    {
        AxisValue = new(0, 0);
    }

    /// <summary>
    /// 初始化 <see cref="AnimationBlendSpace"/> 类的新实例。
    /// </summary>
    /// <param name="skeleton">骨骼数据。</param>
    public AnimationBlendSpace(Skeleton skeleton)
    {
        Skeleton = skeleton;

        bonesTransform = new Matrix4x4[skeleton.Bones.Count];
        BoneMatrixBuffer = new BoneMatrixBuffer(Skeleton, this);

        for (var i = 0; i < bonesTransform.Length; i++)
        {
            bonesTransform[i] = skeleton.Bones[i].WorldMatrix;
        }
    }

    /// <summary>
    /// 获取骨骼数据。
    /// </summary>
    public Skeleton Skeleton { get; private set; }

    /// <inheritdoc />
    public BoneMatrixBuffer BoneMatrixBuffer { get; }

    /// <summary>
    /// 获取或设置是否由外部更新动画。
    /// </summary>
    public bool ExternalUpdate { get; set; } = false;

    /// <inheritdoc />
    public IReadOnlyList<Matrix4x4> BonesTransform => bonesTransform;

    /// <summary>
    /// 骨骼变换矩阵数组。
    /// </summary>
    public Matrix4x4[] bonesTransform;

    List <(Vector2, IAnimationSampler)> animationSamplers = [];

    List<float> weights = new List<float>();

    /// <summary>
    /// 添加动画采样器到混合空间。
    /// </summary>
    /// <param name="point">采样器在混合空间中的位置，X 和 Y 必须在 [-1, 1] 范围内。</param>
    /// <param name="animationSampler">动画采样器。</param>
    /// <exception cref="ArgumentOutOfRangeException">当点的坐标超出范围时抛出。</exception>
    public void AddAnimationSampler(Vector2 point, IAnimationSampler animationSampler)
    {
        if (point.X > 1 || point.X < -1)
            throw new ArgumentOutOfRangeException(nameof(point), "Animation sampler point X must be in range [-1, 1].");

        if (point.Y > 1 || point.Y < -1)
            throw new ArgumentOutOfRangeException(nameof(point), "Animation sampler point Y must be in range [-1, 1].");

        animationSamplers.Add((point, animationSampler));
        weights.Add(0);

    }
    Vector2 AxisValue = default;

    /// <summary>
    /// 设置混合空间的轴值。
    /// </summary>
    /// <param name="x">X 轴值，必须在 [-1, 1] 范围内。</param>
    /// <param name="y">Y 轴值，必须在 [-1, 1] 范围内。</param>
    /// <exception cref="ArgumentOutOfRangeException">当轴值超出范围时抛出。</exception>
    public void SetAxis(float x, float y)
    {
        if (x < -1 || y < -1 || x > 1 || y > 1)
            throw new ArgumentOutOfRangeException(nameof(x), "Axis values must be in range [-1, 1].");
        AxisValue.X = x;
        AxisValue.Y = y;
    }

    /// <summary>
    /// 获取或设置反距离加权（IDW）的幂次。默认值为 2。
    /// </summary>
    public float IdwPower { get; set; } = 2f;

    /// <summary>
    /// 初始化混合姿态。在所有动画采样器添加完成后调用，
    /// 避免首帧显示绑定姿态（T-Pose）。
    /// </summary>
    public void InitializePose()
    {
        if (animationSamplers.Count == 0)
            return;
        computeBlend(0);
    }

    /// <summary>
    /// 更新混合空间，计算并应用骨骼变换。
    /// </summary>
    /// <param name="deltaTime">自上一帧以来的时间增量。</param>
    public void Update(double deltaTime)
    {
        computeBlend(deltaTime);
        BoneMatrixBuffer.MarkModified();
    }

    private void computeBlend(double deltaTime)
    {
        float totalRawWeight = 0f;

        int index = 0;
        foreach (var (point, anim) in animationSamplers)
        {
            float distance = CalculateDistance(AxisValue.X, AxisValue.Y, point.X, point.Y);

            if (distance < 0.000001)
            {
                // Exact match: clear all previous weights and assign full weight
                for (int j = 0; j < weights.Count; j++)
                    weights[j] = 0f;
                weights[index] = 1f;
                totalRawWeight = 1f;
                index++;
                break;
            }

            weights[index] = 1f / (float)MathF.Pow(distance, IdwPower);
            totalRawWeight += weights[index];
            index++;
        }

        index = 0;
        for (int i = 0; i < weights.Count; i ++)
        {
            float weight = weights[i] / totalRawWeight;
            if (weight < 0.0001)
                weight = 0;
            if (weight > 0.9999)
                weight = 1;
            weights[i] = weight;
        }

        index = 0;
        bool firstContributor = true;
        foreach (var weight in weights)
        {
            if (weight > 0)
            {
                animationSamplers[index].Item2.Update(deltaTime);
                for (int j = 0; j < BonesTransform.Count; j++)
                {
                    if (firstContributor)
                        bonesTransform[j] = animationSamplers[index].Item2.BonesTransform[j] * weight;
                    else
                        bonesTransform[j] += animationSamplers[index].Item2.BonesTransform[j] * weight;
                }
                firstContributor = false;
            }
            index++;
        }
    }

    /// <summary>
    /// 计算两点之间的欧几里得距离。
    /// </summary>
    /// <param name="x1">第一点的 X 坐标。</param>
    /// <param name="y1">第一点的 Y 坐标。</param>
    /// <param name="x2">第二点的 X 坐标。</param>
    /// <param name="y2">第二点的 Y 坐标。</param>
    /// <returns>两点之间的距离。</returns>
    private float CalculateDistance(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return (float)MathF.Sqrt(dx * dx + dy * dy);
    }
}
