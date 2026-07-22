  using System.Numerics;
using Aura3D.Core.Math;

namespace Aura3D.Core.Resources;

/// <summary>
/// 动画类，存储动画数据和采样方法
/// </summary>
public class Animation
{
    /// <summary>
    /// 动画名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 动画持续时间（秒）
    /// </summary>
    public float Duration { get; set; } // in seconds

    /// <summary>
    /// 动画通道字典，键为骨骼名称
    /// </summary>
    public Dictionary<string, AnimationChannel> Channels { get; } = new();

    /// <summary>
    /// 关联的骨骼系统
    /// </summary>
    public Skeleton? Skeleton { get; set; }
    /// <summary>
    /// 在指定时间采样动画通道的变换矩阵
    /// </summary>
    /// <param name="channelName">通道名称（骨骼名称）</param>
    /// <param name="time">采样时间</param>
    /// <returns>变换矩阵</returns>
    public Matrix4x4 Sample(string channelName, float time)
    {
        if (!Channels.TryGetValue(channelName, out var channel))
        {
            var bone = Skeleton!.Bones.Find(b => b.Name == channelName);

            return bone!.LocalMatrix;
        }

        var position = channel.PositionKeyframes.GetValueByTime(time, SamplerHelper.Lerp);

        var rotation = channel.RotationKeyframes.GetValueByTime(time, SamplerHelper.Slerp);

        var scale = channel.ScaleKeyframes.GetValueByTime(time, SamplerHelper.Lerp);

        return MatrixHelper.CreateTransform(position, rotation, scale);
    }
}

/// <summary>
/// 动画通道，包含位置、旋转和缩放的关键帧数据
/// </summary>
public class AnimationChannel
{
    /// <summary>
    /// 位置关键帧列表
    /// </summary>
    public List<Keyframe<Vector3>> PositionKeyframes { get; } = new();
    /// <summary>
    /// 旋转关键帧列表
    /// </summary>
    public List<Keyframe<Quaternion>> RotationKeyframes { get; } = new();
    /// <summary>
    /// 缩放关键帧列表
    /// </summary>
    public List<Keyframe<Vector3>> ScaleKeyframes { get; } = new();

}
/// <summary>
/// 关键帧结构体
/// </summary>
/// <typeparam name="T">关键帧值类型</typeparam>
public struct Keyframe<T> where T : struct
{
    /// <summary>
    /// 关键帧时间
    /// </summary>
    public float Time { get; set; }
    /// <summary>
    /// 关键帧值
    /// </summary>
    public T Value { get; set; }
}


/// <summary>
/// 采样器辅助类，提供关键帧插值方法
/// </summary>
public static class SamplerHelper
{
    /// <summary>
    /// 根据时间从关键帧列表中获取插值后的值
    /// </summary>
    /// <typeparam name="T">关键帧值类型</typeparam>
    /// <param name="list">关键帧列表</param>
    /// <param name="time">采样时间</param>
    /// <param name="lerpFunc">插值函数</param>
    /// <returns>插值后的值</returns>
    public static T GetValueByTime<T>(this IReadOnlyList<Keyframe<T>> list, float time, Func<Keyframe<T>, Keyframe<T>, float, T> lerpFunc) where T : struct
    {
        if (list.Count == 0)
            throw Aura3D.Core.Exceptions.AnimationErrors.EmptyKeyframeList();

        if (list.Count == 1)
            return list[0].Value;
        if (time <= list[0].Time)
            return list[0].Value;
        if (time >= list[^1].Time)
            return list[^1].Value;
        for (int i = 0; i < list.Count - 1; i++)
        {
            if (time >= list[i].Time && time <= list[i + 1].Time)
            {
                return lerpFunc(list[i], list[i + 1], time);
            }
        }

        // Fallback: find nearest keyframe (reached due to float precision gap)
        float minDist = float.MaxValue;
        T nearest = list[0].Value;
        for (int i = 0; i < list.Count; i++)
        {
            float dist = MathF.Abs(time - list[i].Time);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = list[i].Value;
            }
        }
        return nearest;
    }


    /// <summary>
    /// 浮点数线性插值
    /// </summary>
    /// <param name="left">左侧关键帧</param>
    /// <param name="right">右侧关键帧</param>
    /// <param name="time">采样时间</param>
    /// <returns>插值结果</returns>
    public static float Lerp(Keyframe<float> left, Keyframe<float> right, float time)
    {
        float t = (time - left.Time) / (right.Time - left.Time);
        float v0 = left.Value;
        float v1 = right.Value;
        return v0 + t * (v1 - v0);
    }

    /// <summary>
    /// 三维向量线性插值
    /// </summary>
    /// <param name="left">左侧关键帧</param>
    /// <param name="right">右侧关键帧</param>
    /// <param name="time">采样时间</param>
    /// <returns>插值结果</returns>
    public static Vector3 Lerp(Keyframe<Vector3> left, Keyframe<Vector3> right, float time)
    {
        float t = (time - left.Time) / (right.Time - left.Time);
        Vector3 v0 = left.Value;
        Vector3 v1 = right.Value;
        return Vector3.Lerp(v0, v1, t);
    }

    /// <summary>
    /// 四元数球面线性插值
    /// </summary>
    /// <param name="left">左侧关键帧</param>
    /// <param name="right">右侧关键帧</param>
    /// <param name="time">采样时间</param>
    /// <returns>插值结果</returns>
    public static Quaternion Slerp(Keyframe<Quaternion> left, Keyframe<Quaternion> right, float time)
    {
        float t = (time - left.Time) / (right.Time - left.Time);
        Quaternion q0 = left.Value;
        Quaternion q1 = right.Value;
        return Quaternion.Slerp(q0, q1, t);
    }
}
